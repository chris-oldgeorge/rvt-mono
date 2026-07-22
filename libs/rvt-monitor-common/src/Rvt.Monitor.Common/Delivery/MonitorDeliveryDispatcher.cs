using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Delivery;

public sealed class MonitorDeliveryDispatcher
{
    private const int MaximumErrorLength = 1024;

    private readonly IMonitorDeliveryOutboxQueries queries;
    private readonly IMonitorDeliveryOutboxCommands commands;
    private readonly IMonitorDeliveryFailureSink failureSink;
    private readonly IMqttClient mqttClient;
    private readonly INotificationDeliveryService notificationDelivery;
    private readonly ILogger<MonitorDeliveryDispatcher> logger;
    private readonly MonitorDeliveryOptions options;

    public MonitorDeliveryDispatcher(
        IMonitorDeliveryOutboxQueries queries,
        IMonitorDeliveryOutboxCommands commands,
        IMonitorDeliveryFailureSink failureSink,
        IMqttClient mqttClient,
        INotificationDeliveryService notificationDelivery,
        ILogger<MonitorDeliveryDispatcher> logger,
        MonitorDeliveryOptions options)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.failureSink = failureSink ?? throw new ArgumentNullException(nameof(failureSink));
        this.mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
        this.notificationDelivery = notificationDelivery ?? throw new ArgumentNullException(nameof(notificationDelivery));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();
        for (var index = 0; index < options.BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = await queries.ClaimNextDueAsync(
                options.Producer,
                DateTime.UtcNow,
                options.LeaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                break;
            }

            MonitorDeliveryPayloadV1 payload;
            try
            {
                payload = ValidateAndDecode(message);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            {
                await RecordOutcomeAsync(
                    message,
                    exception,
                    terminal: true,
                    payload: null,
                    failures,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            MonitorDeliveryAudit? audit;
            try
            {
                using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deliveryCancellation.CancelAfter(options.DeliveryTimeout);
                audit = await DeliverAsync(message, payload, deliveryCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordOutcomeAsync(
                    message,
                    exception,
                    terminal: IsTerminal(exception, message.AttemptCount),
                    payload,
                    failures,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            var completed = await commands.CompleteAsync(
                message.Id,
                message.LeaseId,
                DateTime.UtcNow,
                audit,
                cancellationToken).ConfigureAwait(false);
            if (!completed)
            {
                LogOwnershipLoss(message);
            }
        }

        if (failures.Count > 0)
        {
            throw new MonitorDeliveryDispatchException(failures);
        }
    }

    private MonitorDeliveryPayloadV1 ValidateAndDecode(MonitorDeliveryMessage message)
    {
        if (!string.Equals(message.Producer, options.Producer, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Delivery producer does not match the configured producer.");
        }

        if (!Enum.IsDefined(message.Kind))
        {
            throw new InvalidDataException("Delivery kind is not supported.");
        }

        return MonitorDeliveryPayloadCodec.Decode(message);
    }

    private async Task<MonitorDeliveryAudit?> DeliverAsync(
        MonitorDeliveryMessage message,
        MonitorDeliveryPayloadV1 payload,
        CancellationToken cancellationToken)
    {
        switch (message.Kind)
        {
            case MonitorDeliveryKind.MqttDataInserted:
                await PublishMqttAsync(options.InsertTopic, payload, "Dto Inserted", cancellationToken)
                    .ConfigureAwait(false);
                return null;
            case MonitorDeliveryKind.MqttAlert:
                var prefix = message.Producer == MonitorDeliveryProducers.MyAtm ? "Dust" : "Noise";
                var text = $"{prefix} {payload.AlertType} {payload.Field} level={payload.Level}";
                await PublishMqttAsync(options.AlertTopic, payload, text, cancellationToken)
                    .ConfigureAwait(false);
                return null;
            case MonitorDeliveryKind.Email:
                await notificationDelivery.SendAsync(
                    new NotificationDeliveryRequest(
                        ToNotificationKind(payload.AlertType),
                        NotificationChannel.Email,
                        message.Destination,
                        payload.FleetNr,
                        NotificationUrl(message.Producer, payload)),
                    cancellationToken).ConfigureAwait(false);
                return CreateAudit(message, payload, NotificationConstants.SENT_OK, DateTime.UtcNow);
            case MonitorDeliveryKind.Sms:
                await notificationDelivery.SendAsync(
                    new NotificationDeliveryRequest(
                        ToNotificationKind(payload.AlertType),
                        NotificationChannel.Sms,
                        message.Destination,
                        payload.FleetNr,
                        NotificationUrl(message.Producer, payload)),
                    cancellationToken).ConfigureAwait(false);
                return CreateAudit(message, payload, NotificationConstants.SENT_OK, DateTime.UtcNow);
            default:
                throw new InvalidDataException("Delivery kind is not supported.");
        }
    }

    private async Task PublishMqttAsync(
        string topic,
        MonitorDeliveryPayloadV1 payload,
        string text,
        CancellationToken cancellationToken)
    {
        var mqttMessage = payload.CustomerId.HasValue
            ? new RvtMqttMessage(payload.Timestamp, payload.CustomerId.Value, payload.SerialId, text)
            : new RvtMqttMessage(payload.Timestamp, payload.SerialId, text);
        await mqttClient.PublishAsync(
            topic,
            JsonSerializer.Serialize(mqttMessage),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordOutcomeAsync(
        MonitorDeliveryMessage message,
        Exception exception,
        bool terminal,
        MonitorDeliveryPayloadV1? payload,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        var error = DeliveryError(exception);
        bool outcomeRecorded;
        if (terminal)
        {
            var audit = payload is null
                ? null
                : CreateAudit(message, payload, error, DateTime.UtcNow);
            outcomeRecorded = await commands.DeadLetterAsync(
                message.Id,
                message.LeaseId,
                DateTime.UtcNow,
                error,
                audit,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            outcomeRecorded = await commands.RetryAsync(
                message.Id,
                message.LeaseId,
                DateTime.UtcNow.Add(RetryDelay(message.AttemptCount, exception)),
                error,
                cancellationToken).ConfigureAwait(false);
        }

        if (!outcomeRecorded)
        {
            LogOwnershipLoss(message);
            return;
        }

        await RecordFailureBestEffortAsync(message, error, terminal, cancellationToken)
            .ConfigureAwait(false);

        if (terminal || options.FailureMode == MonitorDeliveryFailureMode.AnyDeliveryFailure)
        {
            failures.Add(new InvalidOperationException(
                $"Delivery message {message.Id} failed during this dispatch pass."));
        }
    }

    private async Task RecordFailureBestEffortAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken)
    {
        try
        {
            await failureSink.RecordFailureAsync(message, error, terminal, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning(
                "Delivery failure sink failed for message {DeliveryMessageId}; the fenced outbox outcome remains authoritative.",
                message.Id);
        }
    }

    private static MonitorDeliveryAudit? CreateAudit(
        MonitorDeliveryMessage message,
        MonitorDeliveryPayloadV1 payload,
        string result,
        DateTime sentAt)
    {
        if (message.Kind is not (MonitorDeliveryKind.Email or MonitorDeliveryKind.Sms) ||
            !message.NotificationId.HasValue ||
            message.NotificationId.Value != payload.NotificationId)
        {
            return null;
        }

        return new MonitorDeliveryAudit(
            payload.NotificationId,
            message.Destination,
            result,
            sentAt);
    }

    private string NotificationUrl(string producer, MonitorDeliveryPayloadV1 payload)
    {
        if (producer == MonitorDeliveryProducers.Svantek &&
            payload.AlertType is not (AlertType.Alert or AlertType.Caution))
        {
            return string.Empty;
        }

        return $"{options.PortalBaseUrl.TrimEnd('/')}/Notification/View/{payload.NotificationId}";
    }

    private static NotificationMessageKind ToNotificationKind(AlertType alertType) =>
        alertType switch
        {
            AlertType.Alert => NotificationMessageKind.Alert,
            AlertType.Caution => NotificationMessageKind.Caution,
            AlertType.BatteryAlert => NotificationMessageKind.BatteryAlert,
            AlertType.BatteryCaution => NotificationMessageKind.BatteryCaution,
            _ => NotificationMessageKind.Offline
        };

    private bool IsTerminal(Exception exception, int attemptCount) =>
        exception is DeliveryException { FailureKind: not DeliveryFailureKind.Transient } ||
        attemptCount >= options.MaxAttempts;

    private TimeSpan RetryDelay(int attemptCount, Exception exception)
    {
        var exponent = Math.Max(0, attemptCount - 1);
        var ticks = options.InitialRetryDelay.Ticks * Math.Pow(2, exponent);
        var exponential = TimeSpan.FromTicks((long)Math.Min(ticks, options.RetryCap.Ticks));
        var retryAfter = exception is DeliveryException { RetryAfter: { } requested }
            ? requested
            : TimeSpan.Zero;
        return TimeSpan.FromTicks(Math.Min(
            Math.Max(exponential.Ticks, retryAfter.Ticks),
            options.RetryCap.Ticks));
    }

    private static string DeliveryError(Exception exception)
    {
        var error = exception is DeliveryException
            ? exception.Message
            : $"Delivery failed ({exception.GetType().Name}).";
        return error.Length <= MaximumErrorLength ? error : error[..MaximumErrorLength];
    }

    private void LogOwnershipLoss(MonitorDeliveryMessage message) =>
        logger.LogWarning(
            "Delivery ownership was lost for message {DeliveryMessageId}; no further mutation will be attempted.",
            message.Id);
}

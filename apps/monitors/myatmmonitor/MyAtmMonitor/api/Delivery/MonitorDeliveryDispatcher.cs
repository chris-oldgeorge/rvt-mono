// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Delivery;

public sealed class MonitorDeliveryDispatcher
{
    private readonly IMonitorDeliveryOutboxQueries _queries;
    private readonly IMonitorDeliveryOutboxCommands _commands;
    private readonly IMonitorDeliveryFailureSink _failureSink;
    private readonly IMqttClient _mqttClient;
    private readonly INotificationDeliveryService _notificationDelivery;
    private readonly ILogger<MonitorDeliveryDispatcher> _logger;
    private readonly MonitorDeliveryOptions _options;

    public MonitorDeliveryDispatcher(
        IMonitorDeliveryOutboxQueries queries,
        IMonitorDeliveryOutboxCommands commands,
        IMonitorDeliveryFailureSink failureSink,
        IMqttClient mqttClient,
        INotificationDeliveryService notificationDelivery,
        ILogger<MonitorDeliveryDispatcher> logger,
        MonitorDeliveryOptions options)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _failureSink = failureSink ?? throw new ArgumentNullException(nameof(failureSink));
        _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
        _notificationDelivery = notificationDelivery ?? throw new ArgumentNullException(nameof(notificationDelivery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        List<Exception> failures = [];
        for (int index = 0; index < _options.BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MonitorDeliveryMessage? message = await _queries.ClaimNextDueAsync(
                _options.Producer,
                DateTime.UtcNow,
                _options.LeaseDuration,
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
                using CancellationTokenSource deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deliveryCancellation.CancelAfter(_options.DeliveryTimeout);
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

            bool completed = await _commands.CompleteAsync(
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
        if (!string.Equals(message.Producer, _options.Producer, StringComparison.Ordinal))
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
                await PublishMqttAsync(_options.InsertTopic, payload, "Dto Inserted", cancellationToken)
                    .ConfigureAwait(false);
                return null;
            case MonitorDeliveryKind.MqttAlert:
                string prefix = message.Producer == MonitorDeliveryProducers.MyAtm ? "Dust" : "Noise";
                string text = $"{prefix} {payload.AlertType} {payload.Field} level={payload.Level}";
                await PublishMqttAsync(_options.AlertTopic, payload, text, cancellationToken)
                    .ConfigureAwait(false);
                return null;
            case MonitorDeliveryKind.Email:
                await _notificationDelivery.SendAsync(
                    new NotificationDeliveryRequest(
                        ToNotificationKind(payload.AlertType),
                        NotificationChannel.Email,
                        message.Destination,
                        payload.FleetNr,
                        NotificationUrl(message.Producer, payload)),
                    cancellationToken).ConfigureAwait(false);
                return CreateAudit(message, payload, NotificationConstants.SENT_OK, DateTime.UtcNow);
            case MonitorDeliveryKind.Sms:
                await _notificationDelivery.SendAsync(
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
        RvtMqttMessage mqttMessage = payload.CustomerId.HasValue
            ? new RvtMqttMessage(payload.Timestamp, payload.CustomerId.Value, payload.SerialId, text)
            : new RvtMqttMessage(payload.Timestamp, payload.SerialId, text);
        await _mqttClient.PublishAsync(
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
        string error = DeliveryError(exception);
        bool outcomeRecorded;
        if (terminal)
        {
            MonitorDeliveryAudit? audit = payload is null
                ? null
                : CreateAudit(message, payload, error, DateTime.UtcNow);
            outcomeRecorded = await _commands.DeadLetterAsync(
                message.Id,
                message.LeaseId,
                DateTime.UtcNow,
                error,
                audit,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            outcomeRecorded = await _commands.RetryAsync(
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

        if (terminal || _options.FailureMode == MonitorDeliveryFailureMode.AnyDeliveryFailure)
        {
            failures.Add(new InvalidOperationException(
                $"Delivery message {message.Id} failed during this dispatch pass."));
        }
    }

    [SuppressMessage(
        "Major Code Smell",
        "S6667:Exception information should be passed to the logger",
        Justification = "Failure-sink exceptions can include sensitive delivery data; the log records only the affected message identity.")]
    private async Task RecordFailureBestEffortAsync(
        MonitorDeliveryMessage message,
        string error,
        bool terminal,
        CancellationToken cancellationToken)
    {
        try
        {
            await _failureSink.RecordFailureAsync(message, error, terminal, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _logger.LogWarning(
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

        return $"{_options.PortalBaseUrl.TrimEnd('/')}/Notification/View/{payload.NotificationId}";
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
        DeliveryDispatchPolicy.IsTerminal(exception, attemptCount, _options.MaxAttempts);

    private TimeSpan RetryDelay(int attemptCount, Exception exception) =>
        DeliveryRetrySchedule.NextDelay(
            attemptCount,
            _options.InitialRetryDelay,
            _options.RetryCap,
            exception);

    private static string DeliveryError(Exception exception) =>
        DeliveryDispatchPolicy.SafeError(
            exception,
            $"Delivery failed ({exception.GetType().Name}).");

    private void LogOwnershipLoss(MonitorDeliveryMessage message) =>
        _logger.LogWarning(
            "Delivery ownership was lost for message {DeliveryMessageId}; no further mutation will be attempted.",
            message.Id);
}

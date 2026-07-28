using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertDispatcher
{
    private readonly IAlertOutboxStore store;
    private readonly IReadOnlyDictionary<string, IAlertDeliveryAdapter> adapters;
    private readonly DurableAlertOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DurableAlertDispatcher> logger;

    public DurableAlertDispatcher(
        IAlertOutboxStore store,
        IEnumerable<IAlertDeliveryAdapter> adapters,
        IOptions<DurableAlertOptions> options,
        TimeProvider timeProvider,
        ILogger<DurableAlertDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.store = store;
        this.adapters = adapters.ToDictionary(adapter => adapter.Kind, StringComparer.Ordinal);
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task DispatchAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> deadLetteredIds = [];
        for (int index = 0; index < options.BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime claimTime = timeProvider.GetUtcNow().UtcDateTime;
            ClaimedAlertDelivery? message = await store.ClaimNextDueAsync(
                claimTime,
                TimeSpan.FromSeconds(options.LeaseSeconds),
                cancellationToken);
            if (message is null)
            {
                break;
            }

            try
            {
                if (!adapters.TryGetValue(message.Kind, out IAlertDeliveryAdapter? adapter))
                {
                    throw new InvalidOperationException("No alert delivery adapter is registered for the message kind.");
                }

                using CancellationTokenSource timeoutCancellation = new(
                    TimeSpan.FromSeconds(options.DeliveryTimeoutSeconds),
                    timeProvider);
                using CancellationTokenSource deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCancellation.Token);
                AlertDeliveryAudit? audit = await adapter.DeliverAsync(message, deliveryCancellation.Token);
                DateTime outcomeTime = timeProvider.GetUtcNow().UtcDateTime;
                bool completed = await store.CompleteAsync(
                    message.Id,
                    message.LeaseId,
                    outcomeTime,
                    audit,
                    cancellationToken);
                if (!completed)
                {
                    LogOwnershipLoss(message.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                bool deadLetter = IsTerminal(exception, message.AttemptCount, options);
                string safeError = exception is DeliveryException
                    ? exception.Message
                    : $"Alert delivery failed ({exception.GetType().Name}).";
                DateTime outcomeTime = timeProvider.GetUtcNow().UtcDateTime;
                DateTime nextAttemptAt = deadLetter
                    ? outcomeTime
                    : outcomeTime.Add(RetryDelay(message.AttemptCount, exception, options));
                bool retried = await store.RetryAsync(
                    message.Id,
                    message.LeaseId,
                    nextAttemptAt,
                    safeError,
                    deadLetter,
                    deadLetter ? CreateFailureAudit(message, safeError, outcomeTime) : null,
                    cancellationToken);
                if (!retried)
                {
                    LogOwnershipLoss(message.Id);
                    continue;
                }

                LogFailure(message, safeError, deadLetter);
                if (deadLetter)
                {
                    deadLetteredIds.Add(message.Id);
                }
            }
        }

        if (deadLetteredIds.Count > 0)
        {
            IEnumerable<InvalidOperationException> failures = deadLetteredIds
                .Select(id => new InvalidOperationException($"Alert delivery {id} was dead-lettered."));
            throw new AggregateException("One or more alert deliveries were dead-lettered.", failures);
        }
    }

    private static bool IsTerminal(
        Exception exception,
        int attemptCount,
        DurableAlertOptions options) =>
        exception is DeliveryException { FailureKind: not DeliveryFailureKind.Transient } ||
        attemptCount >= options.MaxAttempts;

    private static TimeSpan RetryDelay(
        int attemptCount,
        Exception exception,
        DurableAlertOptions options) =>
        DeliveryRetrySchedule.NextDelay(
            attemptCount,
            TimeSpan.FromSeconds(options.InitialRetrySeconds),
            TimeSpan.FromSeconds(options.MaxRetrySeconds),
            exception);

    private static AlertDeliveryAudit? CreateFailureAudit(
        ClaimedAlertDelivery message,
        string safeError,
        DateTime sentAt)
    {
        if (message.Kind is not (AlertDeliveryAdapterValidation.EmailKind or AlertDeliveryAdapterValidation.SmsKind))
        {
            return null;
        }

        if (message.NotificationId is not { } notificationId || notificationId == Guid.Empty)
        {
            return null;
        }

        return new AlertDeliveryAudit(
            notificationId,
            message.Destination,
            safeError,
            sentAt);
    }

    private void LogFailure(ClaimedAlertDelivery message, string safeError, bool deadLetter)
    {
        if (deadLetter)
        {
            logger.LogError(
                "Alert delivery {AlertDeliveryId} was dead-lettered for kind {AlertDeliveryKind}: {SafeError}",
                message.Id,
                message.Kind,
                safeError);
            return;
        }

        logger.LogWarning(
            "Alert delivery {AlertDeliveryId} failed for kind {AlertDeliveryKind}: {SafeError}",
            message.Id,
            message.Kind,
            safeError);
    }

    private void LogOwnershipLoss(Guid messageId) =>
        logger.LogWarning(
            "Alert delivery ownership was lost for message {AlertDeliveryId}; no further mutation will be attempted.",
            messageId);
}

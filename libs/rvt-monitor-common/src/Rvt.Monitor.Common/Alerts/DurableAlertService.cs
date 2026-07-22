using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertService : IAlertIngressPort
{
    private const int MaximumSourceLength = 128;
    private const int MaximumSourceEventKeyLength = 512;
    private const int MaximumSerialIdLength = 128;
    private const int MaximumFieldLength = 128;
    private const int MaximumMessageLength = 1024;
    private const AlertDeliveryChannels SupportedDeliveryChannels =
        AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms;

    private readonly IAlertCommitStore store;
    private readonly TimeProvider timeProvider;

    public DurableAlertService(IAlertCommitStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.timeProvider = timeProvider;
    }

    public async Task<AlertIngressResult> AcceptAsync(
        AlertSignal signal,
        CancellationToken cancellationToken = default)
    {
        Validate(signal);

        var sourceKeyHash = AlertIdentity.CreateSourceKeyHash(signal.SourceEventKey);
        var notificationId = AlertIdentity.CreateNotificationId(signal.Source, sourceKeyHash);
        var request = new AlertCommitRequest(
            signal,
            sourceKeyHash,
            notificationId,
            timeProvider.GetUtcNow().UtcDateTime);
        var result = await store.CommitAsync(request, cancellationToken);

        return new AlertIngressResult(
            result.OccurrenceId,
            result.NotificationId,
            result.Outcome,
            result.IsDuplicate);
    }

    private static void Validate(AlertSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        ValidateText(signal.Source, MaximumSourceLength, nameof(signal.Source));
        ValidateText(
            signal.SourceEventKey,
            MaximumSourceEventKeyLength,
            nameof(signal.SourceEventKey));

        if (signal.EventTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Event time must be UTC.", nameof(signal.EventTime));
        }

        ValidateText(signal.SerialId, MaximumSerialIdLength, nameof(signal.SerialId));

        if (signal.AlertType is not AlertType.Alert and not AlertType.Caution and not AlertType.Ignore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.AlertType),
                signal.AlertType,
                "Unsupported alert type.");
        }

        ValidateText(signal.Field, MaximumFieldLength, nameof(signal.Field));

        if (!double.IsFinite(signal.Level))
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.Level),
                signal.Level,
                "Level must be finite.");
        }

        if (!double.IsFinite(signal.Limit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.Limit),
                signal.Limit,
                "Limit must be finite.");
        }

        if (signal.AveragingPeriod < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.AveragingPeriod),
                signal.AveragingPeriod,
                "Averaging period cannot be negative.");
        }

        ValidateText(signal.Message, MaximumMessageLength, nameof(signal.Message));

        if ((signal.DeliveryChannels & ~SupportedDeliveryChannels) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.DeliveryChannels),
                signal.DeliveryChannels,
                "Unsupported delivery channel bits.");
        }

        if (signal.SuppressionWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signal.SuppressionWindow),
                signal.SuppressionWindow,
                "Suppression window must be positive.");
        }
    }

    private static void ValidateText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be blank.", parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }
}

using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.Common.Alerts;

public sealed record AlertSignal(
    string Source,
    string SourceEventKey,
    DateTime EventTime,
    string SerialId,
    AlertType AlertType,
    string Field,
    double Level,
    double Limit,
    int AveragingPeriod,
    string Message,
    AlertDeliveryChannels DeliveryChannels,
    TimeSpan SuppressionWindow);

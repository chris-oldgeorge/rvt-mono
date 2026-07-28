namespace Rvt.Monitor.Common.Configuration;

/// <summary>
/// The monitor-specific behaviour of shared alert rules and notification text.
/// </summary>
/// <remarks>
/// These behaviours legitimately differ per monitor, but they used to be read
/// straight from static configuration that inferred the monitor from the entry
/// assembly name. The same shared type therefore evaluated differently
/// depending on which executable happened to load it, and could not be
/// exercised without setting up global state.
///
/// Expressing the differences as an explicit value makes each behaviour a
/// stated property of the rule rather than an ambient property of the process,
/// and lets tests construct the variant they mean.
/// </remarks>
public sealed record MonitorRulePolicy(
    bool AppliesActivityTimeWindow,
    MonitorNotificationStyle NotificationStyle)
{
    /// <summary>
    /// The behaviour used when no monitor-specific policy is supplied:
    /// alert windows are honoured and notification text is generic.
    /// </summary>
    public static MonitorRulePolicy Default { get; } =
        new(AppliesActivityTimeWindow: true, MonitorNotificationStyle.Generic);

    public static MonitorRulePolicy ForMonitorKind(string? monitorKind) => monitorKind switch
    {
        // MyAtm alert rules carry no time window, so only the day applies.
        "myatm" => new MonitorRulePolicy(false, MonitorNotificationStyle.Generic),
        "omnidots" => new MonitorRulePolicy(true, MonitorNotificationStyle.Vibration),
        "airq" or "svantek" => new MonitorRulePolicy(true, MonitorNotificationStyle.Noise),
        _ => Default,
    };
}

/// <summary>
/// Selects the wording of alert notification messages.
/// </summary>
public enum MonitorNotificationStyle
{
    Generic,
    Noise,
    Vibration,
}

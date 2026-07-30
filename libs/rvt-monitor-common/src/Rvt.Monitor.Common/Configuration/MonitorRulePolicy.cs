namespace Rvt.Monitor.Common.Configuration;

/// <summary>
/// The monitor-specific behaviour of shared alert rules.
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
    bool AppliesActivityTimeWindow)
{
    /// <summary>
    /// The behaviour used when no monitor-specific policy is supplied:
    /// alert windows are honoured.
    /// </summary>
    public static MonitorRulePolicy Default { get; } =
        new(AppliesActivityTimeWindow: true);

    public static MonitorRulePolicy ForMonitorKind(string? monitorKind) => monitorKind switch
    {
        // MyAtm alert rules carry no time window, so only the day applies.
        "myatm" => new MonitorRulePolicy(false),
        _ => Default,
    };
}

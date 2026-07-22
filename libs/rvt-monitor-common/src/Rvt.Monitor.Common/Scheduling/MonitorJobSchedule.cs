namespace Rvt.Monitor.Common.Scheduling;

// Summary: Describes one configured monitor job schedule.
// Major updates:
// - 2026-06-18 Quartz scheduling: introduced shared scheduler job configuration.
public sealed record MonitorJobSchedule
{
    public string Name { get; init; } = "";

    public string Cron { get; init; } = "";

    public bool Enabled { get; init; } = true;

    public string Description { get; init; } = "";
}

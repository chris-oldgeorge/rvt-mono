using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Scheduling;

// Summary: Binds monitor scheduler configuration shared by all monitor workers.
// Major updates:
// - 2026-06-18 Quartz scheduling: introduced shared scheduler options and enabled-job filtering.
public sealed record MonitorSchedulerOptions
{
    public bool Enabled { get; init; }

    public string TimeZoneId { get; init; } = "UTC";

    public List<MonitorJobSchedule> Jobs { get; init; } = [];

    public IReadOnlyList<MonitorJobSchedule> GetEnabledJobs() =>
        [.. Jobs.Where(job => job.Enabled)];

    public static MonitorSchedulerOptions Bind(IConfiguration configuration)
    {
        MonitorSchedulerOptions options = new();
        configuration.GetSection("MonitorScheduler").Bind(options);
        return options;
    }
}

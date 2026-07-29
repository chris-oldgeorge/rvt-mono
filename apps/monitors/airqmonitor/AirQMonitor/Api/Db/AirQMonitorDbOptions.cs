using Rvt.Monitor.Common.Data;

namespace AirQ.Api.Db;

// Summary: Supplies AirQ-specific legacy-to-canonical database identifier mappings.
// Major updates:
// - 2026-06-12 Monitor Migration: added monitor-specific options for Rvt.Monitor.Common data access.
// - 2026-06-18 Canonical Timescale pass: corrected AirQ canonical table mappings used by DBUtil.
internal static class AirQMonitorDbOptions
{
    public static MonitorDbOptions Current { get; } = MonitorDbOptions.FromEnvironment(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MonitorsList"] = "monitor",
        ["AirQMonitorStatus"] = "air_q_monitor_status",
        ["AirQNoiseLevels"] = "air_q_noise_level",
        ["AirQNoise8HourAverage"] = "air_q_noise_8_hour_average",
        ["AirQErrorMessages"] = "air_q_error_message",
        ["RvtAlertRules"] = "rvt_alert_rule",
        ["Deployments"] = "deployment",
        ["Contracts"] = "contract",
        ["SiteUsers"] = "site_user",
        ["NotificationSettings"] = "notification_setting",
        ["NotificationsSent"] = "notification_sent",
        ["Notifications"] = "notification",
        ["Sites"] = "site",
        ["SiteAverages"] = "site_average",
        ["AspNetUsers"] = "\"AspNetUsers\""
    });
}

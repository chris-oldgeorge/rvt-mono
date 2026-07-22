using Rvt.Monitor.Common.Data;

namespace Svantek.Api.Db;

// Summary: Supplies Svantek-specific legacy-to-canonical database identifier mappings.
// Major updates:
// - 2026-06-12 Monitor Migration: added monitor-specific options for Rvt.Monitor.Common data access.
// - 2026-06-18 Canonical Timescale pass: corrected Svantek 8-hour average mapping used by DBUtil.
internal static class SvantekMonitorDbOptions
{
    public static MonitorDbOptions Current { get; } = MonitorDbOptions.FromEnvironment(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MonitorsList"] = "monitor",
        ["SvantekMonitorStatus"] = "svantek_monitor_status",
        ["SvantekNoiseLevels"] = "svantek_noise_level",
        ["SvantekNoise8HourAverage"] = "svantek_noise_8_hour_average",
        ["SvantekErrorMessages"] = "svantek_error_message",
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

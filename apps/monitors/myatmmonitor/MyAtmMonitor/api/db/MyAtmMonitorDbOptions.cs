using Rvt.Monitor.Common.Data;

namespace MyAtm.Api.Db;

// Summary: Supplies MyAtm-specific legacy-to-canonical database identifier mappings.
// Major updates:
// - 2026-06-12 Monitor Migration: added monitor-specific options for Rvt.Monitor.Common data access.
// - 2026-06-18 Canonical Timescale pass: corrected MyAtm canonical table mappings used by DBUtil.
internal static class MyAtmMonitorDbOptions
{
    public static MonitorDbOptions Current { get; } = MonitorDbOptions.FromEnvironment(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MonitorsList"] = "monitor",
        ["MyAtmDustLevels"] = "my_atm_dust_level",
        ["MyAtmDustLevel8hourAvg"] = "my_atm_dust_level_8_hour_avg",
        ["MyAtmAccessoryInfo"] = "my_atm_accessory_info",
        ["MyAtmErrorMessages"] = "my_atm_error_message",
        ["MyAtmAlertOccurrences"] = "my_atm_alert_occurrence",
        ["RvtAlertRules"] = "rvt_alert_rule",
        ["Deployments"] = "deployment",
        ["Contracts"] = "contract",
        ["SiteUsers"] = "site_user",
        ["NotificationSettings"] = "notification_setting",
        ["NotificationsSent"] = "notification_sent",
        ["Notifications"] = "notification",
        ["Sites"] = "site",
        ["AspNetUsers"] = "\"AspNetUsers\""
    });
}

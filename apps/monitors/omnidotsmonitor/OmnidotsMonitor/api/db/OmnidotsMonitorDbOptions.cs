using Rvt.Monitor.Common.Data;

namespace Omnidots.Api.Db;

// Summary: Supplies Omnidots-specific legacy-to-canonical database identifier mappings.
// Major updates:
// - 2026-06-12 Monitor Migration: added monitor-specific options for Rvt.Monitor.Common data access.
// - 2026-06-18 Canonical Timescale pass: confirmed Omnidots write-path table mappings for DBUtil upserts.
internal static class OmnidotsMonitorDbOptions
{
    public static MonitorDbOptions Current { get; } = MonitorDbOptions.FromEnvironment(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MonitorsList"] = "monitor",
        ["OmnidotsMonitorStatus"] = "omnidots_monitor_status",
        ["OmnidotsSensors"] = "omnidots_sensor",
        ["OmnidotsPeakLevels"] = "omnidots_peak_level",
        ["OmnidotsVeffLevels"] = "omnidots_veff_level",
        ["OmnidotsVdvLevels"] = "omnidots_vdv_level",
        ["OmnidotsImportCursor"] = "omnidots_import_cursor",
        ["OmnidotsTracesIndex"] = "omnidots_trace_index",
        ["OmnidotsTraces"] = "omnidots_trace",
        ["OmnidotsErrorMessages"] = "omnidots_error_message",
        ["RvtAlertRules"] = "rvt_alert_rule",
        ["Deployments"] = "deployment",
        ["Contracts"] = "contract",
        ["SiteUsers"] = "site_user",
        ["NotificationSettings"] = "notification_setting",
        ["NotificationsSent"] = "notification_sent",
        ["Notifications"] = "notification",
        ["Sites"] = "site",
        ["AspNetUsers"] = "\"AspNetUsers\"",
        ["AlertOccurrences"] = "alert_occurrence",
        ["AlertDeliveryOutbox"] = "alert_delivery_outbox"
    });
}

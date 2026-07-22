// File summary: Exposes API endpoints used by the React portal for dashboard api contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

namespace RvtPortal.Spa.Api;

public class DashboardSummaryResponse
{
    public string Role { get; set; } = "";
    // Function summary: Carries dashboard monitor-state totals grouped by status.
    public DashboardMonitorCounts MonitorCounts { get; set; } = new();
    public int OpenAlerts { get; set; }
    public int OpenCautions { get; set; }
    public List<OptionItem> Sites { get; set; } = [];
    public List<OptionItem> CalendarDeployments { get; set; } = [];
    public List<DashboardNotificationItem> RecentNotifications { get; set; } = [];
}

public class DashboardMonitorCounts
{
    public int New { get; set; }
    public int NotUsed { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Assigned { get; set; }
}

public class DashboardNotificationItem
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public string? FleetNumber { get; set; }
    public string SerialId { get; set; } = "";
    public string AlertType { get; set; } = "";
    public string AlertField { get; set; } = "";
    public double Level { get; set; }
    public DateTime NotificationTime { get; set; }
    public string? SiteName { get; set; }
}

public class BreachesAlertsRequest : PagedQueryRequest
{
    public DateTime? Date { get; set; }
}

public class BreachesAlertsResponse : PagedResponse<BreachesAlertsItem>
{
    public DateTime Date { get; set; }
}

public class BreachesAlertsItem
{
    public string? SerialId { get; set; }
    public string? FleetNumber { get; set; }
    public Guid? MonitorId { get; set; }
    public DateTime? SampleTime { get; set; }
    public Guid? NotificationId { get; set; }
    public DateTime? NotificationTime { get; set; }
    public double? Xvtop { get; set; }
    public double? Yvtop { get; set; }
    public double? Zvtop { get; set; }
}

public class MapMarkersRequest
{
    public Guid? SiteId { get; set; }
}

public class MapMarkersResponse
{
    public Guid? SiteId { get; set; }
    public string? SiteName { get; set; }
    public bool IsScopedToCurrentUser { get; set; }
    public List<MapMonitorMarker> Markers { get; set; } = [];
}

public class MapMonitorMarker
{
    public Guid MonitorId { get; set; }
    public Guid DeploymentId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TypeOfMonitor { get; set; } = "";
    public bool Offline { get; set; }
    public bool Alert { get; set; }
    public bool Caution { get; set; }
    public string? SiteName { get; set; }
    public string? FleetNumber { get; set; }
    public string SerialId { get; set; } = "";
    public DateTime? LastDataTime { get; set; }
    public string? What3words { get; set; }
}

public class CalendarMonthRequest
{
    public Guid? DeploymentId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class CalendarMonthResponse
{
    public Guid MonitorId { get; set; }
    public Guid DeploymentId { get; set; }
    public string FleetNumber { get; set; } = "";
    public string SerialId { get; set; } = "";
    public string TypeOfMonitor { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Unit { get; set; } = "";
    public List<OptionItem> Deployments { get; set; } = [];
    public List<CalendarMonthDayItem> Days { get; set; } = [];
}

public class CalendarMonthDayItem
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public string Status { get; set; } = "";
    public double? Average { get; set; }
    public int NotificationCount { get; set; }
}

public class CalendarDayRequest
{
    public Guid? MonitorId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
}

public class CalendarDayResponse
{
    public Guid MonitorId { get; set; }
    public DateTime DisplayDay { get; set; }
    public string FleetNumber { get; set; } = "";
    public string TypeOfMonitor { get; set; } = "";
    public string Unit { get; set; } = "";
    public List<CalendarMeasurementItem> Values { get; set; } = [];
    public List<AlertLevelItem> AlertLevels { get; set; } = [];
    public List<DashboardNotificationItem> Notifications { get; set; } = [];
}

public class CalendarMeasurementItem
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
}

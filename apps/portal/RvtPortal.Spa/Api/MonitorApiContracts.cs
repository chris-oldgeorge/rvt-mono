// File summary: Exposes API endpoints used by the React portal for monitor api contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added unattached monitor removal and archive contracts.
// - 2026-06-09 pending Added legacy monitor-detail summary, deployment, and picture-upload contracts.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using RVT.Entities;

namespace RvtPortal.Spa.Api;

public class QueryMonitorsRequest : PagedQueryRequest
{
    public string State { get; set; } = MonitorListStates.All;
    public MonitorTypeEnum? MonitorType { get; set; }
}

public static class MonitorListStates
{
    public const string All = "all";
    public const string New = "new";
    public const string NotInUse = "not-in-use";
    public const string Offline = "offline";
    public const string Online = "online";
    public const string Installer = "installer";

    // Function summary: Lists accepted monitor inventory state filter values.
    public static readonly IReadOnlySet<string> Values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        All,
        New,
        NotInUse,
        Offline,
        Online,
        Installer
    };

    // Function summary: Normalizes an incoming state filter to a supported monitor list value.
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return All;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return Values.Contains(normalized) ? normalized : All;
    }
}

public class QueryMonitorsResponse : PagedResponse<MonitorListItem>
{
    public string State { get; set; } = MonitorListStates.All;
    public bool IsScopedToCurrentUser { get; set; }
    public bool CanManage { get; set; }
    public bool CanUseInstallerTools { get; set; }
}

public class QueryUnattachedMonitorsResponse : PagedResponse<UnattachedMonitorListItem>
{
    public bool CanRemove { get; set; }
}

public class MonitorListItem
{
    public Guid Id { get; set; }
    public Guid? DeploymentId { get; set; }
    public string? FleetNumber { get; set; }
    public string SerialId { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string FirmwareVersion { get; set; } = "";
    public string TypeOfMonitor { get; set; } = "";
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public Guid? SiteId { get; set; }
    public string? SiteName { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastDataTime { get; set; }
    public bool IsAssigned { get; set; }
    public bool IsOffline { get; set; }
    public bool HasAlerts { get; set; }
    public bool HasCautions { get; set; }
    public bool CanEdit { get; set; }
    public bool CanAssign { get; set; }
    public bool CanInstallerEdit { get; set; }
}

public class UnattachedMonitorListItem : MonitorListItem
{
    public bool HasRelatedData { get; set; }
    public bool WillArchiveOnRemoval { get; set; }
    public MonitorRemovalImpactResponse Impact { get; set; } = new();
}

public class MonitorDetailResponse : MonitorListItem
{
    public DateTime ListedAtTime { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? CalibrationDue { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Location { get; set; }
    public string? What3words { get; set; }
    public string? PictureLink { get; set; }
    public string? StatusLabel { get; set; }
    public string MonitorNotes { get; set; } = "";
    public MonitorMetricSummary? LatestReading { get; set; }
    public MonitorMetricSummary? LatestAverage { get; set; }
    public MonitorMetricSummary? LatestBattery { get; set; }
    public MonitorDeploymentSummary? DeploymentSummary { get; set; }
    public List<MonitorAlertLevelItem> AlertLevels { get; set; } = [];
    public List<MonitorNotificationItem> RecentNotifications { get; set; } = [];
}

public class MonitorMetricSummary
{
    public string Label { get; set; } = "";
    public string Field { get; set; } = "";
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public DateTime? SampleTime { get; set; }
    public string? Detail { get; set; }
}

public class MonitorDeploymentSummary
{
    public Guid DeploymentId { get; set; }
    public string? ContractNumber { get; set; }
    public string? SiteName { get; set; }
    public string? CompanyName { get; set; }
    public DateTime OnHireDate { get; set; }
    public DateTime? OffHireDate { get; set; }
    public DateTime AddedDate { get; set; }
}

public class MonitorAlertLevelItem
{
    public Guid Id { get; set; }
    public string SerialId { get; set; } = "";
    public string AlertField { get; set; } = "";
    public double LimitOn { get; set; }
    public double LimitOff { get; set; }
    public string AlertType { get; set; } = "";
    public bool IsActive { get; set; }
    public int AveragingPeriod { get; set; }
    public bool Weekdays { get; set; }
    public bool Saturdays { get; set; }
    public bool Sundays { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsDeleted { get; set; }
}

public class MonitorNotificationItem
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public DateTime NotificationTime { get; set; }
    public string AlertType { get; set; } = "";
    public string AlertField { get; set; } = "";
    public double LimitOn { get; set; }
    public double Level { get; set; }
    public DateTime? ClosedTime { get; set; }
}

public class MonitorMutationRequest
{
    public string? FleetNumber { get; set; }
    public DateTime? CalibrationDate { get; set; }
    public DateTime? CalibrationDue { get; set; }
    public Guid? DeploymentId { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Location { get; set; }
    public string? What3words { get; set; }
}

public class MonitorRemovalImpactResponse
{
    public int DeploymentCount { get; set; }
    public int NotificationCount { get; set; }
    public int AlertRuleCount { get; set; }
    public int MeasurementTableCount { get; set; }
    public int MeasurementRowCount { get; set; }
    public bool HasRelatedData =>
        DeploymentCount > 0 ||
        NotificationCount > 0 ||
        AlertRuleCount > 0 ||
        MeasurementRowCount > 0;
}

public class MonitorRemovalRequest
{
    public string? Reason { get; set; }
}

public class MonitorRemovalResponse : MutationResponse
{
    public string Action { get; set; } = "";
    public MonitorRemovalImpactResponse Impact { get; set; } = new();
}

public class FleetNumberMutationRequest
{
    public string FleetNumber { get; set; } = "";
}

public class MonitorOptionsResponse
{
    public List<OptionItem> MonitorTypes { get; set; } = [];
    public List<OptionItem> Contracts { get; set; } = [];
    public List<OptionItem> Sites { get; set; } = [];
}

public class MonitorAssignmentRequest
{
    public required Guid ContractId { get; set; }
}

public class MonitorAssignmentContextResponse
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public List<OptionItem> Contracts { get; set; } = [];
    public List<MonitorListItem> AvailableMonitors { get; set; } = [];
    public List<MonitorListItem> AssignedMonitors { get; set; } = [];
}

public class DefaultMonitorsResponse
{
    public int Processed { get; set; }
    public int CreatedAlertLevels { get; set; }
    public List<Guid> MonitorIds { get; set; } = [];
}

public class InstallerDeploymentMutationRequest
{
    public string? What3words { get; set; }
    public string? Location { get; set; }
    public required double Lat { get; set; }
    public required double Lng { get; set; }
}

public class InstallerMonitorStatusResponse
{
    public Guid MonitorId { get; set; }
    public bool IsOffline { get; set; }
    public DateTime? LastDataTime { get; set; }
    public string Status { get; set; } = "";
}

public class What3WordsConvertResponse
{
    public string Words { get; set; } = "";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? NearestPlace { get; set; }
    public string Message { get; set; } = "";
}

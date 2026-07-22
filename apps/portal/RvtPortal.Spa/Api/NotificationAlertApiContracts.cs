// File summary: Exposes API endpoints used by the React portal for notification alert api contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using RVT.Entities;

namespace RvtPortal.Spa.Api;

public class QueryNotificationsRequest : PagedQueryRequest
{
    public string State { get; set; } = NotificationListStates.All;
    public MonitorTypeEnum? MonitorType { get; set; }
    public AlertTypeEnum? AlertType { get; set; }
    public bool? OpenAlerts { get; set; }
    public Guid? SiteId { get; set; }
}

public static class NotificationListStates
{
    public const string All = "all";
    public const string Open = "open";
    public const string Cautions = "cautions";

    // Function summary: Lists accepted notification state filter values.
    public static readonly IReadOnlySet<string> Values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        All,
        Open,
        Cautions
    };

    // Function summary: Normalizes an incoming state filter to a supported notification list value.
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

public class QueryNotificationsResponse : PagedResponse<NotificationListItem>
{
    public string State { get; set; } = NotificationListStates.All;
    public bool IsScopedToCurrentUser { get; set; }
    public bool CanClose { get; set; }
}

public class NotificationListItem
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public Guid? DeploymentId { get; set; }
    public string? FleetNumber { get; set; }
    public string SerialId { get; set; } = "";
    public string TypeOfMonitor { get; set; } = "";
    public string AlertType { get; set; } = "";
    public string AlertField { get; set; } = "";
    public double LimitOn { get; set; }
    public double Level { get; set; }
    public int AveragingPeriod { get; set; }
    public DateTime NotificationTime { get; set; }
    public DateTime? ClosedTime { get; set; }
    public Guid? ClosedByUser { get; set; }
    public string? ClosedNote { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public Guid? SiteId { get; set; }
    public string? SiteName { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string LimitName { get; set; } = "";
    public string AlertStatus { get; set; } = "";
    public bool CanClose { get; set; }
}

public class NotificationDetailResponse : NotificationListItem
{
    public DateTime? LastDataTime { get; set; }
    public DateTime? DeploymentStartDate { get; set; }
    public DateTime? DeploymentEndDate { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Location { get; set; }
    public string? What3words { get; set; }
    public string? RecordingLink { get; set; }
    public DateTime GraphFromUtc { get; set; }
    public DateTime GraphToUtc { get; set; }
    public List<NotificationListItem> RelatedNotifications { get; set; } = [];
    public List<AlertLevelItem> AlertLevels { get; set; } = [];
}

public class NotificationCloseRequest
{
    public string Note { get; set; } = "";
}

public class NotificationBatchCloseRequest
{
    public List<Guid> NotificationIds { get; set; } = [];
    public string Note { get; set; } = "";
}

public class NotificationBatchCloseResponse
{
    public List<Guid> ClosedIds { get; set; } = [];
    public List<Guid> NotFoundIds { get; set; } = [];
    public List<Guid> ForbiddenIds { get; set; } = [];
    public List<Guid> InvalidIds { get; set; } = [];
    public int Requested { get; set; }
}

public class QueryAlertLevelsRequest : PagedQueryRequest
{
    public Guid? MonitorId { get; set; }
}

public class QueryAlertLevelsResponse : PagedResponse<AlertLevelItem>
{
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = "";
    public string? FleetNumber { get; set; }
    public string TypeOfMonitor { get; set; } = "";
    public bool CanManage { get; set; }
    // Function summary: Carries alert-level form options for the selected monitor.
    public AlertLevelOptionsResponse Options { get; set; } = new();
}

public class AlertLevelItem
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = "";
    public string AlertField { get; set; } = "";
    public double LimitOn { get; set; }
    public double LimitOff { get; set; }
    public string AlertType { get; set; } = "";
    public bool IsActive { get; set; }
    public int AveragingPeriod { get; set; }
    public string AveragingPeriodLabel { get; set; } = "";
    public bool Weekdays { get; set; }
    public bool Saturdays { get; set; }
    public bool Sundays { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsDeleted { get; set; }
}

public class AlertLevelOptionsResponse
{
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = "";
    public string TypeOfMonitor { get; set; } = "";
    public List<OptionItem> AlertFields { get; set; } = [];
    public List<OptionItem> AlertTypes { get; set; } = [];
    public List<OptionItem> AveragingPeriods { get; set; } = [];
}

public class AlertLevelMutationRequest
{
    public required Guid MonitorId { get; set; }
    public string AlertField { get; set; } = "";
    public required double LimitOn { get; set; }
    public required double LimitOff { get; set; }
    public string AlertType { get; set; } = "";
    public required int AveragingPeriod { get; set; }
    public required bool Weekdays { get; set; }
    public required bool Saturdays { get; set; }
    public required bool Sundays { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class VibrationAlertLevelMutationRequest
{
    public required double AlertLevel { get; set; }
    public required double CautionLevel { get; set; }
}

public class VibrationAlertLevelResponse
{
    public Guid MonitorId { get; set; }
    public string SerialId { get; set; } = "";
    public double AlertLevel { get; set; }
    public double CautionLevel { get; set; }
    public bool ExternalSyncAttempted { get; set; }
    public bool ExternalSyncSucceeded { get; set; }
    public List<AlertLevelItem> AlertLevels { get; set; } = [];
}

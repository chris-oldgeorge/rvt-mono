// File summary: Exposes API endpoints used by the React portal for contract site api contracts workflows.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added per-day site operating-hours request/response contracts.
// - 2026-06-09 pending Added site monitor coordinates for embedded detail maps.
// - 2026-06-24 pending Added protected customer-logo links for report branding.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

namespace RvtPortal.Spa.Api;

public class QueryContractsRequest : PagedQueryRequest
{
    public Guid? CompanyId { get; set; }
    public Guid? SiteId { get; set; }
}
public class QueryContractsResponse : PagedResponse<ContractListItem>
{
}
public class ContractListItem
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = "";
    public DateTime OnHireDate { get; set; }
    public DateTime? OffHireDate { get; set; }
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public Guid? SiteId { get; set; }
    public string? SiteName { get; set; }
}
public class ContractDetailResponse : ContractListItem
{
    public List<OptionItem> Companies { get; set; } = [];
    public List<OptionItem> Sites { get; set; } = [];
}
public class ContractMutationRequest
{
    public string ContractNumber { get; set; } = "";
    public required Guid CompanyId { get; set; }
    public Guid? SiteId { get; set; }
    public required DateTime OnHireDate { get; set; }
    public DateTime? OffHireDate { get; set; }
}
public class ContractOptionsResponse
{
    public List<OptionItem> Companies { get; set; } = [];
    public List<OptionItem> Sites { get; set; } = [];
}
public class QuerySitesRequest : PagedQueryRequest
{
    public Guid? CompanyId { get; set; }
    public bool? IncludeArchived { get; set; }
}
public class QuerySitesResponse : PagedResponse<SiteListItem>
{
    public bool IsScopedToCurrentUser { get; set; }
}
public class SiteListItem
{
    public Guid Id { get; set; }
    public string SiteName { get; set; } = "";
    public bool Archived { get; set; }
    public DateTime CreateDate { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? SiteAddress { get; set; }
    public string? Contracts { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? SiteContact { get; set; }
    public int MonitorCount { get; set; }
    public int OpenNotificationCount { get; set; }
}
public class SiteDetailResponse : SiteListItem
{
    public string? CustomerLogoUrl { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? SatStartTime { get; set; }
    public string? SatEndTime { get; set; }
    public string? SunStartTime { get; set; }
    public string? SunEndTime { get; set; }
    public List<SiteOperatingHoursResponse> OperatingHours { get; set; } = [];
    public List<ContractListItem> ContractList { get; set; } = [];
    public List<SiteMonitorItem> Monitors { get; set; } = [];
    public List<SiteNotificationItem> OpenNotifications { get; set; } = [];
    public SiteArchiveResponse? Archive { get; set; }
    public List<OptionItem> Companies { get; set; } = [];
    public List<OptionItem> AvailableContracts { get; set; } = [];
    public bool CanManage { get; set; }
}
public class SiteMutationRequest
{
    public string SiteName { get; set; } = "";
    public required Guid CompanyId { get; set; }
    public Guid? ContractId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? SatStartTime { get; set; }
    public string? SatEndTime { get; set; }
    public string? SunStartTime { get; set; }
    public string? SunEndTime { get; set; }
    public List<SiteOperatingHoursMutationRequest>? OperatingHours { get; set; }
}
public class SiteOperatingHoursResponse
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = "";
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsClosed { get; set; }
}
public class SiteOperatingHoursMutationRequest
{
    public required int DayOfWeek { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public required bool IsClosed { get; set; }
}
public class SiteOptionsResponse
{
    public List<OptionItem> Companies { get; set; } = [];
    public List<OptionItem> Contracts { get; set; } = [];
}
public class SiteArchiveResponse
{
    public DateTime Archived { get; set; }
    public string? CreatedBy { get; set; }
    public string? PictureLink { get; set; }
}
public class QuerySiteMonitorsResponse : PagedResponse<SiteMonitorItem>
{
}
public class SiteMonitorItem
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public string? FleetNumber { get; set; }
    public string? SerialId { get; set; }
    public string? MonitorName { get; set; }
    public string TypeOfMonitor { get; set; } = "";
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = "";
    public DateTime? LastDataTime { get; set; }
    public bool OffLine { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? What3words { get; set; }
}
public class QuerySiteNotificationsResponse : PagedResponse<SiteNotificationItem>
{
}
public class SiteNotificationItem
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public string? FleetNumber { get; set; }
    public string? SerialId { get; set; }
    public string TypeOfMonitor { get; set; } = "";
    public string AlertType { get; set; } = "";
    public string? AlertField { get; set; }
    public double? LimitOn { get; set; }
    public double? Level { get; set; }
    public DateTime NotificationTime { get; set; }
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = "";
}
public class SiteNotificationSettingsResponse
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public List<SiteNotificationSettingItem> Settings { get; set; } = [];
}
public class SiteNotificationSettingItem
{
    public Guid SiteUserId { get; set; }
    public Guid SiteId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public string? UserName { get; set; }
    public bool SiteContact { get; set; }
    public bool Email { get; set; }
    public bool Sms { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}
public class SiteNotificationSettingMutationRequest
{
    public required bool Email { get; set; }
    public required bool Sms { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

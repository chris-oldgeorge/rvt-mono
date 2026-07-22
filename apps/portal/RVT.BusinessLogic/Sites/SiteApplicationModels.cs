// File summary: Defines transport-neutral site application models used by the business layer.
// Major updates:
// - 2026-07-05 pending Added site models for controller-to-business refactoring.
// - 2026-07-05 pending Added site monitor, notification, and notification-setting workflow models.

using RVT.BusinessLogic.Application.Paging;

namespace RVT.BusinessLogic.Sites;

public sealed record SiteQuery(
    Guid? CompanyId,
    bool? IncludeArchived,
    PageRequest Page);

public sealed record SiteMutation(
    string SiteName,
    Guid CompanyId,
    Guid? ContractId,
    string? AddressLine1,
    string? AddressLine2,
    string? Postcode,
    string? City,
    string? County,
    string? StartTime,
    string? EndTime,
    string? SatStartTime,
    string? SatEndTime,
    string? SunStartTime,
    string? SunEndTime,
    IReadOnlyList<SiteOperatingHoursMutation>? OperatingHours);

public sealed record SiteOperatingHoursMutation(
    int DayOfWeek,
    string? StartTime,
    string? EndTime,
    bool IsClosed);

public sealed record SiteOperatingHoursModel(
    int DayOfWeek,
    string DayName,
    string? StartTime,
    string? EndTime,
    bool IsClosed);

public sealed record SiteOptionModel(string Value, string Label);

public sealed record SiteContractModel(
    Guid Id,
    string ContractNumber,
    DateTime OnHireDate,
    DateTime? OffHireDate,
    Guid CompanyId,
    string? CompanyName,
    Guid? SiteId,
    string? SiteName);

public sealed record SiteArchiveModel(
    DateTime Archived,
    string? CreatedBy,
    string? PictureLink);

public sealed record SiteMonitorModel(
    Guid Id,
    Guid DeploymentId,
    string? FleetNumber,
    string? SerialId,
    string? MonitorName,
    string TypeOfMonitor,
    Guid ContractId,
    string ContractNumber,
    DateTime? LastDataTime,
    bool OffLine,
    double? Lat,
    double? Lng,
    string? What3words);

public sealed record SiteNotificationModel(
    Guid Id,
    Guid MonitorId,
    string? FleetNumber,
    string? SerialId,
    string TypeOfMonitor,
    string AlertType,
    string? AlertField,
    double? LimitOn,
    double? Level,
    DateTime NotificationTime,
    Guid ContractId,
    string ContractNumber);

public sealed record SiteNotificationSettingMutation(
    bool Email,
    bool Sms,
    string? StartTime,
    string? EndTime);

public sealed record SiteNotificationSettingModel(
    Guid SiteUserId,
    Guid SiteId,
    Guid UserId,
    string UserEmail,
    string? UserName,
    bool SiteContact,
    bool Email,
    bool Sms,
    string? StartTime,
    string? EndTime);

public sealed class SiteNotificationSettingsModel
{
    public Guid SiteId { get; init; }
    public string? SiteName { get; init; }
    public List<SiteNotificationSettingModel> Settings { get; init; } = [];
}

public class SiteListModel
{
    public Guid Id { get; init; }
    public string SiteName { get; init; } = "";
    public bool Archived { get; init; }
    public DateTime CreateDate { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? Postcode { get; init; }
    public string? City { get; init; }
    public string? County { get; init; }
    public string? SiteAddress { get; init; }
    public string? Contracts { get; init; }
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string? SiteContact { get; init; }
    public int MonitorCount { get; set; }
    public int OpenNotificationCount { get; set; }
}

public sealed class SiteDetailModel : SiteListModel
{
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public string? SatStartTime { get; init; }
    public string? SatEndTime { get; init; }
    public string? SunStartTime { get; init; }
    public string? SunEndTime { get; init; }
    public List<SiteOperatingHoursModel> OperatingHours { get; init; } = [];
    public List<SiteContractModel> ContractList { get; init; } = [];
    public List<SiteMonitorModel> Monitors { get; init; } = [];
    public List<SiteNotificationModel> OpenNotifications { get; init; } = [];
    public SiteArchiveModel? Archive { get; init; }
    public List<SiteOptionModel> Companies { get; init; } = [];
    public List<SiteOptionModel> AvailableContracts { get; init; } = [];
    public bool CanManage { get; init; }
}

public sealed class SiteOptionsModel
{
    public List<SiteOptionModel> Companies { get; init; } = [];
    public List<SiteOptionModel> Contracts { get; init; } = [];
}

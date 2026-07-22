// File summary: Maps site business-layer models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-05 pending Added site API mapper for controller-to-business refactoring.
// - 2026-07-05 pending Added mappings for site monitor, open-notification, and notification-setting workflows.

using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Sites;

namespace RvtPortal.Spa.Api.Mappers;

public static class SiteApiMapper
{
    // Function summary: Maps paged site application results to the existing API response contract.
    public static QuerySitesResponse ToQueryResponse(PagedResult<SiteListModel> result, bool isScopedToCurrentUser)
    {
        return new QuerySitesResponse
        {
            Results = result.Results.Select(ToListItem).ToList(),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir,
            IsScopedToCurrentUser = isScopedToCurrentUser
        };
    }

    // Function summary: Maps site options to the existing API response contract.
    public static SiteOptionsResponse ToOptionsResponse(SiteOptionsModel model)
    {
        return new SiteOptionsResponse
        {
            Companies = model.Companies.Select(ToOptionItem).ToList(),
            Contracts = model.Contracts.Select(ToOptionItem).ToList()
        };
    }

    // Function summary: Maps site detail to the existing API response contract.
    public static SiteDetailResponse ToDetailResponse(SiteDetailModel model, string? customerLogoUrl)
    {
        var item = ToListItem(model);
        return new SiteDetailResponse
        {
            Id = item.Id,
            SiteName = item.SiteName,
            Archived = item.Archived,
            CreateDate = item.CreateDate,
            AddressLine1 = item.AddressLine1,
            AddressLine2 = item.AddressLine2,
            Postcode = item.Postcode,
            City = item.City,
            County = item.County,
            SiteAddress = item.SiteAddress,
            Contracts = item.Contracts,
            CompanyId = item.CompanyId,
            CompanyName = item.CompanyName,
            SiteContact = item.SiteContact,
            MonitorCount = item.MonitorCount,
            OpenNotificationCount = item.OpenNotificationCount,
            CustomerLogoUrl = customerLogoUrl,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            SatStartTime = model.SatStartTime,
            SatEndTime = model.SatEndTime,
            SunStartTime = model.SunStartTime,
            SunEndTime = model.SunEndTime,
            OperatingHours = model.OperatingHours.Select(ToOperatingHours).ToList(),
            ContractList = model.ContractList.Select(ToContractItem).ToList(),
            Monitors = model.Monitors.Select(ToMonitorItem).ToList(),
            OpenNotifications = model.OpenNotifications.Select(ToNotificationItem).ToList(),
            Archive = model.Archive == null ? null : new SiteArchiveResponse
            {
                Archived = model.Archive.Archived,
                CreatedBy = model.Archive.CreatedBy,
                PictureLink = model.Archive.PictureLink
            },
            Companies = model.Companies.Select(ToOptionItem).ToList(),
            AvailableContracts = model.AvailableContracts.Select(ToOptionItem).ToList(),
            CanManage = model.CanManage
        };
    }

    // Function summary: Maps site mutation request DTOs to transport-neutral business input.
    public static SiteMutation ToMutation(SiteMutationRequest request)
    {
        return new SiteMutation(
            request.SiteName,
            request.CompanyId,
            request.ContractId,
            request.AddressLine1,
            request.AddressLine2,
            request.Postcode,
            request.City,
            request.County,
            request.StartTime,
            request.EndTime,
            request.SatStartTime,
            request.SatEndTime,
            request.SunStartTime,
            request.SunEndTime,
            request.OperatingHours?.Select(hours => new SiteOperatingHoursMutation(
                hours.DayOfWeek,
                hours.StartTime,
                hours.EndTime,
                hours.IsClosed)).ToList());
    }

    // Function summary: Maps paged site monitor results to the existing API response contract.
    public static QuerySiteMonitorsResponse ToMonitorQueryResponse(PagedResult<SiteMonitorModel> result)
    {
        return new QuerySiteMonitorsResponse
        {
            Results = result.Results.Select(ToMonitorItem).ToList(),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir
        };
    }

    // Function summary: Maps paged site open-notification results to the existing API response contract.
    public static QuerySiteNotificationsResponse ToNotificationQueryResponse(PagedResult<SiteNotificationModel> result)
    {
        return new QuerySiteNotificationsResponse
        {
            Results = result.Results.Select(ToNotificationItem).ToList(),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir
        };
    }

    // Function summary: Maps notification-setting business models to the existing API response contract.
    public static SiteNotificationSettingsResponse ToNotificationSettingsResponse(SiteNotificationSettingsModel model)
    {
        return new SiteNotificationSettingsResponse
        {
            SiteId = model.SiteId,
            SiteName = model.SiteName ?? "",
            Settings = model.Settings.Select(ToNotificationSettingItem).ToList()
        };
    }

    // Function summary: Maps one notification-setting business model to the existing API item contract.
    public static SiteNotificationSettingItem ToNotificationSettingItem(SiteNotificationSettingModel model)
    {
        return new SiteNotificationSettingItem
        {
            SiteUserId = model.SiteUserId,
            SiteId = model.SiteId,
            UserId = model.UserId,
            UserEmail = model.UserEmail,
            UserName = model.UserName,
            SiteContact = model.SiteContact,
            Email = model.Email,
            Sms = model.Sms,
            StartTime = model.StartTime,
            EndTime = model.EndTime
        };
    }

    // Function summary: Maps a notification-setting mutation DTO to business-layer input.
    public static SiteNotificationSettingMutation ToNotificationSettingMutation(SiteNotificationSettingMutationRequest request)
    {
        return new SiteNotificationSettingMutation(request.Email, request.Sms, request.StartTime, request.EndTime);
    }

    // Function summary: Maps a business-layer site row to the existing site list DTO.
    private static SiteListItem ToListItem(SiteListModel model)
    {
        return new SiteListItem
        {
            Id = model.Id,
            SiteName = model.SiteName,
            Archived = model.Archived,
            CreateDate = model.CreateDate,
            AddressLine1 = model.AddressLine1,
            AddressLine2 = model.AddressLine2,
            Postcode = model.Postcode,
            City = model.City,
            County = model.County,
            SiteAddress = model.SiteAddress,
            Contracts = model.Contracts,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            SiteContact = model.SiteContact,
            MonitorCount = model.MonitorCount,
            OpenNotificationCount = model.OpenNotificationCount
        };
    }

    // Function summary: Maps business-layer daily operating hours to the existing site hours DTO.
    private static SiteOperatingHoursResponse ToOperatingHours(SiteOperatingHoursModel model)
    {
        return new SiteOperatingHoursResponse
        {
            DayOfWeek = model.DayOfWeek,
            DayName = model.DayName,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            IsClosed = model.IsClosed
        };
    }

    // Function summary: Maps a business-layer contract row to the existing contract list DTO.
    private static ContractListItem ToContractItem(SiteContractModel model)
    {
        return new ContractListItem
        {
            Id = model.Id,
            ContractNumber = model.ContractNumber,
            OnHireDate = model.OnHireDate,
            OffHireDate = model.OffHireDate,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            SiteId = model.SiteId,
            SiteName = model.SiteName
        };
    }

    // Function summary: Maps a business-layer site monitor row to the existing embedded monitor DTO.
    private static SiteMonitorItem ToMonitorItem(SiteMonitorModel model)
    {
        return new SiteMonitorItem
        {
            Id = model.Id,
            DeploymentId = model.DeploymentId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            MonitorName = model.MonitorName,
            TypeOfMonitor = model.TypeOfMonitor,
            ContractId = model.ContractId,
            ContractNumber = model.ContractNumber,
            LastDataTime = model.LastDataTime,
            OffLine = model.OffLine,
            Lat = model.Lat,
            Lng = model.Lng,
            What3words = model.What3words
        };
    }

    // Function summary: Maps a business-layer open notification row to the existing embedded notification DTO.
    private static SiteNotificationItem ToNotificationItem(SiteNotificationModel model)
    {
        return new SiteNotificationItem
        {
            Id = model.Id,
            MonitorId = model.MonitorId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            TypeOfMonitor = model.TypeOfMonitor,
            AlertType = model.AlertType,
            AlertField = model.AlertField,
            LimitOn = model.LimitOn,
            Level = model.Level,
            NotificationTime = model.NotificationTime,
            ContractId = model.ContractId,
            ContractNumber = model.ContractNumber
        };
    }

    // Function summary: Maps a business-layer option row to the existing option DTO.
    private static OptionItem ToOptionItem(SiteOptionModel model)
    {
        return new OptionItem
        {
            Value = model.Value,
            Label = model.Label
        };
    }
}

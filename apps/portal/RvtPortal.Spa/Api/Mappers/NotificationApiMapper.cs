// File summary: Maps notification application-service models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added notification read-service mappers for controller cleanup.

using RvtPortal.Spa.Application.Notifications;

namespace RvtPortal.Spa.Api.Mappers;

public static class NotificationApiMapper
{
    // Function summary: Maps a notification query application result to the existing API response contract.
    public static QueryNotificationsResponse ToQueryResponse(NotificationQueryResult result)
    {
        return new QueryNotificationsResponse
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
            State = result.State,
            IsScopedToCurrentUser = result.IsScopedToCurrentUser,
            CanClose = result.CanClose
        };
    }

    // Function summary: Maps a notification detail application model to the existing API response contract.
    public static NotificationDetailResponse ToDetailResponse(NotificationDetailModel model)
    {
        return new NotificationDetailResponse
        {
            Id = model.Id,
            MonitorId = model.MonitorId,
            DeploymentId = model.DeploymentId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            TypeOfMonitor = model.TypeOfMonitor,
            AlertType = model.AlertType,
            AlertField = model.AlertField,
            LimitOn = model.LimitOn,
            Level = model.Level,
            AveragingPeriod = model.AveragingPeriod,
            NotificationTime = model.NotificationTime,
            ClosedTime = model.ClosedTime,
            ClosedByUser = model.ClosedByUser,
            ClosedNote = model.ClosedNote,
            ContractId = model.ContractId,
            ContractNumber = model.ContractNumber,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            LimitName = model.LimitName,
            AlertStatus = model.AlertStatus,
            CanClose = model.CanClose,
            LastDataTime = model.LastDataTime,
            DeploymentStartDate = model.DeploymentStartDate,
            DeploymentEndDate = model.DeploymentEndDate,
            Lat = model.Lat,
            Lng = model.Lng,
            Location = model.Location,
            What3words = model.What3words,
            RecordingLink = model.RecordingLink,
            GraphFromUtc = model.GraphFromUtc,
            GraphToUtc = model.GraphToUtc,
            RelatedNotifications = model.RelatedNotifications.Select(ToListItem).ToList(),
            AlertLevels = model.AlertLevels.Select(ToAlertLevelItem).ToList()
        };
    }

    // Function summary: Maps one notification list model to the existing API list item contract.
    private static NotificationListItem ToListItem(NotificationListModel model)
    {
        return new NotificationListItem
        {
            Id = model.Id,
            MonitorId = model.MonitorId,
            DeploymentId = model.DeploymentId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            TypeOfMonitor = model.TypeOfMonitor,
            AlertType = model.AlertType,
            AlertField = model.AlertField,
            LimitOn = model.LimitOn,
            Level = model.Level,
            AveragingPeriod = model.AveragingPeriod,
            NotificationTime = model.NotificationTime,
            ClosedTime = model.ClosedTime,
            ClosedByUser = model.ClosedByUser,
            ClosedNote = model.ClosedNote,
            ContractId = model.ContractId,
            ContractNumber = model.ContractNumber,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            LimitName = model.LimitName,
            AlertStatus = model.AlertStatus,
            CanClose = model.CanClose
        };
    }

    // Function summary: Maps one alert-level model to the existing API alert-level item contract.
    private static AlertLevelItem ToAlertLevelItem(NotificationAlertLevelModel model)
    {
        return new AlertLevelItem
        {
            Id = model.Id,
            MonitorId = model.MonitorId,
            SerialId = model.SerialId,
            AlertField = model.AlertField,
            LimitOn = model.LimitOn,
            LimitOff = model.LimitOff,
            AlertType = model.AlertType,
            IsActive = model.IsActive,
            AveragingPeriod = model.AveragingPeriod,
            AveragingPeriodLabel = model.AveragingPeriodLabel,
            Weekdays = model.Weekdays,
            Saturdays = model.Saturdays,
            Sundays = model.Sundays,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            IsDeleted = model.IsDeleted
        };
    }
}

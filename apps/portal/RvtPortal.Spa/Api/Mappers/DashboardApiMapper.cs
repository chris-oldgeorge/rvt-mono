// File summary: Maps dashboard application-service models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added summary, map, and calendar mappers for dashboard controller cleanup.
// - 2026-07-08 pending Added breach-alert mapper for controller-to-application-service cleanup.

using RvtPortal.Spa.Application.Dashboard;

namespace RvtPortal.Spa.Api.Mappers;

public static class DashboardApiMapper
{
    // Function summary: Maps a dashboard summary application model to the existing API response contract.
    public static DashboardSummaryResponse ToSummaryResponse(DashboardSummaryModel model)
    {
        return new DashboardSummaryResponse
        {
            Role = model.Role,
            MonitorCounts = new DashboardMonitorCounts
            {
                New = model.MonitorCounts.New,
                NotUsed = model.MonitorCounts.NotUsed,
                Online = model.MonitorCounts.Online,
                Offline = model.MonitorCounts.Offline,
                Assigned = model.MonitorCounts.Assigned
            },
            OpenAlerts = model.OpenAlerts,
            OpenCautions = model.OpenCautions,
            Sites = model.Sites.Select(ToOptionItem).ToList(),
            CalendarDeployments = model.CalendarDeployments.Select(ToOptionItem).ToList(),
            RecentNotifications = model.RecentNotifications.Select(ToNotificationItem).ToList()
        };
    }

    // Function summary: Maps breach-alert application results to the existing API response contract.
    public static BreachesAlertsResponse ToBreachesAlertsResponse(DashboardBreachResult result)
    {
        return new BreachesAlertsResponse
        {
            Date = result.Date,
            Results = result.Page.Results.Select(ToBreachItem).ToList(),
            Total = result.Page.Total,
            Page = result.Page.Page,
            PageSize = result.Page.PageSize,
            TotalPages = result.Page.TotalPages,
            HasPreviousPage = result.Page.HasPreviousPage,
            HasNextPage = result.Page.HasNextPage,
            SearchText = result.Page.SearchText,
            Sort = result.Page.Sort,
            SortDir = result.Page.SortDir
        };
    }

    // Function summary: Maps a map-marker application model to the existing API response contract.
    public static MapMarkersResponse ToMapMarkersResponse(DashboardMapMarkersModel model)
    {
        return new MapMarkersResponse
        {
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            IsScopedToCurrentUser = model.IsScopedToCurrentUser,
            Markers = model.Markers.Select(marker => new MapMonitorMarker
            {
                MonitorId = marker.MonitorId,
                DeploymentId = marker.DeploymentId,
                Latitude = marker.Latitude,
                Longitude = marker.Longitude,
                TypeOfMonitor = marker.TypeOfMonitor,
                Offline = marker.Offline,
                Alert = marker.Alert,
                Caution = marker.Caution,
                SiteName = marker.SiteName,
                FleetNumber = marker.FleetNumber,
                SerialId = marker.SerialId,
                LastDataTime = marker.LastDataTime,
                What3words = marker.What3words
            }).ToList()
        };
    }

    // Function summary: Maps a calendar month application model to the existing API response contract.
    public static CalendarMonthResponse ToCalendarMonthResponse(DashboardCalendarMonthModel model)
    {
        return new CalendarMonthResponse
        {
            MonitorId = model.MonitorId,
            DeploymentId = model.DeploymentId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            TypeOfMonitor = model.TypeOfMonitor,
            Year = model.Year,
            Month = model.Month,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Unit = model.Unit,
            Deployments = model.Deployments.Select(ToOptionItem).ToList(),
            Days = model.Days.Select(day => new CalendarMonthDayItem
            {
                Date = day.Date,
                IsCurrentMonth = day.IsCurrentMonth,
                Status = day.Status,
                Average = day.Average,
                NotificationCount = day.NotificationCount
            }).ToList()
        };
    }

    // Function summary: Maps a calendar day application model to the existing API response contract.
    public static CalendarDayResponse ToCalendarDayResponse(DashboardCalendarDayModel model)
    {
        return new CalendarDayResponse
        {
            MonitorId = model.MonitorId,
            DisplayDay = model.DisplayDay,
            FleetNumber = model.FleetNumber,
            TypeOfMonitor = model.TypeOfMonitor,
            Unit = model.Unit,
            Values = model.Values.Select(value => new CalendarMeasurementItem
            {
                Label = value.Label,
                Value = value.Value
            }).ToList(),
            AlertLevels = model.AlertLevels.Select(ToAlertLevelItem).ToList(),
            Notifications = model.Notifications.Select(ToNotificationItem).ToList()
        };
    }

    // Function summary: Maps one breach-alert application model to the existing row DTO.
    private static BreachesAlertsItem ToBreachItem(DashboardBreachModel model)
    {
        return new BreachesAlertsItem
        {
            SerialId = model.SerialId,
            FleetNumber = model.FleetNumber,
            MonitorId = model.MonitorId,
            SampleTime = model.SampleTime,
            NotificationId = model.NotificationId,
            NotificationTime = model.NotificationTime,
            Xvtop = model.Xvtop,
            Yvtop = model.Yvtop,
            Zvtop = model.Zvtop
        };
    }

    // Function summary: Maps a dashboard option application model to the shared API option contract.
    private static OptionItem ToOptionItem(DashboardOptionModel model)
    {
        return new OptionItem
        {
            Value = model.Value,
            Label = model.Label
        };
    }

    // Function summary: Maps a dashboard notification application model to the existing dashboard notification contract.
    private static DashboardNotificationItem ToNotificationItem(DashboardNotificationModel model)
    {
        return new DashboardNotificationItem
        {
            Id = model.Id,
            MonitorId = model.MonitorId,
            FleetNumber = model.FleetNumber,
            SerialId = model.SerialId,
            AlertType = model.AlertType,
            AlertField = model.AlertField,
            Level = model.Level,
            NotificationTime = model.NotificationTime,
            SiteName = model.SiteName
        };
    }

    // Function summary: Maps an alert-level application model to the existing API alert-level contract.
    private static AlertLevelItem ToAlertLevelItem(DashboardAlertLevelModel model)
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

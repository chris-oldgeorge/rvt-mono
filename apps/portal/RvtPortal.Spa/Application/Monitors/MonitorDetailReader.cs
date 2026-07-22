// File summary: Builds monitor detail read models shared by controllers and MediatR handlers.
// Major updates:
// - 2026-07-08 pending Consumed monitor-picture storage through the business-layer storage port.
// - 2026-06-26 pending Scoped monitor detail notifications to selected deployment/contract ownership windows.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Extracted monitor detail assembly for the first targeted CQRS/MediatR slice.

using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Ports.Storage;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Monitors;

public interface IMonitorDetailReader
{
    // Function summary: Builds a complete monitor detail response for controller and CQRS read paths.
    Task<MonitorDetailResponse> BuildAsync(MonitorEntity monitor, Deployment? deployment, ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed class MonitorDetailReader : IMonitorDetailReader
{
    private readonly RVTDbContext domainContext;
    private readonly IMonitorDetailSummaryService summaryService;
    private readonly IMonitorPictureStorage pictureStorage;

    // Function summary: Initializes the shared monitor detail read model builder.
    public MonitorDetailReader(
        RVTDbContext domainContext,
        IMonitorDetailSummaryService summaryService,
        IMonitorPictureStorage pictureStorage)
    {
        this.domainContext = domainContext;
        this.summaryService = summaryService;
        this.pictureStorage = pictureStorage;
    }

    // Function summary: Builds a complete monitor detail response for controller and CQRS read paths.
    public async Task<MonitorDetailResponse> BuildAsync(MonitorEntity monitor, Deployment? deployment, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var ownershipWindow = deployment is null
            ? (MonitorOwnershipWindow?)null
            : MonitorOwnershipWindowResolver.ForDeployment(deployment);
        var notifications = ownershipWindow is null
            ? []
            : await domainContext.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.MonitorId == monitor.Id &&
                    notification.NotificationTime >= ownershipWindow.Value.Start &&
                    (!ownershipWindow.Value.End.HasValue || notification.NotificationTime < ownershipWindow.Value.End.Value))
                .OrderByDescending(notification => notification.NotificationTime)
                .Take(10)
                .ToListAsync(cancellationToken);
        var row = BuildListItem(monitor, deployment, notifications.Where(notification => notification.ClosedTime == null).ToList(), user);
        var alertLevels = await domainContext.RvtAlertRules
            .AsNoTracking()
            .Where(level => level.MonitorId == monitor.Id && !level.IsDeleted)
            .OrderBy(level => level.AlertType)
            .ThenBy(level => level.AlertField)
            .ToListAsync(cancellationToken);

        return new MonitorDetailResponse
        {
            Id = row.Id,
            DeploymentId = row.DeploymentId,
            FleetNumber = row.FleetNumber,
            SerialId = row.SerialId,
            Manufacturer = row.Manufacturer,
            Model = row.Model,
            FirmwareVersion = row.FirmwareVersion,
            TypeOfMonitor = row.TypeOfMonitor,
            ContractId = row.ContractId,
            ContractNumber = row.ContractNumber,
            SiteId = row.SiteId,
            SiteName = row.SiteName,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            LastDataTime = row.LastDataTime,
            IsAssigned = row.IsAssigned,
            IsOffline = row.IsOffline,
            HasAlerts = row.HasAlerts,
            HasCautions = row.HasCautions,
            CanEdit = row.CanEdit,
            CanAssign = row.CanAssign,
            CanInstallerEdit = row.CanInstallerEdit,
            ListedAtTime = monitor.ListedAtTime,
            CalibrationDate = monitor.CalibrationDate,
            CalibrationDue = monitor.CalibrationDue,
            Lat = deployment?.Lat,
            Lng = deployment?.Lng,
            Location = deployment?.Location,
            What3words = deployment?.What3words,
            PictureLink = pictureStorage.BuildProtectedLink(monitor.Id, deployment?.PictureLink),
            StatusLabel = MonitorStatusLabel(row),
            MonitorNotes = "No notes for this monitor",
            LatestReading = await summaryService.BuildLatestReadingAsync(deployment, notifications.FirstOrDefault()),
            LatestAverage = await summaryService.BuildLatestAverageAsync(deployment),
            LatestBattery = await summaryService.BuildLatestBatteryAsync(monitor),
            DeploymentSummary = summaryService.BuildDeploymentSummary(deployment),
            AlertLevels = alertLevels.Select(BuildAlertLevelItem).ToList(),
            RecentNotifications = notifications.Select(BuildNotificationItem).ToList()
        };
    }

    // Function summary: Builds the monitor list fields embedded inside monitor detail responses.
    private static MonitorListItem BuildListItem(MonitorEntity monitor, Deployment? deployment, List<Notification> notifications, ClaimsPrincipal user)
    {
        var lastDataTime = LastDataTime(monitor);
        var contract = deployment?.Contract;
        return new MonitorListItem
        {
            Id = monitor.Id,
            DeploymentId = deployment?.Id,
            FleetNumber = monitor.FleetNr,
            SerialId = monitor.SerialId,
            Manufacturer = monitor.Manufacturer,
            Model = monitor.Model,
            FirmwareVersion = monitor.FirmwareVersion,
            TypeOfMonitor = monitor.TypeOfMonitor.ToString(),
            ContractId = contract?.Id,
            ContractNumber = contract?.ContractNumber,
            SiteId = contract?.SiteiD,
            SiteName = contract?.Site?.SiteName,
            CompanyId = contract?.CompanyId,
            CompanyName = contract?.Company?.CompanyName,
            StartDate = deployment?.StartDate,
            EndDate = deployment?.EndDate,
            LastDataTime = lastDataTime,
            IsAssigned = deployment != null,
            IsOffline = IsOffline(lastDataTime),
            HasAlerts = notifications.Any(notification => notification.AlertType == AlertTypeEnum.Alert),
            HasCautions = notifications.Any(notification => notification.AlertType == AlertTypeEnum.Caution),
            CanEdit = IsAdmin(user),
            CanAssign = IsAdmin(user) && deployment == null && !string.IsNullOrWhiteSpace(monitor.FleetNr),
            CanInstallerEdit = (IsAdmin(user) || user.IsInRole(RoleNames.RVTInstaller)) && deployment != null
        };
    }

    // Function summary: Builds alert level DTOs for monitor detail responses.
    private static MonitorAlertLevelItem BuildAlertLevelItem(Alertlevel level)
    {
        return new MonitorAlertLevelItem
        {
            Id = level.Id,
            SerialId = level.SerialId,
            AlertField = level.AlertField,
            LimitOn = level.LimitOn,
            LimitOff = level.LimitOff,
            AlertType = level.AlertType.ToString(),
            IsActive = level.IsActive,
            AveragingPeriod = level.AveragingPeriod,
            Weekdays = level.Weekdays,
            Saturdays = level.Saturdays,
            Sundays = level.Sundays,
            StartTime = FormatTime(level.StartTime),
            EndTime = FormatTime(level.EndTime),
            IsDeleted = level.IsDeleted
        };
    }

    // Function summary: Builds notification DTOs for monitor detail responses.
    private static MonitorNotificationItem BuildNotificationItem(Notification notification)
    {
        return new MonitorNotificationItem
        {
            Id = notification.Id,
            MonitorId = notification.MonitorId,
            NotificationTime = notification.NotificationTime,
            AlertType = notification.AlertType.ToString(),
            AlertField = notification.AlertField,
            LimitOn = notification.LimitOn,
            Level = notification.Level,
            ClosedTime = notification.ClosedTime
        };
    }

    // Function summary: Converts monitor state flags into a display label.
    private static string MonitorStatusLabel(MonitorListItem row)
    {
        if (row.IsOffline)
        {
            return "Offline";
        }

        return row.IsAssigned ? "Online" : "Not deployed";
    }

    // Function summary: Evaluates whether the current user has RVT administrator privileges.
    private static bool IsAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.RVTMasterAdmin) || user.IsInRole(RoleNames.RVTAdmin);
    }

    // Function summary: Finds the newest monitor data timestamp used for online/offline status.
    private static DateTime? LastDataTime(MonitorEntity monitor)
    {
        return new[] { monitor.LastDataTime15Min, monitor.LastDataTime1Min, monitor.LastDataTime1Hour, monitor.LastDataTime24Hour }
            .Where(value => value.HasValue)
            .Max();
    }

    // Function summary: Evaluates whether a monitor should be considered offline.
    private static bool IsOffline(DateTime? lastDataTime)
    {
        return !lastDataTime.HasValue || lastDataTime.Value < DateTime.UtcNow.AddHours(-1);
    }

    // Function summary: Formats optional time values for API clients.
    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}

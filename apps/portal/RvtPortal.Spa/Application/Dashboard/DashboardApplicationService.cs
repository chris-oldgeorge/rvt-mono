// File summary: Provides database-backed dashboard overview, map, and calendar workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved dashboard summary, map marker, and calendar query logic out of the API controller.
// - 2026-07-22 pending Enforced inclusive active assignment windows across dashboard visibility.

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Application.Sites;
using RvtPortal.Spa.Data;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Dashboard;

public interface IDashboardApplicationService
{
    // Function summary: Returns the role-scoped dashboard summary model.
    Task<DashboardSummaryModel> GetSummaryAsync(DashboardActor actor, CancellationToken cancellationToken);

    // Function summary: Returns role-scoped map markers, or null when the requested site is hidden or missing.
    Task<DashboardMapMarkersModel?> GetMapMarkersAsync(DashboardActor actor, Guid? siteId, CancellationToken cancellationToken);

    // Function summary: Returns a role-scoped calendar month, or null when the deployment is hidden or missing.
    Task<DashboardCalendarMonthModel?> GetCalendarMonthAsync(
        DashboardActor actor,
        Guid deploymentId,
        DateTime selectedMonth,
        CancellationToken cancellationToken);

    // Function summary: Returns a role-scoped calendar day, or null when the monitor is hidden or missing.
    Task<DashboardCalendarDayModel?> GetCalendarDayAsync(
        DashboardActor actor,
        Guid monitorId,
        DateTime displayDay,
        CancellationToken cancellationToken);
}

public sealed record DashboardActor(
    Guid? UserId,
    bool IsMasterAdmin,
    bool IsRvtAdmin,
    bool IsInstallerRole,
    bool IsCompanyUserRole)
{
    public bool IsAdmin => IsMasterAdmin || IsRvtAdmin;
    public bool IsInstaller => IsInstallerRole && !IsAdmin;
    public bool IsScopedCompanyUser => IsCompanyUserRole && !IsAdmin;

    public string Role => IsMasterAdmin
        ? RoleNames.RVTMasterAdmin
        : IsRvtAdmin
            ? RoleNames.RVTAdmin
            : IsInstaller
                ? RoleNames.RVTInstaller
                : RoleNames.CompanyUser;

    // Function summary: Converts the shared portal user context into dashboard-specific role facts.
    public static DashboardActor FromPortalUser(
        PortalUserContext user,
        bool isMasterAdmin,
        bool isRvtAdmin)
    {
        var isRvtAdminOrFallback = isRvtAdmin || (user.IsAdmin && !isMasterAdmin);
        return new DashboardActor(user.UserId, isMasterAdmin, isRvtAdminOrFallback, user.IsInstaller, user.IsCompanyUser);
    }
}

public sealed class DashboardSummaryModel
{
    public string Role { get; init; } = "";
    public DashboardMonitorCountsModel MonitorCounts { get; init; } = new();
    public int OpenAlerts { get; init; }
    public int OpenCautions { get; init; }
    public List<DashboardOptionModel> Sites { get; init; } = [];
    public List<DashboardOptionModel> CalendarDeployments { get; init; } = [];
    public List<DashboardNotificationModel> RecentNotifications { get; init; } = [];
}

public sealed class DashboardMonitorCountsModel
{
    public int New { get; init; }
    public int NotUsed { get; init; }
    public int Online { get; init; }
    public int Offline { get; init; }
    public int Assigned { get; init; }
}

public sealed class DashboardOptionModel
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class DashboardNotificationModel
{
    public Guid Id { get; init; }
    public Guid MonitorId { get; init; }
    public string? FleetNumber { get; init; }
    public string SerialId { get; init; } = "";
    public string AlertType { get; init; } = "";
    public string AlertField { get; init; } = "";
    public double Level { get; init; }
    public DateTime NotificationTime { get; init; }
    public string? SiteName { get; init; }
}

public sealed class DashboardMapMarkersModel
{
    public Guid? SiteId { get; init; }
    public string? SiteName { get; init; }
    public bool IsScopedToCurrentUser { get; init; }
    public List<DashboardMapMonitorMarkerModel> Markers { get; init; } = [];
}

public sealed class DashboardMapMonitorMarkerModel
{
    public Guid MonitorId { get; init; }
    public Guid DeploymentId { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string TypeOfMonitor { get; init; } = "";
    public bool Offline { get; init; }
    public bool Alert { get; init; }
    public bool Caution { get; init; }
    public string? SiteName { get; init; }
    public string? FleetNumber { get; init; }
    public string SerialId { get; init; } = "";
    public DateTime? LastDataTime { get; init; }
    public string? What3words { get; init; }
}

public sealed class DashboardCalendarMonthModel
{
    public Guid MonitorId { get; init; }
    public Guid DeploymentId { get; init; }
    public string FleetNumber { get; init; } = "";
    public string SerialId { get; init; } = "";
    public string TypeOfMonitor { get; init; } = "";
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Unit { get; init; } = "";
    public List<DashboardOptionModel> Deployments { get; init; } = [];
    public List<DashboardCalendarMonthDayModel> Days { get; init; } = [];
}

public sealed class DashboardCalendarMonthDayModel
{
    public DateTime Date { get; init; }
    public bool IsCurrentMonth { get; init; }
    public string Status { get; init; } = "";
    public double? Average { get; init; }
    public int NotificationCount { get; init; }
}

public sealed class DashboardCalendarDayModel
{
    public Guid MonitorId { get; init; }
    public DateTime DisplayDay { get; init; }
    public string FleetNumber { get; init; } = "";
    public string TypeOfMonitor { get; init; } = "";
    public string Unit { get; init; } = "";
    public List<DashboardCalendarMeasurementModel> Values { get; init; } = [];
    public List<DashboardAlertLevelModel> AlertLevels { get; init; } = [];
    public List<DashboardNotificationModel> Notifications { get; init; } = [];
}

public sealed class DashboardCalendarMeasurementModel
{
    public string Label { get; init; } = "";
    public double Value { get; init; }
}

public sealed class DashboardAlertLevelModel
{
    public Guid Id { get; init; }
    public Guid MonitorId { get; init; }
    public string SerialId { get; init; } = "";
    public string AlertField { get; init; } = "";
    public double LimitOn { get; init; }
    public double LimitOff { get; init; }
    public string AlertType { get; init; } = "";
    public bool IsActive { get; init; }
    public int AveragingPeriod { get; init; }
    public string AveragingPeriodLabel { get; init; } = "";
    public bool Weekdays { get; init; }
    public bool Saturdays { get; init; }
    public bool Sundays { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class DashboardApplicationService : IDashboardApplicationService
{
    private readonly RVTDbContext domainContext;
    private readonly TimeProvider timeProvider;

    // Per-request memo for the signed-in user's visible sites; see VisibleSiteIdsAsync.
    private HashSet<Guid>? visibleSiteIdsCache;
    private Guid? visibleSiteIdsCacheUserId;

    // Function summary: Initializes this application service with the domain read context.
    public DashboardApplicationService(RVTDbContext domainContext, TimeProvider timeProvider)
    {
        this.domainContext = domainContext;
        this.timeProvider = timeProvider;
    }

    // Function summary: Returns dashboard counts, options, and recent open notifications for the caller's role scope.
    public async Task<DashboardSummaryModel> GetSummaryAsync(
        DashboardActor actor,
        CancellationToken cancellationToken)
    {
        var rows = await VisibleMonitorRowsAsync(actor, cancellationToken);
        var openNotifications = await BuildVisibleNotificationsAsync(rows, openOnly: true, cancellationToken);

        return new DashboardSummaryModel
        {
            Role = actor.Role,
            MonitorCounts = new DashboardMonitorCountsModel
            {
                New = rows.Count(row => string.IsNullOrWhiteSpace(row.FleetNumber)),
                NotUsed = rows.Count(row => !string.IsNullOrWhiteSpace(row.FleetNumber) && !row.IsAssigned),
                Online = rows.Count(row => !string.IsNullOrWhiteSpace(row.FleetNumber) && row.IsAssigned && !row.Offline),
                Offline = rows.Count(row => !string.IsNullOrWhiteSpace(row.FleetNumber) && row.IsAssigned && row.Offline),
                Assigned = rows.Count(row => row.IsAssigned)
            },
            OpenAlerts = openNotifications.Count(notification => notification.AlertType == AlertTypeEnum.Alert),
            OpenCautions = openNotifications.Count(notification => notification.AlertType == AlertTypeEnum.Caution),
            Sites = await BuildVisibleSiteOptionsAsync(actor, cancellationToken),
            CalendarDeployments = BuildCalendarDeploymentOptions(rows),
            RecentNotifications = openNotifications
                .OrderByDescending(notification => notification.NotificationTime)
                .Take(5)
                .Select(BuildNotificationModel)
                .ToList()
        };
    }

    // Function summary: Returns role-scoped map markers for all visible sites or one requested site.
    public async Task<DashboardMapMarkersModel?> GetMapMarkersAsync(
        DashboardActor actor,
        Guid? siteId,
        CancellationToken cancellationToken)
    {
        var rows = await VisibleMonitorRowsAsync(actor, cancellationToken);
        DashboardOptionModel? selectedSite = null;
        if (siteId.HasValue)
        {
            selectedSite = (await BuildVisibleSiteOptionsAsync(actor, cancellationToken))
                .FirstOrDefault(site => string.Equals(site.Value, siteId.Value.ToString(), StringComparison.OrdinalIgnoreCase));
            if (selectedSite is null)
            {
                return null;
            }

            rows = rows.Where(row => row.SiteId == siteId.Value).ToList();
        }

        return new DashboardMapMarkersModel
        {
            SiteId = siteId,
            SiteName = selectedSite?.Label,
            IsScopedToCurrentUser = actor.IsScopedCompanyUser,
            Markers = rows
                .Where(row => row.DeploymentId.HasValue && row.Lat.HasValue && row.Lng.HasValue && !IsNullIsland(row.Lat.Value, row.Lng.Value))
                .OrderBy(row => row.SiteName)
                .ThenBy(row => row.FleetNumber ?? row.SerialId)
                .Select(row => new DashboardMapMonitorMarkerModel
                {
                    MonitorId = row.MonitorId,
                    DeploymentId = row.DeploymentId!.Value,
                    Latitude = row.Lat!.Value,
                    Longitude = row.Lng!.Value,
                    TypeOfMonitor = row.TypeOfMonitor,
                    Offline = row.Offline,
                    Alert = row.Alert,
                    Caution = row.Caution,
                    SiteName = row.SiteName,
                    FleetNumber = row.FleetNumber,
                    SerialId = row.SerialId,
                    LastDataTime = row.LastDataTime,
                    What3words = row.What3words
                })
                .ToList()
        };
    }

    // Function summary: Returns one role-scoped calendar month for a visible deployment.
    public async Task<DashboardCalendarMonthModel?> GetCalendarMonthAsync(
        DashboardActor actor,
        Guid deploymentId,
        DateTime selectedMonth,
        CancellationToken cancellationToken)
    {
        var rows = await VisibleMonitorRowsAsync(actor, cancellationToken);
        if (!rows.Any(item => item.DeploymentId == deploymentId))
        {
            return null;
        }

        var deployment = await FindDeploymentAsync(deploymentId, cancellationToken);
        if (deployment is null)
        {
            return null;
        }

        selectedMonth = ClampCalendarMonth(deployment, selectedMonth);
        var monthEnd = selectedMonth.AddMonths(1).AddDays(-1);
        var calendarStart = StartOfCalendar(selectedMonth);
        var calendarEnd = EndOfCalendar(monthEnd);
        var window = MonitorOwnershipWindowResolver.ForDeployment(deployment);
        // NotificationTime is timestamptz, so these query bounds must be Kind=Utc or Npgsql rejects them. They are
        // already the intended UTC instants: the ownership window is read from timestamptz columns (Kind=Utc), and
        // the calendar is a UTC-day view - the notifications below are grouped by NotificationTime.Date, the UTC
        // date. calendarStart/calendarEnd inherit the controller's DateTimeKind, though, so pin the chosen bound to
        // Utc without shifting its ticks.
        //
        // DESIGN DECISION (deferred): this calendar buckets notifications by their UTC calendar day. Presenting it
        // in the configured local time zone instead is a deliberate product choice, NOT a bug - it would move
        // notifications near midnight into a different cell for non-UTC zones. Making that switch means converting
        // the grid bounds via LocalToUtc AND grouping by UtcToLocal(NotificationTime).Date, with matching updates
        // to DashboardMapCalendarTests. Left as UTC-day here to keep this a Kind fix, not a behaviour change.
        var notificationStart = DateTime.SpecifyKind(
            window.Start > calendarStart ? window.Start : calendarStart,
            DateTimeKind.Utc);
        var notificationEnd = DateTime.SpecifyKind(
            window.End.HasValue && window.End.Value < calendarEnd.AddDays(1)
                ? window.End.Value
                : calendarEnd.AddDays(1),
            DateTimeKind.Utc);
        var notifications = notificationEnd <= notificationStart
            ? []
            : await domainContext.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.MonitorId == deployment.MonitorId &&
                    notification.NotificationTime >= notificationStart &&
                    notification.NotificationTime < notificationEnd)
                .ToListAsync(cancellationToken);
        var notificationsByDate = notifications
            .GroupBy(notification => notification.NotificationTime.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        return new DashboardCalendarMonthModel
        {
            MonitorId = deployment.MonitorId,
            DeploymentId = deployment.Id,
            FleetNumber = deployment.Monitor.FleetNr ?? "",
            SerialId = deployment.Monitor.SerialId,
            TypeOfMonitor = deployment.Monitor.TypeOfMonitor.ToString(),
            Year = selectedMonth.Year,
            Month = selectedMonth.Month,
            StartDate = deployment.StartDate.Date,
            EndDate = CalendarMaxDate(deployment).Date,
            Unit = Unit(deployment.Monitor.TypeOfMonitor),
            Deployments = BuildCalendarDeploymentOptions(rows),
            Days = CalendarDates(calendarStart, calendarEnd)
                .Select(day =>
                {
                    notificationsByDate.TryGetValue(day.Date, out var dayNotifications);
                    dayNotifications ??= [];
                    return new DashboardCalendarMonthDayModel
                    {
                        Date = day,
                        IsCurrentMonth = day.Month == selectedMonth.Month && day.Year == selectedMonth.Year,
                        Status = CalendarStatus(dayNotifications),
                        Average = dayNotifications.Count == 0 ? null : dayNotifications.Average(notification => notification.Level),
                        NotificationCount = dayNotifications.Count
                    };
                })
                .ToList()
        };
    }

    // Function summary: Returns notification values and alert levels for one visible monitor calendar day.
    public async Task<DashboardCalendarDayModel?> GetCalendarDayAsync(
        DashboardActor actor,
        Guid monitorId,
        DateTime displayDay,
        CancellationToken cancellationToken)
    {
        var monitor = await domainContext.MonitorsList
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == monitorId, cancellationToken);
        if (monitor is null)
        {
            return null;
        }

        var nextDay = displayDay.AddDays(1);
        var deployment = await FindDeploymentForMonitorDateAsync(monitor.Id, displayDay, nextDay, cancellationToken);
        if (deployment is null || !await CanReadDeploymentAsync(actor, deployment, cancellationToken))
        {
            return null;
        }

        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Include(notification => notification.Monitor)
            .Where(notification =>
                notification.MonitorId == monitor.Id &&
                notification.NotificationTime >= displayDay &&
                notification.NotificationTime < nextDay)
            .OrderByDescending(notification => notification.NotificationTime)
            .ToListAsync(cancellationToken);
        var alertLevels = await domainContext.RvtAlertRules
            .AsNoTracking()
            .Where(level => level.MonitorId == monitor.Id && !level.IsDeleted)
            .OrderBy(level => level.AlertField)
            .ThenBy(level => level.AlertType)
            .ToListAsync(cancellationToken);

        return new DashboardCalendarDayModel
        {
            MonitorId = monitor.Id,
            DisplayDay = displayDay,
            FleetNumber = monitor.FleetNr ?? "",
            TypeOfMonitor = monitor.TypeOfMonitor.ToString(),
            Unit = Unit(monitor.TypeOfMonitor),
            Values = notifications
                .GroupBy(notification => notification.AlertField)
                .OrderBy(group => group.Key)
                .Select(group => new DashboardCalendarMeasurementModel
                {
                    Label = group.Key,
                    Value = group.Max(notification => notification.Level)
                })
                .ToList(),
            AlertLevels = alertLevels.Select(level => BuildAlertLevelModel(level, monitor.TypeOfMonitor)).ToList(),
            Notifications = notifications.Select(BuildNotificationModel).ToList()
        };
    }

    // Function summary: Builds and filters monitor dashboard rows for the caller's role.
    private async Task<List<DashboardMonitorRow>> VisibleMonitorRowsAsync(
        DashboardActor actor,
        CancellationToken cancellationToken)
    {
        var rows = await BuildMonitorRowsAsync(cancellationToken);
        return await ApplyRoleVisibilityAsync(actor, rows, cancellationToken);
    }

    // Function summary: Builds monitor rows with current deployment, freshness, and open-notification state.
    private async Task<List<DashboardMonitorRow>> BuildMonitorRowsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monitors = await domainContext.MonitorsList.AsNoTracking().ToListAsync(cancellationToken);

        // The ownership window is applied in SQL now, so only deployments that actually own their monitor's
        // data right now come back - previously every open deployment was loaded and then filtered in memory.
        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .Where(deployment => deployment.EndDate == null)
            .Where(MonitorOwnershipWindowResolver.OwnsAt(now))
            .ToListAsync(cancellationToken);
        var currentByMonitor = deployments
            .GroupBy(deployment => deployment.MonitorId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(deployment => deployment.StartDate).First());

        // Only monitors with a current deployment can contribute notifications: the lookup below drops anything
        // else. Asking for just those monitors avoids loading open notifications that would be discarded.
        var deployedMonitorIds = currentByMonitor.Keys.ToList();
        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Where(notification => deployedMonitorIds.Contains(notification.MonitorId) && notification.ClosedTime == null)
            .ToListAsync(cancellationToken);
        var notificationLookup = notifications
            .Where(notification =>
                currentByMonitor.TryGetValue(notification.MonitorId, out var deployment) &&
                MonitorOwnershipWindowResolver.ForDeployment(deployment).Contains(notification.NotificationTime))
            .GroupBy(notification => notification.MonitorId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return monitors.Select(monitor =>
        {
            currentByMonitor.TryGetValue(monitor.Id, out var deployment);
            notificationLookup.TryGetValue(monitor.Id, out var monitorNotifications);
            return BuildMonitorRow(monitor, deployment, monitorNotifications ?? [], now);
        }).ToList();
    }

    // Function summary: Applies the current user's dashboard visibility rules to monitor rows.
    private async Task<List<DashboardMonitorRow>> ApplyRoleVisibilityAsync(
        DashboardActor actor,
        List<DashboardMonitorRow> rows,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return rows;
        }

        if (actor.IsInstaller)
        {
            return rows.Where(row => row.IsAssigned).ToList();
        }

        var visibleSiteIds = await VisibleSiteIdsAsync(actor, cancellationToken);
        return rows.Where(row => row.SiteId.HasValue && visibleSiteIds.Contains(row.SiteId.Value)).ToList();
    }

    // Function summary: Builds notification entities visible through the current monitor rows.
    private async Task<List<Notification>> BuildVisibleNotificationsAsync(
        IReadOnlyCollection<DashboardMonitorRow> rows,
        bool openOnly,
        CancellationToken cancellationToken)
    {
        var monitorIds = rows.Select(row => row.MonitorId).ToHashSet();
        if (monitorIds.Count == 0)
        {
            return [];
        }

        var deploymentIds = rows
            .Where(row => row.DeploymentId.HasValue)
            .Select(row => row.DeploymentId!.Value)
            .ToList();
        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .Where(deployment => deploymentIds.Contains(deployment.Id))
            .ToListAsync(cancellationToken);
        var currentByMonitor = deployments.ToDictionary(deployment => deployment.MonitorId);

        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Include(notification => notification.Monitor)
            .Where(notification =>
                monitorIds.Contains(notification.MonitorId) &&
                (!openOnly || notification.ClosedTime == null))
            .ToListAsync(cancellationToken);
        return notifications
            .Where(notification =>
                currentByMonitor.TryGetValue(notification.MonitorId, out var deployment) &&
                MonitorOwnershipWindowResolver.ForDeployment(deployment).Contains(notification.NotificationTime))
            .ToList();
    }

    // Function summary: Builds role-scoped site options for dashboard filters.
    private async Task<List<DashboardOptionModel>> BuildVisibleSiteOptionsAsync(
        DashboardActor actor,
        CancellationToken cancellationToken)
    {
        var sites = domainContext.Sites
            .AsNoTracking()
            .Where(site => !site.Archived);
        if (actor.IsScopedCompanyUser)
        {
            var visibleSiteIds = await VisibleSiteIdsAsync(actor, cancellationToken);
            sites = sites.Where(site => visibleSiteIds.Contains(site.Id));
        }
        else if (actor.IsInstaller)
        {
            var siteIds = await domainContext.Deployments
                .AsNoTracking()
                .Where(deployment => deployment.EndDate == null && deployment.Contract.SiteiD.HasValue)
                .Select(deployment => deployment.Contract.SiteiD!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            sites = sites.Where(site => siteIds.Contains(site.Id));
        }

        return await sites
            .OrderBy(site => site.SiteName)
            .Select(site => new DashboardOptionModel { Value = site.Id.ToString(), Label = site.SiteName })
            .ToListAsync(cancellationToken);
    }

    // Function summary: Builds calendar deployment option labels from visible monitor rows.
    private static List<DashboardOptionModel> BuildCalendarDeploymentOptions(IEnumerable<DashboardMonitorRow> rows)
    {
        return rows
            .Where(row => row.DeploymentId.HasValue)
            .OrderBy(row => row.FleetNumber ?? row.SerialId)
            .Select(row => new DashboardOptionModel
            {
                Value = row.DeploymentId!.Value.ToString(),
                Label = string.IsNullOrWhiteSpace(row.SiteName)
                    ? $"{row.FleetNumber ?? row.SerialId}"
                    : $"{row.FleetNumber ?? row.SerialId} - {row.SiteName}"
            })
            .ToList();
    }

    // Function summary: Converts one monitor plus optional current deployment into a dashboard row.
    private static DashboardMonitorRow BuildMonitorRow(
        MonitorEntity monitor,
        Deployment? deployment,
        List<Notification> notifications,
        DateTime now)
    {
        var lastDataTime = LastDataTime(monitor);
        return new DashboardMonitorRow
        {
            MonitorId = monitor.Id,
            DeploymentId = deployment?.Id,
            FleetNumber = monitor.FleetNr,
            SerialId = monitor.SerialId,
            TypeOfMonitor = monitor.TypeOfMonitor.ToString(),
            SiteId = deployment?.Contract?.SiteiD,
            SiteName = deployment?.Contract?.Site?.SiteName,
            CompanyId = deployment?.Contract?.CompanyId,
            CompanyName = deployment?.Contract?.Company?.CompanyName,
            Lat = deployment?.Lat,
            Lng = deployment?.Lng,
            Location = deployment?.Location,
            What3words = deployment?.What3words,
            LastDataTime = lastDataTime,
            IsAssigned = deployment != null,
            Offline = IsOffline(lastDataTime, now),
            Alert = notifications.Any(notification => notification.AlertType == AlertTypeEnum.Alert),
            Caution = notifications.Any(notification => notification.AlertType == AlertTypeEnum.Caution)
        };
    }

    // Function summary: Maps one notification entity to the dashboard application model.
    private static DashboardNotificationModel BuildNotificationModel(Notification notification)
    {
        return new DashboardNotificationModel
        {
            Id = notification.Id,
            MonitorId = notification.MonitorId,
            FleetNumber = notification.Monitor?.FleetNr,
            SerialId = notification.Monitor?.SerialId ?? "",
            AlertType = notification.AlertType.ToString(),
            AlertField = notification.AlertField,
            Level = notification.Level,
            NotificationTime = notification.NotificationTime
        };
    }

    // Function summary: Maps one alert-level entity to the dashboard application model.
    private static DashboardAlertLevelModel BuildAlertLevelModel(Alertlevel level, MonitorTypeEnum? monitorType)
    {
        return new DashboardAlertLevelModel
        {
            Id = level.Id,
            MonitorId = level.MonitorId,
            SerialId = level.SerialId,
            AlertField = level.AlertField,
            LimitOn = level.LimitOn,
            LimitOff = level.LimitOff,
            AlertType = level.AlertType.ToString(),
            IsActive = level.IsActive,
            AveragingPeriod = level.AveragingPeriod,
            AveragingPeriodLabel = AveragingPeriodLabel(level, monitorType),
            Weekdays = level.Weekdays,
            Saturdays = level.Saturdays,
            Sundays = level.Sundays,
            StartTime = FormatTime(level.StartTime),
            EndTime = FormatTime(level.EndTime),
            IsDeleted = level.IsDeleted
        };
    }

    // Function summary: Finds site ids visible to the current company user.
    private async Task<HashSet<Guid>> VisibleSiteIdsAsync(
        DashboardActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.UserId.HasValue)
        {
            return [];
        }

        // A single dashboard request asks for the visible sites up to three times (role filtering, site
        // options, deployment authorization). This service is request-scoped and the actor is the signed-in
        // user, so the answer is memoized for the request rather than re-queried each time.
        if (visibleSiteIdsCache is not null && visibleSiteIdsCacheUserId == actor.UserId.Value)
        {
            return visibleSiteIdsCache;
        }

        var siteIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(ActiveSiteAssignment.ForUser(actor.UserId.Value, timeProvider.GetUtcNow().UtcDateTime))
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);

        visibleSiteIdsCacheUserId = actor.UserId.Value;
        visibleSiteIdsCache = siteIds.ToHashSet();
        return visibleSiteIdsCache;
    }

    // Function summary: Finds the deployment required for a calendar month view.
    private Task<Deployment?> FindDeploymentAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        return domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Monitor)
            .Include(deployment => deployment.Contract)
            .ThenInclude(contract => contract.Site)
            .FirstOrDefaultAsync(deployment => deployment.Id == deploymentId, cancellationToken);
    }

    // Function summary: Finds the deployment whose ownership window overlaps the requested calendar day.
    private async Task<Deployment?> FindDeploymentForMonitorDateAsync(
        Guid monitorId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var deployments = await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .Where(deployment => deployment.MonitorId == monitorId)
            .ToListAsync(cancellationToken);

        return deployments
            .Where(deployment => MonitorOwnershipWindowResolver.ForDeployment(deployment).Intersects(from, to))
            .OrderByDescending(deployment => MonitorOwnershipWindowResolver.ForDeployment(deployment).Start)
            .FirstOrDefault();
    }

    // Function summary: Evaluates whether the dashboard actor can read deployment-scoped data.
    private async Task<bool> CanReadDeploymentAsync(
        DashboardActor actor,
        Deployment deployment,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        var siteId = deployment.Contract?.SiteiD;
        if (!siteId.HasValue || !actor.IsScopedCompanyUser)
        {
            return false;
        }

        return (await VisibleSiteIdsAsync(actor, cancellationToken)).Contains(siteId.Value);
    }

    // Function summary: Finds the latest monitor data timestamp across supported intervals.
    private static DateTime? LastDataTime(MonitorEntity monitor)
    {
        return new[] { monitor.LastDataTime15Min, monitor.LastDataTime1Min, monitor.LastDataTime1Hour, monitor.LastDataTime24Hour }
            .Where(value => value.HasValue)
            .Max();
    }

    // Function summary: Evaluates whether a monitor is considered offline for dashboard purposes.
    private static bool IsOffline(DateTime? lastDataTime, DateTime now)
    {
        return !lastDataTime.HasValue || lastDataTime.Value < now.AddHours(-1);
    }

    // Function summary: Clamps a calendar month into the deployment's valid display range.
    private static DateTime ClampCalendarMonth(Deployment deployment, DateTime selectedMonth)
    {
        var firstDeploymentMonth = MonthStart(deployment.StartDate);
        var lastDate = CalendarMaxDate(deployment);
        var lastDeploymentMonth = MonthStart(lastDate);
        if (selectedMonth < firstDeploymentMonth)
        {
            return firstDeploymentMonth;
        }
        if (selectedMonth > lastDeploymentMonth)
        {
            return lastDeploymentMonth;
        }

        return selectedMonth;
    }

    // Function summary: Returns the first day of the supplied month.
    private static DateTime MonthStart(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind);
    }

    // Function summary: Returns the maximum date a deployment can show on the dashboard calendar.
    private static DateTime CalendarMaxDate(Deployment deployment)
    {
        var today = DateTime.Today;
        if (deployment.EndDate.HasValue && deployment.EndDate.Value.Date < today)
        {
            return deployment.EndDate.Value.Date;
        }

        return today;
    }

    // Function summary: Returns the Monday-aligned calendar grid start.
    private static DateTime StartOfCalendar(DateTime startOfMonth)
    {
        var offset = ((int)startOfMonth.DayOfWeek + 6) % 7;
        return startOfMonth.Date.AddDays(-offset);
    }

    // Function summary: Returns the Sunday-aligned calendar grid end.
    private static DateTime EndOfCalendar(DateTime endOfMonth)
    {
        var offset = 6 - (((int)endOfMonth.DayOfWeek + 6) % 7);
        return endOfMonth.Date.AddDays(offset);
    }

    // Function summary: Enumerates all dates in a calendar range.
    private static IEnumerable<DateTime> CalendarDates(DateTime start, DateTime end)
    {
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    // Function summary: Calculates a display status from a day's notifications.
    private static string CalendarStatus(IReadOnlyCollection<Notification> notifications)
    {
        if (notifications.Any(notification => notification.AlertType == AlertTypeEnum.Alert))
        {
            return "Alert";
        }
        if (notifications.Any(notification => notification.AlertType == AlertTypeEnum.Caution))
        {
            return "Caution";
        }

        return "Ok";
    }

    // Function summary: Returns the display unit for a monitor type.
    private static string Unit(MonitorTypeEnum type)
    {
        return type switch
        {
            MonitorTypeEnum.Dust => "Βµg/mΒ³",
            MonitorTypeEnum.Noise => "dB",
            MonitorTypeEnum.Vibration => "mm/s",
            _ => ""
        };
    }

    // Function summary: Determines whether coordinates represent the invalid null-island location.
    private static bool IsNullIsland(double lat, double lng)
    {
        return Math.Abs(lat) < 0.000001 && Math.Abs(lng) < 0.000001;
    }

    // Function summary: Formats optional alert-level times for existing API contracts.
    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    // Function summary: Resolves an alert-level averaging-period label for the monitor type.
    private static string AveragingPeriodLabel(Alertlevel level, MonitorTypeEnum? monitorType)
    {
        if (monitorType == MonitorTypeEnum.Vibration)
        {
            return "";
        }

        if (Enum.IsDefined(typeof(AveragingPeriodsDustEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsDustEnum)level.AveragingPeriod).ToString());
        }
        if (Enum.IsDefined(typeof(AveragingPeriodsNoiseEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsNoiseEnum)level.AveragingPeriod).ToString());
        }
        if (Enum.IsDefined(typeof(AveragingPeriodsVibrationEnum), level.AveragingPeriod))
        {
            return EnumLabel(((AveragingPeriodsVibrationEnum)level.AveragingPeriod).ToString());
        }

        return level.Monitor?.TypeOfMonitor switch
        {
            MonitorTypeEnum.Dust => Enum.IsDefined(typeof(AveragingPeriodsDustEnum), level.AveragingPeriod)
                ? EnumLabel(((AveragingPeriodsDustEnum)level.AveragingPeriod).ToString())
                : level.AveragingPeriod.ToString(CultureInfo.InvariantCulture),
            MonitorTypeEnum.Noise => Enum.IsDefined(typeof(AveragingPeriodsNoiseEnum), level.AveragingPeriod)
                ? EnumLabel(((AveragingPeriodsNoiseEnum)level.AveragingPeriod).ToString())
                : level.AveragingPeriod.ToString(CultureInfo.InvariantCulture),
            _ => level.AveragingPeriod.ToString(CultureInfo.InvariantCulture)
        };
    }

    // Function summary: Converts enum member names into user-facing labels.
    private static string EnumLabel(string value)
    {
        return value.TrimStart('_').Replace('_', ' ');
    }

    private sealed class DashboardMonitorRow
    {
        public Guid MonitorId { get; init; }
        public Guid? DeploymentId { get; init; }
        public string? FleetNumber { get; init; }
        public string SerialId { get; init; } = "";
        public string TypeOfMonitor { get; init; } = "";
        public Guid? SiteId { get; init; }
        public string? SiteName { get; init; }
        public Guid? CompanyId { get; init; }
        public string? CompanyName { get; init; }
        public double? Lat { get; init; }
        public double? Lng { get; init; }
        public string? Location { get; init; }
        public string? What3words { get; init; }
        public DateTime? LastDataTime { get; init; }
        public bool IsAssigned { get; init; }
        public bool Offline { get; init; }
        public bool Alert { get; init; }
        public bool Caution { get; init; }
    }
}

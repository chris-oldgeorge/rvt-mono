// File summary: Provides database-backed notification list and detail workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved notification close and batch-close orchestration out of the API controller.
// - 2026-07-09 pending Moved notification read/detail composition out of the API controller.

using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Application.Notifications;

public interface INotificationApplicationService
{
    // Function summary: Returns role-scoped notification rows for the requested filter and paging state.
    Task<NotificationQueryResult> QueryAsync(
        PortalUserContext user,
        NotificationQuery query,
        CancellationToken cancellationToken);

    // Function summary: Returns a role-scoped notification detail model, or null when hidden or missing.
    Task<NotificationDetailModel?> GetAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken);

    // Function summary: Builds a role-scoped detail model from a command result notification.
    Task<NotificationDetailModel?> BuildDetailAsync(
        PortalUserContext user,
        Notification notification,
        Deployment? deployment,
        CancellationToken cancellationToken);

    // Function summary: Converts the current portal user into the actor used by notification close commands.
    NotificationCloseActor BuildCloseActor(PortalUserContext user);

    // Function summary: Closes one visible alert notification and rebuilds its detail response model.
    Task<NotificationCloseWorkflowResult> CloseAsync(
        PortalUserContext user,
        Guid id,
        string? note,
        CancellationToken cancellationToken);

    // Function summary: Closes a batch of visible alert notifications and reports skipped ids.
    Task<NotificationBatchCloseResponse> BatchCloseAsync(
        PortalUserContext user,
        IReadOnlyCollection<Guid> ids,
        string? note,
        CancellationToken cancellationToken);
}

public sealed record NotificationQuery(
    string? SearchText,
    int Page,
    int PageSize,
    string Sort,
    string SortDir,
    string State,
    MonitorTypeEnum? MonitorType,
    AlertTypeEnum? AlertType,
    bool? OpenAlerts,
    Guid? SiteId);

public sealed class NotificationQueryResult
{
    public List<NotificationListModel> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = "";
    public string State { get; init; } = NotificationListStates.All;
    public bool IsScopedToCurrentUser { get; init; }
    public bool CanClose { get; init; }
}

public class NotificationListModel
{
    public Guid Id { get; init; }
    public Guid MonitorId { get; init; }
    public Guid? DeploymentId { get; init; }
    public string? FleetNumber { get; init; }
    public string SerialId { get; init; } = "";
    public string TypeOfMonitor { get; init; } = "";
    public string AlertType { get; init; } = "";
    public string AlertField { get; init; } = "";
    public double LimitOn { get; init; }
    public double Level { get; init; }
    public int AveragingPeriod { get; init; }
    public DateTime NotificationTime { get; init; }
    public DateTime? ClosedTime { get; init; }
    public Guid? ClosedByUser { get; init; }
    public string? ClosedNote { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public Guid? SiteId { get; init; }
    public string? SiteName { get; init; }
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string LimitName { get; init; } = "";
    public string AlertStatus { get; init; } = "";
    public bool CanClose { get; init; }
}

public sealed class NotificationDetailModel : NotificationListModel
{
    public DateTime? LastDataTime { get; init; }
    public DateTime? DeploymentStartDate { get; init; }
    public DateTime? DeploymentEndDate { get; init; }
    public double? Lat { get; init; }
    public double? Lng { get; init; }
    public string? Location { get; init; }
    public string? What3words { get; init; }
    public string? RecordingLink { get; init; }
    public DateTime GraphFromUtc { get; init; }
    public DateTime GraphToUtc { get; init; }
    public List<NotificationListModel> RelatedNotifications { get; init; } = [];
    public List<NotificationAlertLevelModel> AlertLevels { get; init; } = [];
}

public sealed class NotificationAlertLevelModel
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

public sealed class NotificationCloseWorkflowResult
{
    public bool NotFound { get; init; }
    public NotificationDetailModel? Detail { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a not-found result for hidden or missing notification close attempts.
    public static NotificationCloseWorkflowResult NotFoundResult()
    {
        return new NotificationCloseWorkflowResult { NotFound = true };
    }

    // Function summary: Builds a validation result for notification close attempts.
    public static NotificationCloseWorkflowResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors)
    {
        return new NotificationCloseWorkflowResult { Errors = errors };
    }

    // Function summary: Builds a successful notification close result.
    public static NotificationCloseWorkflowResult Success(NotificationDetailModel detail)
    {
        return new NotificationCloseWorkflowResult { Detail = detail };
    }
}

public sealed class NotificationApplicationService : INotificationApplicationService
{
    public static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["typeOfMonitor"] = "typeOfMonitor",
        ["fleetNumber"] = "fleetNumber",
        ["contractNumber"] = "contractNumber",
        ["siteName"] = "siteName",
        ["alertField"] = "alertField",
        ["limitName"] = "limitName",
        ["notificationTime"] = "notificationTime",
        ["level"] = "level",
        ["alertStatus"] = "alertStatus"
    };

    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;

    // Function summary: Initializes the notification read service with the domain context.
    public NotificationApplicationService(RVTDbContext domainContext, IMediator mediator)
    {
        this.domainContext = domainContext;
        this.mediator = mediator;
    }

    // Function summary: Builds the paged notification list for the caller's role and filters.
    public async Task<NotificationQueryResult> QueryAsync(
        PortalUserContext user,
        NotificationQuery query,
        CancellationToken cancellationToken)
    {
        var actor = BuildCloseActor(user);
        var rows = await BuildNotificationRowsAsync(cancellationToken);
        var visibleSiteIds = await NotificationCloseWorkflow.VisibleSiteIdsAsync(domainContext, actor, cancellationToken);
        rows = ApplyRoleVisibility(rows, actor, visibleSiteIds).ToList();
        rows = ApplyState(rows, query.State).ToList();

        if (query.SiteId.HasValue)
        {
            rows = rows.Where(row => row.SiteId == query.SiteId.Value).ToList();
        }

        if (query.MonitorType.HasValue)
        {
            rows = rows.Where(row => string.Equals(row.TypeOfMonitor, query.MonitorType.Value.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.AlertType.HasValue)
        {
            rows = rows.Where(row => string.Equals(row.AlertType, query.AlertType.Value.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.OpenAlerts == true)
        {
            rows = rows.Where(row => row.ClosedTime == null && string.Equals(row.AlertType, AlertTypeEnum.Alert.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (query.OpenAlerts == false)
        {
            rows = rows.Where(row => row.ClosedTime != null).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            rows = rows.Where(row =>
                Contains(row.FleetNumber, search) ||
                Contains(row.SerialId, search) ||
                Contains(row.TypeOfMonitor, search) ||
                Contains(row.ContractNumber, search) ||
                Contains(row.SiteName, search) ||
                Contains(row.AlertField, search) ||
                Contains(row.LimitName, search))
                .ToList();
        }

        rows = ApplySort(rows, query.Sort, query.SortDir).ToList();
        var total = rows.Count;
        return new NotificationQueryResult
        {
            Results = rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize),
            HasPreviousPage = query.Page > 1 && total > 0,
            HasNextPage = query.Page * query.PageSize < total,
            SearchText = query.SearchText ?? "",
            Sort = query.Sort,
            SortDir = query.SortDir,
            State = query.State,
            IsScopedToCurrentUser = actor.IsCompanyUser,
            CanClose = actor.IsAdmin || actor.IsCompanyUser
        };
    }

    // Function summary: Builds one notification detail when it is visible to the caller.
    public async Task<NotificationDetailModel?> GetAsync(
        PortalUserContext user,
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await domainContext.Notifications
            .AsNoTracking()
            .Include(item => item.Monitor)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification == null)
        {
            return null;
        }

        var deployment = await NotificationCloseWorkflow.FindDeploymentForNotificationAsync(domainContext, notification, cancellationToken);
        return await BuildDetailAsync(user, notification, deployment, cancellationToken);
    }

    // Function summary: Builds a notification detail from a known notification/deployment pair.
    public async Task<NotificationDetailModel?> BuildDetailAsync(
        PortalUserContext user,
        Notification notification,
        Deployment? deployment,
        CancellationToken cancellationToken)
    {
        var actor = BuildCloseActor(user);
        var visibleSiteIds = await NotificationCloseWorkflow.VisibleSiteIdsAsync(domainContext, actor, cancellationToken);
        var row = BuildNotificationListModel(notification, deployment);
        if (!CanReadNotification(row, actor, visibleSiteIds))
        {
            return null;
        }

        return await BuildNotificationDetailAsync(notification, deployment, row, cancellationToken);
    }

    // Function summary: Converts shared portal role facts to the notification close actor shape.
    public NotificationCloseActor BuildCloseActor(PortalUserContext user)
    {
        return new NotificationCloseActor(user.UserId, user.IsAdmin, user.IsCompanyUser && !user.IsAdmin);
    }

    // Function summary: Dispatches the transactional close command and rebuilds role-scoped detail for the caller.
    public async Task<NotificationCloseWorkflowResult> CloseAsync(
        PortalUserContext user,
        Guid id,
        string? note,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CloseNotificationCommand(id, note, BuildCloseActor(user)),
            cancellationToken);
        if (result.NotFound || result.Notification == null)
        {
            return NotificationCloseWorkflowResult.NotFoundResult();
        }

        if (result.Errors.Count > 0)
        {
            return NotificationCloseWorkflowResult.ValidationFailed(result.Errors);
        }

        var detail = await BuildDetailAsync(user, result.Notification, result.Deployment, cancellationToken);
        return detail == null
            ? NotificationCloseWorkflowResult.NotFoundResult()
            : NotificationCloseWorkflowResult.Success(detail);
    }

    // Function summary: Dispatches the transactional batch-close command for a caller's role scope.
    public Task<NotificationBatchCloseResponse> BatchCloseAsync(
        PortalUserContext user,
        IReadOnlyCollection<Guid> ids,
        string? note,
        CancellationToken cancellationToken)
    {
        return mediator.Send(
            new BatchCloseNotificationsCommand(ids, note, BuildCloseActor(user)),
            cancellationToken);
    }

    // Function summary: Builds notification rows with effective deployment ownership attached.
    private async Task<List<NotificationListModel>> BuildNotificationRowsAsync(CancellationToken cancellationToken)
    {
        var notifications = await domainContext.Notifications
            .AsNoTracking()
            .Include(notification => notification.Monitor)
            .OrderByDescending(notification => notification.NotificationTime)
            .ToListAsync(cancellationToken);
        var deploymentLookup = await NotificationCloseWorkflow.BuildDeploymentLookupAsync(domainContext, notifications, cancellationToken);

        return notifications
            .Where(notification => !string.IsNullOrWhiteSpace(notification.Monitor?.FleetNr))
            .Select(notification =>
            {
                deploymentLookup.TryGetValue(notification.Id, out var deployment);
                return BuildNotificationListModel(notification, deployment);
            })
            .ToList();
    }

    // Function summary: Builds the frontend-facing notification row model.
    private static NotificationListModel BuildNotificationListModel(Notification notification, Deployment? deployment)
    {
        var monitor = notification.Monitor ?? deployment?.Monitor;
        var contract = deployment?.Contract;
        var alertType = notification.AlertType.ToString();
        return new NotificationListModel
        {
            Id = notification.Id,
            MonitorId = notification.MonitorId,
            DeploymentId = deployment?.Id,
            FleetNumber = monitor?.FleetNr,
            SerialId = monitor?.SerialId ?? "",
            TypeOfMonitor = monitor?.TypeOfMonitor.ToString() ?? "",
            AlertType = alertType,
            AlertField = notification.AlertField,
            LimitOn = notification.LimitOn,
            Level = notification.Level,
            AveragingPeriod = notification.AveragingPeriod,
            NotificationTime = notification.NotificationTime,
            ClosedTime = notification.ClosedTime,
            ClosedByUser = notification.ClosedByUser,
            ClosedNote = notification.ClosedNote,
            ContractId = contract?.Id,
            ContractNumber = contract?.ContractNumber,
            SiteId = contract?.SiteiD,
            SiteName = contract?.Site?.SiteName,
            CompanyId = contract?.CompanyId,
            CompanyName = contract?.Company?.CompanyName,
            LimitName = BuildLimitName(monitor?.TypeOfMonitor, notification),
            AlertStatus = NotificationStatus(notification),
            CanClose = notification.AlertType == AlertTypeEnum.Alert
        };
    }

    // Function summary: Builds the detailed notification model including related alerts and level thresholds.
    private async Task<NotificationDetailModel> BuildNotificationDetailAsync(
        Notification notification,
        Deployment? deployment,
        NotificationListModel row,
        CancellationToken cancellationToken)
    {
        var graphWindowMinutes = row.TypeOfMonitor == MonitorTypeEnum.Noise.ToString() ? 30 : 5;
        var related = (await BuildNotificationRowsAsync(cancellationToken))
            .Where(item => item.MonitorId == notification.MonitorId && item.Id != notification.Id)
            .Where(item => item.DeploymentId == row.DeploymentId)
            .OrderByDescending(item => item.NotificationTime)
            .Take(10)
            .ToList();
        var alertLevelEntities = await domainContext.RvtAlertRules
            .AsNoTracking()
            .Where(level => level.MonitorId == notification.MonitorId && !level.IsDeleted)
            .OrderBy(level => level.AlertType)
            .ThenBy(level => level.AlertField)
            .ToListAsync(cancellationToken);

        var monitorType = Enum.TryParse<MonitorTypeEnum>(row.TypeOfMonitor, out var parsedMonitorType)
            ? parsedMonitorType
            : (MonitorTypeEnum?)null;

        return new NotificationDetailModel
        {
            Id = row.Id,
            MonitorId = row.MonitorId,
            DeploymentId = row.DeploymentId,
            FleetNumber = row.FleetNumber,
            SerialId = row.SerialId,
            TypeOfMonitor = row.TypeOfMonitor,
            AlertType = row.AlertType,
            AlertField = row.AlertField,
            LimitOn = row.LimitOn,
            Level = row.Level,
            AveragingPeriod = row.AveragingPeriod,
            NotificationTime = row.NotificationTime,
            ClosedTime = row.ClosedTime,
            ClosedByUser = row.ClosedByUser,
            ClosedNote = row.ClosedNote,
            ContractId = row.ContractId,
            ContractNumber = row.ContractNumber,
            SiteId = row.SiteId,
            SiteName = row.SiteName,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName,
            LimitName = row.LimitName,
            AlertStatus = row.AlertStatus,
            CanClose = row.CanClose,
            LastDataTime = LastDataTime(notification.Monitor),
            DeploymentStartDate = deployment?.StartDate,
            DeploymentEndDate = deployment?.EndDate,
            Lat = deployment?.Lat,
            Lng = deployment?.Lng,
            Location = deployment?.Location,
            What3words = deployment?.What3words,
            RecordingLink = notification.RecordingLink,
            GraphFromUtc = notification.NotificationTime.AddMinutes(-graphWindowMinutes),
            GraphToUtc = notification.NotificationTime.AddMinutes(graphWindowMinutes),
            RelatedNotifications = related,
            AlertLevels = alertLevelEntities.Select(level => BuildAlertLevelModel(level, monitorType)).ToList()
        };
    }

    // Function summary: Applies admin/company-user notification visibility.
    private static IEnumerable<NotificationListModel> ApplyRoleVisibility(
        IEnumerable<NotificationListModel> rows,
        NotificationCloseActor actor,
        IReadOnlySet<Guid> visibleSiteIds)
    {
        if (actor.IsAdmin)
        {
            return rows;
        }

        if (!actor.IsCompanyUser)
        {
            return [];
        }

        return rows.Where(row => row.SiteId.HasValue && visibleSiteIds.Contains(row.SiteId.Value));
    }

    // Function summary: Applies the requested notification state filter.
    private static IEnumerable<NotificationListModel> ApplyState(IEnumerable<NotificationListModel> rows, string state)
    {
        return state switch
        {
            NotificationListStates.Open => rows.Where(row => row.ClosedTime == null && string.Equals(row.AlertType, AlertTypeEnum.Alert.ToString(), StringComparison.OrdinalIgnoreCase)),
            NotificationListStates.Cautions => rows.Where(row => string.Equals(row.AlertType, AlertTypeEnum.Caution.ToString(), StringComparison.OrdinalIgnoreCase)),
            _ => rows
        };
    }

    // Function summary: Sorts notification rows by the requested field and direction.
    private static IEnumerable<NotificationListModel> ApplySort(IEnumerable<NotificationListModel> rows, string sort, string sortDir)
    {
        var descending = sortDir == SortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "typeofmonitor" => OrderRows(rows, row => row.TypeOfMonitor, descending),
            "fleetnumber" => OrderRows(rows, row => row.FleetNumber, descending),
            "contractnumber" => OrderRows(rows, row => row.ContractNumber, descending),
            "sitename" => OrderRows(rows, row => row.SiteName, descending),
            "alertfield" => OrderRows(rows, row => row.AlertField, descending),
            "limitname" => OrderRows(rows, row => row.LimitName, descending),
            "level" => OrderRows(rows, row => row.Level, descending),
            "alertstatus" => OrderRows(rows, row => row.AlertStatus, descending),
            _ => OrderRows(rows, row => row.NotificationTime, descending)
        };
    }

    // Function summary: Orders rows by a typed key.
    private static IOrderedEnumerable<NotificationListModel> OrderRows<T>(
        IEnumerable<NotificationListModel> rows,
        Func<NotificationListModel, T> keySelector,
        bool descending)
    {
        return descending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }

    // Function summary: Evaluates whether the notification row is visible to the actor.
    private static bool CanReadNotification(
        NotificationListModel row,
        NotificationCloseActor actor,
        IReadOnlySet<Guid> visibleSiteIds)
    {
        return actor.IsAdmin || (actor.IsCompanyUser && row.SiteId.HasValue && visibleSiteIds.Contains(row.SiteId.Value));
    }

    // Function summary: Builds a user-facing threshold label for the notification.
    private static string BuildLimitName(MonitorTypeEnum? monitorType, Notification notification)
    {
        if (monitorType == MonitorTypeEnum.Vibration)
        {
            return $"Peak > {Math.Round(notification.LimitOn, 2)}";
        }

        return string.IsNullOrWhiteSpace(notification.AlertField)
            ? ""
            : $"{notification.AlertField} > {Math.Round(notification.LimitOn, 2)}";
    }

    // Function summary: Builds the alert open/closed status label.
    private static string NotificationStatus(Notification notification)
    {
        if (notification.AlertType != AlertTypeEnum.Alert)
        {
            return "";
        }

        return notification.ClosedTime == null ? "Open" : "Closed";
    }

    // Function summary: Returns the most recent last-data timestamp recorded on a monitor.
    private static DateTime? LastDataTime(RVT.Entities.Monitor? monitor)
    {
        return monitor == null
            ? null
            : new[] { monitor.LastDataTime15Min, monitor.LastDataTime1Min, monitor.LastDataTime1Hour, monitor.LastDataTime24Hour }
                .Where(value => value.HasValue)
                .Max();
    }

    // Function summary: Builds an alert-level model for notification detail thresholds.
    private static NotificationAlertLevelModel BuildAlertLevelModel(Alertlevel level, MonitorTypeEnum? monitorType)
    {
        var resolvedMonitorType = monitorType ?? level.Monitor?.TypeOfMonitor;
        return new NotificationAlertLevelModel
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
            AveragingPeriodLabel = AveragingPeriodLabel(level, resolvedMonitorType),
            Weekdays = level.Weekdays,
            Saturdays = level.Saturdays,
            Sundays = level.Sundays,
            StartTime = FormatTime(level.StartTime),
            EndTime = FormatTime(level.EndTime),
            IsDeleted = level.IsDeleted
        };
    }

    // Function summary: Formats an optional time value as HH:mm.
    private static string? FormatTime(TimeSpan? value)
    {
        return value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    // Function summary: Builds a human-readable averaging period label.
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

    // Function summary: Converts enum member names into existing option labels.
    private static string EnumLabel(string value)
    {
        return value.TrimStart('_').Replace('_', ' ');
    }

    // Function summary: Evaluates case-insensitive substring search.
    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}

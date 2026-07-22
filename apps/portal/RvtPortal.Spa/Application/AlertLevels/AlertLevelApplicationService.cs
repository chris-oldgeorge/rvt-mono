// File summary: Provides alert-level query, option, detail, and mutation workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved alert-level write orchestration out of the API controller.
// - 2026-07-09 pending Moved alert-level read composition and monitor visibility checks out of the API controller.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.AlertLevels;

public interface IAlertLevelApplicationService
{
    // Function summary: Returns a paged alert-level list for a visible monitor.
    Task<AlertLevelQueryResult> QueryAsync(
        PortalUserContext actor,
        AlertLevelQuery query,
        CancellationToken cancellationToken);

    // Function summary: Returns alert-level form options for a visible monitor.
    Task<AlertLevelOptionsResponse?> OptionsAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken);

    // Function summary: Returns one alert-level item after enforcing monitor visibility.
    Task<AlertLevelItem?> GetAsync(
        PortalUserContext actor,
        Guid alertLevelId,
        CancellationToken cancellationToken);

    // Function summary: Creates a non-vibration alert level.
    Task<AlertLevelMutationWorkflowResult> CreateAsync(
        AlertLevelMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Updates a non-vibration alert level.
    Task<AlertLevelMutationWorkflowResult> UpdateAsync(
        Guid alertLevelId,
        AlertLevelMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Upserts vibration alert/caution levels for a monitor.
    Task<VibrationAlertLevelMutationWorkflowResult> UpdateVibrationAsync(
        Guid monitorId,
        VibrationAlertLevelMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Soft-deletes an alert level.
    Task<AlertLevelMutationWorkflowResult> DeleteAsync(Guid alertLevelId, CancellationToken cancellationToken);
}

public sealed record AlertLevelQuery(
    Guid? MonitorId,
    string? SearchText,
    string? Sort,
    string SortDir,
    int Page,
    int PageSize);

public sealed class AlertLevelQueryResult
{
    public bool MissingMonitor { get; init; }
    public bool NotFound { get; init; }
    public string? InvalidSort { get; init; }
    public IReadOnlyCollection<string> ValidSorts { get; init; } = AlertLevelApplicationService.SortFields.Keys.ToArray();
    public QueryAlertLevelsResponse? Response { get; init; }
}

public sealed class AlertLevelMutationWorkflowResult
{
    public bool NotFound { get; init; }
    public Guid? AlertLevelId { get; init; }
    public AlertLevelItem? Item { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a workflow result from a command result.
    public static AlertLevelMutationWorkflowResult FromCommand(AlertLevelCommandResult result)
    {
        return new AlertLevelMutationWorkflowResult
        {
            NotFound = result.NotFound,
            AlertLevelId = result.AlertLevelId,
            Item = result.Item,
            Errors = result.Errors
        };
    }
}

public sealed class VibrationAlertLevelMutationWorkflowResult
{
    public bool NotFound { get; init; }
    public VibrationAlertLevelResponse? Response { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a workflow result from a vibration command result.
    public static VibrationAlertLevelMutationWorkflowResult FromCommand(VibrationAlertLevelCommandResult result)
    {
        return new VibrationAlertLevelMutationWorkflowResult
        {
            NotFound = result.NotFound,
            Response = result.Response,
            Errors = result.Errors
        };
    }
}

public sealed class AlertLevelApplicationService : IAlertLevelApplicationService
{
    public static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["alertField"] = "alertField",
        ["alertType"] = "alertType",
        ["limitOn"] = "limitOn",
        ["averagingPeriod"] = "averagingPeriod",
        ["weekdays"] = "weekdays",
        ["saturdays"] = "saturdays",
        ["sundays"] = "sundays",
        ["startTime"] = "startTime",
        ["endTime"] = "endTime"
    };

    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;

    // Function summary: Initializes alert-level workflows with domain reads and transactional command dispatch dependencies.
    public AlertLevelApplicationService(
        RVTDbContext domainContext,
        IMediator mediator)
    {
        this.domainContext = domainContext;
        this.mediator = mediator;
    }

    // Function summary: Returns a paged alert-level list for a visible monitor.
    public async Task<AlertLevelQueryResult> QueryAsync(
        PortalUserContext actor,
        AlertLevelQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.MonitorId.HasValue || query.MonitorId.Value == Guid.Empty)
        {
            return new AlertLevelQueryResult { MissingMonitor = true };
        }

        var monitorId = query.MonitorId.Value;
        var monitor = await domainContext.MonitorsList.AsNoTracking().SingleOrDefaultAsync(item => item.Id == monitorId, cancellationToken);
        if (monitor == null || !await CanReadMonitorAsync(actor, monitorId, cancellationToken))
        {
            return new AlertLevelQueryResult { NotFound = true };
        }

        var requestedSort = string.IsNullOrWhiteSpace(query.Sort) ? "alertField" : query.Sort.Trim();
        if (!SortFields.ContainsKey(requestedSort))
        {
            return new AlertLevelQueryResult
            {
                InvalidSort = requestedSort,
                ValidSorts = SortFields.Keys.ToArray()
            };
        }

        var levels = await domainContext.RvtAlertRules
            .AsNoTracking()
            .Where(level => level.MonitorId == monitorId && !level.IsDeleted)
            .ToListAsync(cancellationToken);
        var rows = ApplySort(
            levels.Select(level => AlertLevelWorkflow.BuildAlertLevelItem(level, monitor.TypeOfMonitor)),
            requestedSort,
            query.SortDir).ToList();
        var total = rows.Count;

        return new AlertLevelQueryResult
        {
            Response = new QueryAlertLevelsResponse
            {
                Results = rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize),
                HasPreviousPage = query.Page > 1 && total > 0,
                HasNextPage = query.Page * query.PageSize < total,
                SearchText = query.SearchText ?? "",
                Sort = requestedSort,
                SortDir = query.SortDir,
                MonitorId = monitor.Id,
                SerialId = monitor.SerialId,
                FleetNumber = monitor.FleetNr,
                TypeOfMonitor = monitor.TypeOfMonitor.ToString(),
                CanManage = actor.IsAdmin,
                Options = AlertLevelWorkflow.BuildOptions(monitor)
            }
        };
    }

    // Function summary: Returns alert-level form options for a visible monitor.
    public async Task<AlertLevelOptionsResponse?> OptionsAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken)
    {
        var monitor = await domainContext.MonitorsList.AsNoTracking().SingleOrDefaultAsync(item => item.Id == monitorId, cancellationToken);
        return monitor == null || !await CanReadMonitorAsync(actor, monitorId, cancellationToken)
            ? null
            : AlertLevelWorkflow.BuildOptions(monitor);
    }

    // Function summary: Returns one alert-level item after enforcing monitor visibility.
    public async Task<AlertLevelItem?> GetAsync(
        PortalUserContext actor,
        Guid alertLevelId,
        CancellationToken cancellationToken)
    {
        var level = await domainContext.RvtAlertRules.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == alertLevelId && !item.IsDeleted,
            cancellationToken);
        return level == null || !await CanReadMonitorAsync(actor, level.MonitorId, cancellationToken)
            ? null
            : AlertLevelWorkflow.BuildAlertLevelItem(level);
    }

    // Function summary: Creates a non-vibration alert level through the transactional command pipeline.
    public async Task<AlertLevelMutationWorkflowResult> CreateAsync(
        AlertLevelMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateAlertLevelCommand(request), cancellationToken);
        return AlertLevelMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Updates a non-vibration alert level through the transactional command pipeline.
    public async Task<AlertLevelMutationWorkflowResult> UpdateAsync(
        Guid alertLevelId,
        AlertLevelMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateAlertLevelCommand(alertLevelId, request), cancellationToken);
        return AlertLevelMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Upserts vibration alert/caution levels through the transactional command pipeline.
    public async Task<VibrationAlertLevelMutationWorkflowResult> UpdateVibrationAsync(
        Guid monitorId,
        VibrationAlertLevelMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateVibrationAlertLevelsCommand(monitorId, request), cancellationToken);
        return VibrationAlertLevelMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Soft-deletes an alert level through the transactional command pipeline.
    public async Task<AlertLevelMutationWorkflowResult> DeleteAsync(Guid alertLevelId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteAlertLevelCommand(alertLevelId), cancellationToken);
        return AlertLevelMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Evaluates whether the actor may read a monitor's alert-level data.
    private async Task<bool> CanReadMonitorAsync(
        PortalUserContext actor,
        Guid monitorId,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return await domainContext.MonitorsList.AnyAsync(monitor => monitor.Id == monitorId, cancellationToken);
        }

        if (!actor.IsCompanyUser || !actor.UserId.HasValue)
        {
            return false;
        }

        var userId = actor.UserId.Value;
        return await domainContext.Deployments
            .AsNoTracking()
            .Include(deployment => deployment.Contract)
            .AnyAsync(deployment =>
                deployment.MonitorId == monitorId &&
                deployment.EndDate == null &&
                deployment.Contract.SiteiD.HasValue &&
                domainContext.SiteUsers.Any(siteUser =>
                    siteUser.SiteId == deployment.Contract.SiteiD.Value &&
                    siteUser.UserId == userId &&
                    siteUser.EndDate == null),
                cancellationToken);
    }

    // Function summary: Applies the requested alert-level sort to projected API rows.
    private static IEnumerable<AlertLevelItem> ApplySort(IEnumerable<AlertLevelItem> rows, string sort, string sortDir)
    {
        var descending = sortDir == SortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "alerttype" => OrderRows(rows, row => row.AlertType, descending),
            "limiton" => OrderRows(rows, row => row.LimitOn, descending),
            "averagingperiod" => OrderRows(rows, row => row.AveragingPeriod, descending),
            "weekdays" => OrderRows(rows, row => row.Weekdays, descending),
            "saturdays" => OrderRows(rows, row => row.Saturdays, descending),
            "sundays" => OrderRows(rows, row => row.Sundays, descending),
            "starttime" => OrderRows(rows, row => row.StartTime, descending),
            "endtime" => OrderRows(rows, row => row.EndTime, descending),
            _ => OrderRows(rows, row => row.AlertField, descending)
        };
    }

    // Function summary: Orders alert-level rows by a selected key.
    private static IOrderedEnumerable<AlertLevelItem> OrderRows<T>(
        IEnumerable<AlertLevelItem> rows,
        Func<AlertLevelItem, T> keySelector,
        bool descending)
    {
        return descending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }
}

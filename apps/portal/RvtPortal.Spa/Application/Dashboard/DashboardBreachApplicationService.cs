// File summary: Provides database-backed dashboard breach and alert list querying.
// Major updates:
// - 2026-07-08 pending Moved dashboard breach-alert filtering, sorting, paging, and row shaping out of the controller.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Application.Paging;
using RVT.DataAccess.Context;
using RVT.Entities;

namespace RvtPortal.Spa.Application.Dashboard;

public interface IDashboardBreachApplicationService
{
    // Function summary: Returns vibration breach and alert rows for a requested day.
    Task<DashboardBreachResult> QueryAsync(DashboardBreachQuery request, CancellationToken cancellationToken);
}

public sealed record DashboardBreachQuery(
    DateTime? Date,
    PageRequest Page);

public sealed class DashboardBreachResult
{
    public DateTime Date { get; init; }
    public PagedResult<DashboardBreachModel> Page { get; init; } = new();
}

public sealed class DashboardBreachModel
{
    public string? SerialId { get; init; }
    public string? FleetNumber { get; init; }
    public Guid? MonitorId { get; init; }
    public DateTime? SampleTime { get; init; }
    public Guid? NotificationId { get; init; }
    public DateTime? NotificationTime { get; init; }
    public double? Xvtop { get; init; }
    public double? Yvtop { get; init; }
    public double? Zvtop { get; init; }
}

public sealed class DashboardBreachApplicationService : IDashboardBreachApplicationService
{
    public const string DefaultSort = "notificationTime";

    public static readonly IReadOnlySet<string> SortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "serialId",
        "fleetNumber",
        "sampleTime",
        DefaultSort
    };

    private readonly RVTDbContext domainContext;
    private readonly IRvtDateTimeProvider dateTimeProvider;

    // Function summary: Initializes this application service with the domain read context and the clock/time-zone provider.
    public DashboardBreachApplicationService(RVTDbContext domainContext, IRvtDateTimeProvider dateTimeProvider)
    {
        this.domainContext = domainContext;
        this.dateTimeProvider = dateTimeProvider;
    }

    // Function summary: Returns vibration breach and alert rows after applying date, sort, and page in EF.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/sonar/globalization-suppressions.md")]
    public async Task<DashboardBreachResult> QueryAsync(DashboardBreachQuery request, CancellationToken cancellationToken)
    {
        // NotificationTime is timestamptz, so its query bounds must be Kind=Utc or Npgsql rejects them. The
        // request names a calendar day in the configured local zone (defaulting to local "today"); convert that
        // day's local midnight boundaries to UTC rather than comparing a local/Unspecified value directly.
        var localDay = (request.Date ?? dateTimeProvider.UtcToLocal(dateTimeProvider.UtcNow)).Date;
        var start = localDay.LocalToUtc(dateTimeProvider);
        var end = localDay.AddDays(1).LocalToUtc(dateTimeProvider);
        var query = domainContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.Monitor.TypeOfMonitor == MonitorTypeEnum.Vibration &&
                notification.NotificationTime >= start &&
                notification.NotificationTime < end)
            .Select(notification => new DashboardBreachModel
            {
                SerialId = notification.Monitor.SerialId,
                FleetNumber = notification.Monitor.FleetNr,
                MonitorId = notification.MonitorId,
                SampleTime = notification.NotificationTime,
                NotificationId = notification.Id,
                NotificationTime = notification.NotificationTime,
                Xvtop = (notification.AlertField ?? "").ToLower() == "xvtop" ? notification.Level : null,
                Yvtop = (notification.AlertField ?? "").ToLower() == "yvtop" ? notification.Level : null,
                Zvtop = (notification.AlertField ?? "").ToLower() == "zvtop" ? notification.Level : null
            });
        var total = await query.CountAsync(cancellationToken);
        var results = await ApplySort(query, request.Page.Sort, request.Page.SortDir)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);
        return new DashboardBreachResult
        {
            Date = localDay,
            Page = new PagedResult<DashboardBreachModel>
            {
                Results = results,
                Total = total,
                Page = request.Page.Page,
                PageSize = request.Page.PageSize,
                SearchText = request.Page.SearchText ?? "",
                Sort = request.Page.Sort,
                SortDir = request.Page.SortDir
            }
        };
    }

    // Function summary: Applies supported dashboard breach sort fields while rows remain queryable.
    private static IQueryable<DashboardBreachModel> ApplySort(
        IQueryable<DashboardBreachModel> query,
        string sort,
        string sortDir)
    {
        var descending = sortDir == PageSortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "serialid" => descending ? query.OrderByDescending(item => item.SerialId) : query.OrderBy(item => item.SerialId),
            "fleetnumber" => descending ? query.OrderByDescending(item => item.FleetNumber) : query.OrderBy(item => item.FleetNumber),
            "sampletime" => descending ? query.OrderByDescending(item => item.SampleTime) : query.OrderBy(item => item.SampleTime),
            _ => descending ? query.OrderByDescending(item => item.NotificationTime) : query.OrderBy(item => item.NotificationTime)
        };
    }
}

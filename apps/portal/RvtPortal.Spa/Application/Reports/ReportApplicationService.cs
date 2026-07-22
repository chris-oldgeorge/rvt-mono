// File summary: Provides report list and detail read workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved report search, sort, paging, and detail reads out of the API controller.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.Entities;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Reports;

public interface IReportApplicationService
{
    // Function summary: Returns a paged report list while keeping filtering, count, sort, and paging in EF.
    Task<ReportQueryResult> QueryAsync(ReportQuery request, CancellationToken cancellationToken);

    // Function summary: Returns one report item by id, or null when absent.
    Task<ReportListItem?> GetAsync(Guid reportId, CancellationToken cancellationToken);
}

public sealed record ReportQuery(
    string? SearchText,
    string? Sort,
    string SortDir,
    int Page,
    int PageSize);

public sealed class ReportQueryResult
{
    public string? InvalidSort { get; init; }
    public IReadOnlyCollection<string> AllowedSortFields { get; init; } = ReportApplicationService.SortFields.ToArray();
    public QueryReportsResponse? Response { get; init; }
}

public sealed class ReportApplicationService : IReportApplicationService
{
    internal static readonly HashSet<string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "reportName",
        "reportDate",
        "reportFrom",
        "reportTo",
        "frequency",
        "siteName",
        "contracts"
    };

    private readonly RVTSearchContext searchContext;

    // Function summary: Initializes report reads with the search context.
    public ReportApplicationService(RVTSearchContext searchContext)
    {
        this.searchContext = searchContext;
    }

    // Function summary: Returns a paged report list while keeping filtering, count, sort, and paging in EF.
    public async Task<ReportQueryResult> QueryAsync(ReportQuery request, CancellationToken cancellationToken)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "reportDate" : request.Sort.Trim();
        if (!SortFields.Contains(requestedSort))
        {
            return new ReportQueryResult
            {
                InvalidSort = requestedSort,
                AllowedSortFields = SortFields.ToArray()
            };
        }

        var query = searchContext.ReportSearches
            .AsNoTracking()
            .Where(report => !report.Deleted);
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            query = ApplySearch(query, request.SearchText);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await ApplySort(query, requestedSort, request.SortDir)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(BuildReportItem)
            .ToList();

        return new ReportQueryResult
        {
            Response = new QueryReportsResponse
            {
                Results = items,
                Total = total,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize),
                HasPreviousPage = request.Page > 1 && total > 0,
                HasNextPage = request.Page * request.PageSize < total,
                SearchText = request.SearchText ?? "",
                Sort = requestedSort,
                SortDir = request.SortDir
            }
        };
    }

    // Function summary: Returns one report item by id, or null when absent.
    public async Task<ReportListItem?> GetAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var report = await searchContext.ReportSearches
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reportId && !item.Deleted, cancellationToken);
        return report == null ? null : BuildReportItem(report);
    }

    // Function summary: Maps a search-row report to the existing API contract.
    private static ReportListItem BuildReportItem(ReportSearch report)
    {
        return new ReportListItem
        {
            Id = report.Id,
            SiteId = report.SiteId,
            SiteName = report.SiteName,
            ReportDate = report.ReportDate,
            ReportFrom = report.ReportFrom,
            ReportTo = report.ReportTo,
            ReportLink = report.ReportLink,
            ReportRuleId = report.ReportRuleId,
            Frequency = report.Frequency,
            FrequencyLabel = FrequencyLabel((ReportFrequencyType)report.Frequency),
            ReportName = report.ReportName,
            Contracts = report.Contracts
        };
    }

    // Function summary: Applies database-side report text and frequency filtering.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/sonar/globalization-suppressions.md")]
    private static IQueryable<ReportSearch> ApplySearch(IQueryable<ReportSearch> reports, string searchText)
    {
        var search = searchText.Trim().ToLower();
        var frequencyMatches = MatchingFrequencies(search)
            .Select(frequency => (int)frequency)
            .ToArray();
        return reports.Where(report =>
            (report.ReportName != null && report.ReportName.ToLower().Contains(search)) ||
            report.SiteName.ToLower().Contains(search) ||
            (report.Contracts != null && report.Contracts.ToLower().Contains(search)) ||
            report.ReportLink.ToLower().Contains(search) ||
            frequencyMatches.Contains(report.Frequency));
    }

    // Function summary: Applies database-side report sorting.
    private static IOrderedQueryable<ReportSearch> ApplySort(IQueryable<ReportSearch> reports, string sort, string direction)
    {
        var descending = string.Equals(direction, SortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "reportname" => descending ? reports.OrderByDescending(report => report.ReportName) : reports.OrderBy(report => report.ReportName),
            "reportfrom" => descending ? reports.OrderByDescending(report => report.ReportFrom) : reports.OrderBy(report => report.ReportFrom),
            "reportto" => descending ? reports.OrderByDescending(report => report.ReportTo) : reports.OrderBy(report => report.ReportTo),
            "frequency" => descending ? reports.OrderByDescending(report => report.Frequency) : reports.OrderBy(report => report.Frequency),
            "sitename" => descending ? reports.OrderByDescending(report => report.SiteName) : reports.OrderBy(report => report.SiteName),
            "contracts" => descending ? reports.OrderByDescending(report => report.Contracts) : reports.OrderBy(report => report.Contracts),
            _ => descending ? reports.OrderByDescending(report => report.ReportDate) : reports.OrderBy(report => report.ReportDate)
        };
    }

    // Function summary: Matches report frequency labels that should participate in text search.
    private static IEnumerable<ReportFrequencyType> MatchingFrequencies(string search)
    {
        return Enum.GetValues<ReportFrequencyType>()
            .Where(frequency => FrequencyLabel(frequency).Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    // Function summary: Returns the existing report-frequency display label.
    private static string FrequencyLabel(ReportFrequencyType frequency)
    {
        return frequency == ReportFrequencyType.WeeklyAndMonthly ? "Weekly and Monthly" : frequency.ToString();
    }
}

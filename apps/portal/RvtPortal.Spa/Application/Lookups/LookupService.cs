// File summary: Coordinates database-backed lookup queries for admin search suggestions.
// Major updates:
// - 2026-07-09 pending Replaced whole-table lookup caching and sync-over-async reads with bounded async EF queries.
// - 2026-07-08 pending Centralized legacy synchronous repository reads and tidied stale lookup comments.
// - 2026-06-26 pending Removed unread search repository dependency and aligned parameters for Sonar cleanup.
// - 2026-06-25 pending Resolved legacy nullable warnings (CS8600/CS8603/CS8619) in lookup projections and cache returns.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-10 pending Replaced repeated lookup search lowercasing with allocation-free comparison helpers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;

namespace RvtPortal.Spa.Application.Lookups;

public interface ILookupService
{
    Task<List<string>> CompaniesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> ContractsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> MonitorsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> MonitorsOnlineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> MonitorsNewSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> MonitorsOfflineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> SitesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
    Task<List<string>> UserSearchAsync(Guid companyId, string searchString, int take, bool includeAdmin = false, CancellationToken cancellationToken = default);
    Task<List<string>> UserSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
}

public class LookupService : ILookupService
{
    private const int DefaultTake = 20;
    private const int MaximumTake = 50;

    private readonly RVTDbContext _dbContext;
    private readonly RVTSearchContext _searchContext;

    // Function summary: Initializes lookup queries with scoped EF contexts so searches execute in the database.
    public LookupService(RVTDbContext dbContext, RVTSearchContext searchContext)
    {
        _dbContext = dbContext;
        _searchContext = searchContext;
    }

    // Function summary: Returns company-name lookup suggestions using a bounded domain query.
    public Task<List<string>> CompaniesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        return LookupValuesAsync(
            _dbContext.Companies.AsNoTracking().Select(company => company.CompanyName),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns combined contract, site, and company lookup suggestions from the contract search view.
    public Task<List<string>> ContractsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        IQueryable<ContractSearch> rows = _searchContext.ContractSearches.AsNoTracking();
        return LookupValuesAsync(
            rows.Select(contract => contract.ContractNumber)
                .Concat(rows.Select(contract => contract.CompanyName))
                .Concat(rows.Select(contract => contract.SiteName)),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns combined site, company, contract, and address lookup suggestions.
    public Task<List<string>> SitesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        IQueryable<SiteSearch> rows = _searchContext.SiteSearches.AsNoTracking();
        return LookupValuesAsync(
            rows.Select(site => site.Contracts)
                .Concat(rows.Select(site => site.SiteName))
                .Concat(rows.Select(site => site.CompanyName))
                .Concat(rows.Select(site => site.SiteAddress)),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns combined monitor, site, and contract lookup suggestions.
    public Task<List<string>> MonitorsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        IQueryable<MonitorSearch> rows = _searchContext.MonitorSearches.AsNoTracking();
        return LookupValuesAsync(
            rows.Select(monitor => monitor.ContractNumber)
                .Concat(rows.Select(monitor => monitor.FleetNr))
                .Concat(rows.Select(monitor => monitor.SiteName)),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns active unassigned monitor serial suggestions.
    public Task<List<string>> MonitorsNewSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        return LookupValuesAsync(
            _searchContext.MonitorSearches
                .AsNoTracking()
                .Where(monitor => monitor.FleetNr == null && (monitor.Active ?? false))
                .Select(monitor => monitor.SerialId),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns active online monitor fleet-number suggestions.
    public Task<List<string>> MonitorsOnlineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        return LookupValuesAsync(
            _searchContext.MonitorSearches
                .AsNoTracking()
                .Where(monitor => !(monitor.OffLine ?? true) && (monitor.Active ?? false))
                .Select(monitor => monitor.FleetNr),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns active offline monitor fleet-number suggestions.
    public Task<List<string>> MonitorsOfflineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        return LookupValuesAsync(
            _searchContext.MonitorSearches
                .AsNoTracking()
                .Where(monitor => (monitor.OffLine ?? true) && (monitor.Active ?? false))
                .Select(monitor => monitor.FleetNr),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns unscoped user lookup suggestions.
    public Task<List<string>> UserSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
    {
        IQueryable<UserSearch> rows = _searchContext.UserSearches.AsNoTracking();
        return LookupValuesAsync(
            rows.Select(user => user.Email)
                .Concat(rows.Select(user => user.Name))
                .Concat(rows.Select(user => user.CompanyName)),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Returns company-scoped user lookup suggestions, optionally including RVT administrators.
    public Task<List<string>> UserSearchAsync(Guid companyId, string searchString, int take, bool includeAdmin = false, CancellationToken cancellationToken = default)
    {
        IQueryable<UserSearch> rows = _searchContext.UserSearches.AsNoTracking();
        rows = includeAdmin
            ? rows.Where(user => user.CompanyId == companyId || user.Role == "RVTMasterAdmin" || user.Role == "RVTAdmin")
            : rows.Where(user => user.CompanyId == companyId);

        return LookupValuesAsync(
            rows.Select(user => user.Email)
                .Concat(rows.Select(user => user.Name))
                .Concat(rows.Select(user => user.CompanyName)),
            searchString,
            take,
            cancellationToken);
    }

    // Function summary: Applies lookup text filtering, de-duplication, and result limits in the database.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static Task<List<string>> LookupValuesAsync(
        IQueryable<string?> values,
        string searchString,
        int take,
        CancellationToken cancellationToken)
    {
        IQueryable<string?> query = values.Where(value => value != null && value != "");
        string normalizedSearch = NormalizeSearch(searchString);
        if (normalizedSearch.Length > 0)
        {
            query = query.Where(value => value!.ToLower().Contains(normalizedSearch));
        }

        return query
            .Select(value => value!)
            .Distinct()
            .Take(NormalizeTake(take))
            .ToListAsync(cancellationToken);
    }

    // Function summary: Normalizes caller-supplied lookup limits to prevent unbounded suggestion queries.
    private static int NormalizeTake(int take)
    {
        return take <= 0 ? DefaultTake : Math.Min(take, MaximumTake);
    }

    // Function summary: Normalizes lookup text into a provider-neutral form that EF can translate.
    private static string NormalizeSearch(string? searchString)
    {
        return (searchString ?? string.Empty).Trim().ToLowerInvariant();
    }
}

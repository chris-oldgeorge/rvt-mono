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
using RVT.Entities;
using Monitor = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Application.Lookups
{
    public interface ILookupService
    {
        Task<List<string>> CompaniesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<ContractSearch>> ContractsForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default);
        Task<List<SiteSearch>> SitesForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default);
        Task<List<ContractSearch>> ContractsAsync(int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> ContractsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<Monitor>> MonitorsNotDeployedAsync(int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsAvailableSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsOnlineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsNewSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsOfflineSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsForSiteSearchAsync(Guid siteId, string searchString, int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorsForContractSearchAsync(Guid siteId, string searchString, int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> MonitorUserSearchAsync(Guid userId, string searchString, int take = 50, CancellationToken cancellationToken = default);
        Task<List<Site>> SitesListAsync(int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> SitesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<string>> SiteUserSearchAsync(Guid userId, string searchString, int take = 50, CancellationToken cancellationToken = default);
        Task<List<string>> UserSearchAsync(Guid companyId, string searchString, int take, bool includeAdmin = false, CancellationToken cancellationToken = default);
        Task<List<string>> UserSearchAsync(string searchString, int take, CancellationToken cancellationToken = default);
        Task<List<UserSearch>> UsersForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default);
        Task<string?> CompanyNameFromIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    }

    public class LookupService : ILookupService
    {
        private const int DefaultTake = 20;
        private const int MaximumTake = 50;

        private readonly RVTDbContext dbContext;
        private readonly RVTSearchContext searchContext;

        // Function summary: Initializes lookup queries with scoped EF contexts so searches execute in the database.
        public LookupService(RVTDbContext dbContext, RVTSearchContext searchContext)
        {
            this.dbContext = dbContext;
            this.searchContext = searchContext;
        }

        // Function summary: Returns company-name lookup suggestions using a bounded domain query.
        public Task<List<string>> CompaniesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            return LookupValuesAsync(
                dbContext.Companies.AsNoTracking().Select(company => company.CompanyName),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns a company name by ID without loading all companies.
        public Task<string?> CompanyNameFromIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return dbContext.Companies
                .AsNoTracking()
                .Where(company => company.Id == companyId)
                .Select(company => company.CompanyName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Function summary: Returns combined contract, site, and company lookup suggestions from the contract search view.
        public Task<List<string>> ContractsSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.ContractSearches.AsNoTracking();
            return LookupValuesAsync(
                rows.Select(contract => contract.ContractNumber)
                    .Concat(rows.Select(contract => contract.CompanyName))
                    .Concat(rows.Select(contract => contract.SiteName)),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns a bounded set of contract search rows.
        public Task<List<ContractSearch>> ContractsAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            return searchContext.ContractSearches
                .AsNoTracking()
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
        }

        // Function summary: Returns contracts for a company that are not already linked to a site.
        public Task<List<ContractSearch>> ContractsForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default)
        {
            return searchContext.ContractSearches
                .AsNoTracking()
                .Where(contract => contract.CompanyId == companyId && contract.SiteiD == null)
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
        }

        // Function summary: Returns site-search rows visible for company selection.
        public Task<List<SiteSearch>> SitesForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default)
        {
            return searchContext.SiteSearches
                .AsNoTracking()
                .Where(site => site.CompanyId == companyId || site.CompanyId == null)
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
        }

        // Function summary: Returns combined site, company, contract, and address lookup suggestions.
        public Task<List<string>> SitesSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.SiteSearches.AsNoTracking();
            return LookupValuesAsync(
                rows.Select(site => site.Contracts)
                    .Concat(rows.Select(site => site.SiteName))
                    .Concat(rows.Select(site => site.CompanyName))
                    .Concat(rows.Select(site => site.SiteAddress)),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns site lookup suggestions scoped to a user.
        public Task<List<string>> SiteUserSearchAsync(Guid userId, string searchString, int take = 50, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.SiteUserSearches
                .AsNoTracking()
                .Where(site => site.UserId == userId);
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
            var rows = searchContext.MonitorSearches.AsNoTracking();
            return LookupValuesAsync(
                rows.Select(monitor => monitor.ContractNumber)
                    .Concat(rows.Select(monitor => monitor.FleetNr))
                    .Concat(rows.Select(monitor => monitor.SiteName)),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns available monitor fleet-number suggestions.
        public Task<List<string>> MonitorsAvailableSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            return LookupValuesAsync(
                searchContext.MonitorCurrentSearches
                    .AsNoTracking()
                    .Where(monitor => monitor.ContractNumber == null)
                    .Select(monitor => monitor.FleetNr),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns active unassigned monitor serial suggestions.
        public Task<List<string>> MonitorsNewSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            return LookupValuesAsync(
                searchContext.MonitorSearches
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
                searchContext.MonitorSearches
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
                searchContext.MonitorSearches
                    .AsNoTracking()
                    .Where(monitor => (monitor.OffLine ?? true) && (monitor.Active ?? false))
                    .Select(monitor => monitor.FleetNr),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns monitor and contract suggestions scoped to a site.
        public Task<List<string>> MonitorsForSiteSearchAsync(Guid siteId, string searchString, int take = 50, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.MonitorSearches
                .AsNoTracking()
                .Where(monitor => monitor.SiteiD == siteId);
            return LookupValuesAsync(
                rows.Select(monitor => monitor.ContractNumber)
                    .Concat(rows.Select(monitor => monitor.FleetNr)),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns monitor suggestions for contract-related lookup consumers.
        public Task<List<string>> MonitorsForContractSearchAsync(Guid siteId, string searchString, int take = 50, CancellationToken cancellationToken = default)
        {
            _ = siteId;
            return LookupValuesAsync(
                searchContext.MonitorSearches
                    .AsNoTracking()
                    .Select(monitor => monitor.FleetNr),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns monitor lookup suggestions scoped to a user.
        public Task<List<string>> MonitorUserSearchAsync(Guid userId, string searchString, int take = 50, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.MonitorUserSearches
                .AsNoTracking()
                .Where(monitor => monitor.UserId == userId);
            return LookupValuesAsync(
                rows.Select(monitor => monitor.ContractNumber)
                    .Concat(rows.Select(monitor => monitor.FleetNr))
                    .Concat(rows.Select(monitor => monitor.SiteName)),
                searchString,
                take,
                cancellationToken);
        }

        // Function summary: Returns users for a company using a bounded search-view query.
        public Task<List<UserSearch>> UsersForCompanyAsync(Guid companyId, int take = 50, CancellationToken cancellationToken = default)
        {
            return searchContext.UserSearches
                .AsNoTracking()
                .Where(user => user.CompanyId == companyId)
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
        }

        // Function summary: Returns unscoped user lookup suggestions.
        public Task<List<string>> UserSearchAsync(string searchString, int take, CancellationToken cancellationToken = default)
        {
            var rows = searchContext.UserSearches.AsNoTracking();
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
            var rows = searchContext.UserSearches.AsNoTracking();
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

        // Function summary: Returns a bounded list of sites for legacy lookup consumers.
        public Task<List<Site>> SitesListAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            return dbContext.Sites
                .AsNoTracking()
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
        }

        // Function summary: Returns fleet-numbered monitors that are not archived for legacy lookup consumers.
        public Task<List<Monitor>> MonitorsNotDeployedAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            return dbContext.MonitorsList
                .AsNoTracking()
                .Where(monitor => monitor.FleetNr != null && !monitor.Archived)
                .Take(NormalizeTake(take))
                .ToListAsync(cancellationToken);
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
            var query = values.Where(value => value != null && value != "");
            var normalizedSearch = NormalizeSearch(searchString);
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
}

// File summary: Coordinates business-layer operations for company service workflows.
// Major updates:
// - 2026-07-30 pending Folded the single-method company repository hop into a direct domain-context read and made the nullable result honest.
// - 2026-06-25 pending Narrowed local order-by builders to concrete lists for CA1859 cleanup.
// - 2026-06-25 pending Aligned nullable repository results and paging defaults with non-nullable contracts.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-10 pending Removed redundant async/await from repository pass-through service methods.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;

namespace RvtPortal.Spa.Application.Companies;

public interface ICompanyService
{
    Task<Company?> ReadOneAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SearchQueryResult<CompanySearch>> Search(string companyName, int? page, OrderByDirectionEnum sortdir, string sort, int pageSize, CancellationToken cancellationToken = default);
}

public class CompanyService : ICompanyService
{
    private readonly RVTDbContext _domainContext;
    private readonly RVTSearchContext _searchContext;
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public CompanyService(RVTDbContext domainContext, RVTSearchContext searchContext)
    {
        _domainContext = domainContext;
        _searchContext = searchContext;
    }
    // Function summary: Retrieves one company, or null when it does not exist.
    public async Task<Company?> ReadOneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _domainContext.Companies.FindAsync([id], cancellationToken);
    }

    // Function summary: Handles the search workflow for this module.
    public async Task<SearchQueryResult<CompanySearch>> Search(string companyName, int? page, OrderByDirectionEnum sortdir, string sort, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<CompanySearch> companies = _searchContext.CompanySearches.AsNoTracking();
        if (!string.IsNullOrEmpty(companyName))
        {
            companies = companies.Where(company => company.CompanyName.Contains(companyName));
        }

        companies = sortdir == OrderByDirectionEnum.Descending
            ? companies.OrderByDescending(company => company.CompanyName)
            : companies.OrderBy(company => company.CompanyName);

        int recordCount = await companies.CountAsync(cancellationToken);
        int pageNumber = page.GetValueOrDefault() < 1 ? 1 : page.GetValueOrDefault();
        List<CompanySearch> results = await companies
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SearchQueryResult<CompanySearch>(true, string.Empty, results, recordCount, string.Empty);
    }
}

// File summary: Coordinates business-layer operations for company service workflows.
// Major updates:
// - 2026-06-25 pending Narrowed local order-by builders to concrete lists for CA1859 cleanup.
// - 2026-06-25 pending Aligned nullable repository results and paging defaults with non-nullable contracts.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-10 pending Removed redundant async/await from repository pass-through service methods.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Ports.Persistence;
using RVT.Entities.Querying;

namespace RvtPortal.Spa.Application.Companies
{
    public interface ICompanyService
    {
        Task<Company> ReadOneAsync(Guid Id);
        Task<SearchQueryResult<CompanySearch>> Search(string CompanyName, int? page, OrderByDirectionEnum sortdir, string Sort, int PageSize, CancellationToken cancellationToken = default);
    }

    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository companyRepository;
        private readonly RVTSearchContext searchContext;
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public CompanyService(ICompanyRepository companyRepository, RVTSearchContext searchContext)
        {
            this.companyRepository = companyRepository;
            this.searchContext = searchContext;
        }
        // Function summary: Retrieves one data for callers.
        public async Task<Company> ReadOneAsync(Guid Id)
        {
            return (await companyRepository.GetByIdAsync(Id))!;
        }

        // Function summary: Handles the search workflow for this module.
        public async Task<SearchQueryResult<CompanySearch>> Search(string CompanyName, int? page, OrderByDirectionEnum sortdir, string Sort, int PageSize, CancellationToken cancellationToken = default)
        {
            IQueryable<CompanySearch> companies = searchContext.CompanySearches.AsNoTracking();
            if (!string.IsNullOrEmpty(CompanyName))
            {
                companies = companies.Where(company => company.CompanyName.Contains(CompanyName));
            }

            companies = sortdir == OrderByDirectionEnum.Descending
                ? companies.OrderByDescending(company => company.CompanyName)
                : companies.OrderBy(company => company.CompanyName);

            int recordCount = await companies.CountAsync(cancellationToken);
            int pageNumber = page.GetValueOrDefault() < 1 ? 1 : page.GetValueOrDefault();
            List<CompanySearch> results = await companies
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(cancellationToken);

            return new SearchQueryResult<CompanySearch>(true, string.Empty, results, recordCount, string.Empty);
        }
    }
}

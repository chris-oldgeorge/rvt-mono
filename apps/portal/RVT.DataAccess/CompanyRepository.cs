// File summary: Provides data access operations for company repository entities and search projections.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-25 pending Resolved legacy nullable reference warnings.

using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;

namespace RVT.DataAccess
{
    public class CompanyRepository : GenericRepository<Company>, ICompanyRepository
    {
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public CompanyRepository(RVTDbContext ContextDB)
            : base(ContextDB)
        {
        }
        // Function summary: Retrieves filtered data for callers.
        public Task<SearchQueryResult<Company>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default)
        {
            return ReadFilteredAsync(whereFilter, orderBy, maximumRecords, pagedata.paged, pagedata.page, pagedata.pageSize, cancellationToken);
        }

    }
}

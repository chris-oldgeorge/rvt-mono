// File summary: Provides data access operations for monitor repository entities and search projections.
// Major updates:
// - 2026-07-09 pending Read canonical PostgreSQL routine result aliases with legacy fallback.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added relational database support.
// - 2026-06-09 pending Documented PostgreSQL canonical routine-name mapping for monitor status calls.

using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;
using Monitor = RVT.Entities.Monitor;

namespace RVT.DataAccess;

public class MonitorRepository : GenericRepository<Monitor>, IMonitorRepository
{

    // Function summary: Handles the monitor repository workflow for this module.
    public MonitorRepository(RVTDbContext contextDb)
        : base(contextDb)
    {
    }

    // Function summary: Retrieves filtered data for callers.
    public Task<SearchQueryResult<Monitor>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default)
    {
        return ReadFilteredAsync(whereFilter, orderBy, maximumRecords, pagedata.paged, pagedata.page, pagedata.pageSize, cancellationToken);
    }
}

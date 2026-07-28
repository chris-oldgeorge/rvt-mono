// File summary: Driven (outbound) persistence port for monitor access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the monitor repository contract out of the EF adapter into the core ports.

using RVT.Entities.Querying;

namespace RVT.Entities.Ports.Persistence;

public interface IMonitorRepository
{
    Task<Monitor?> GetByIdAsync(Guid Id);
    Task<IList<Monitor>> ReadAllAsync();
    Task<SearchQueryResult<Monitor>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default);
}

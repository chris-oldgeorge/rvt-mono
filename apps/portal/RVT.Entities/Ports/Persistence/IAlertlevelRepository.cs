// File summary: Driven (outbound) persistence port for alert-level access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the alert-level repository contract out of the EF adapter into the core ports.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.Entities.Ports.Persistence
{
    public interface IAlertlevelRepository
    {
        Task<Alertlevel?> GetByIdAsync(Guid Id);
        Task<IList<Alertlevel>> ReadAllAsync();
        Task<IList<Alertlevel>> ReadAllForMonitorAsync(Guid MonitorId);

        Task<SearchQueryResult<Alertlevel>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default);
    }
}

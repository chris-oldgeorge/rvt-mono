// File summary: Driven (outbound) persistence port for deployment access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the deployment repository contract out of the EF adapter into the core ports.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.Entities.Ports.Persistence
{
    public interface IDeploymentRepository
    {
        Task<Deployment?> GetByIdAsync(Guid Id);
        Task<IList<Deployment>> ReadAllAsync();
        Task<SearchQueryResult<Deployment>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default);
        Task<Deployment?> ReadCurrentForMonitiorAsync(Guid MonitorId);
        Task<Deployment?> ReadCurrentForMonitiorAsync(Guid MonitorId, DateTime notificationTime);
    }
}

// File summary: Driven (outbound) persistence port for monitor access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the monitor repository contract out of the EF adapter into the core ports.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.DTO;
using RVT.Entities.Querying;
using Monitor = RVT.Entities.Monitor;

namespace RVT.Entities.Ports.Persistence
{
    public interface IMonitorRepository
    {
        Task<Monitor?> GetByIdAsync(Guid Id);
        Task<IList<Monitor>> ReadAllAsync();
        Task<SearchQueryResult<Monitor>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default);
        Task<MonitorStatusTimeCheckDto> MonitorStatusTimeCheck(Guid MonitorId);
        Task<List<MonitorStatusForMonthDto>> MonitorStatusForMonth(Guid MonitorId, int Year, int Month);
    }
}

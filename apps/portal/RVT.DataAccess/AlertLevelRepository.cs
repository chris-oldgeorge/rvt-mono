// File summary: Provides data access operations for alert level repository entities and search projections.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.DataAccess
{
    public class AlertlevelRepository : GenericRepository<Alertlevel>, IAlertlevelRepository
    {
        // Function summary: Handles the alertlevel repository workflow for this module.
        public AlertlevelRepository(RVTDbContext ContextDB)
            : base(ContextDB)
        {
        }
        // Function summary: Retrieves all for monitor data for callers.
        public async Task<IList<Alertlevel>> ReadAllForMonitorAsync(Guid MonitorId)
        {
            return await this.DbSet.Where(s => s.MonitorId == MonitorId && !s.IsDeleted).ToListAsync();
        }

        // Function summary: Retrieves filtered data for callers.
        public Task<SearchQueryResult<Alertlevel>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, RVT.Entities.Paging pagedata, CancellationToken cancellationToken = default)
        {
            return base.ReadFilteredAsync(whereFilter, orderBy, maximumRecords, pagedata.paged, pagedata.page, pagedata.pageSize, cancellationToken);
        }


    }
}

// File summary: Driven (outbound) persistence port for company access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the company repository contract out of the EF adapter into the core ports.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.Entities.Ports.Persistence
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid Id);
        Task<Company> GetByIdWithContractsAsync(Guid Id);
        Task<IList<Company>> ReadAllAsync();
        Task<SearchQueryResult<Company>> ReadFilteredAsync(List<Filter> whereFilter, OrderByProperty[] orderBy, int maximumRecords, Paging pagedata, CancellationToken cancellationToken = default);
    }
}

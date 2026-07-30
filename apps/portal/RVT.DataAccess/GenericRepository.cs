// File summary: Provides data access operations for generic repository entities and search projections.
// Major updates:
// - 2026-06-29 pending Replaced generic static max-records field with a constant for Sonar maintainability.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-10 pending Removed stale commented-out repository methods for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-04 pending Resolved SonarCloud blocker by implementing IDisposable explicitly.
// - 2026-06-25 pending Resolved legacy nullable reference warnings.
// - 2026-06-25 pending Constructor-injected DbContext, cached DbSet, and removed dirty-read/reflection mapping helpers.
// - 2026-06-26 pending Awaited EF Core save operations in async repository methods for Sonar reliability.
// - 2026-06-26 pending Removed repository disposal of DI-owned DbContext instances.

using Microsoft.EntityFrameworkCore;
using RVT.Entities.Querying;

namespace RVT.DataAccess;

public class GenericRepository<TEntity> where TEntity : class
{

    public const int DAO_MAX_RECORDS = 10000;
    protected DbContext Context { get; }
    protected DbSet<TEntity> DbSet { get; }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    protected GenericRepository(DbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<TEntity>();
    }

    #region Regular Members
    // Function summary: Retrieves filtered data for callers.
    internal Task<SearchQueryResult<TEntity>> ReadFilteredAsync(
        List<Filter> whereFilter,
        OrderByProperty[] orderBy,
        int maximumRecords,
        bool paged,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return SearchQueryExecutor.ReadFilteredAsync<TEntity>(
            Context, whereFilter, orderBy, maximumRecords, paged, page, pageSize, cancellationToken);
    }
    #endregion

    #region Async Members
    // Function summary: Retrieves by ID data for callers.
    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }
    #endregion

}

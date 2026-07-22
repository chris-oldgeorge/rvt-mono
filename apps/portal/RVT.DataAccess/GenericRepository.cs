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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

using RVT.Entities.Querying;

using RVT.Entities;

namespace RVT.DataAccess
{
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
        // Function summary: Retrieves all data for callers.
        public virtual async Task<IList<TEntity>> ReadAllAsync()
        {
            return await this.DbSet.AsNoTracking().ToListAsync();
        }

        // Function summary: Retrieves by ID data for callers.
        public virtual async Task<TEntity?> GetByIdAsync(Guid id)
        {
            return await DbSet.FindAsync(id);
        }
        // Function summary: Retrieves by ID data for callers.
        public virtual async Task<TEntity?> GetByIdAsync(Guid id, string includeProperties)
        {
            List<Filter> query = new List<Filter> { new SingleFilter { Operation = Op.Equals, PropertyName = "Id", Value = id } };

            var filt = FilterExpression.ExpressionBuilder.GetExpression<TEntity>(query);

            // FirstOrDefaultAsync, not FirstAsync: a missing row is a null result for the caller to handle,
            // not an InvalidOperationException thrown from inside the data-access layer.
            return await DbSet.Include(includeProperties).Where(filt).FirstOrDefaultAsync();
        }
        #endregion

    }
}

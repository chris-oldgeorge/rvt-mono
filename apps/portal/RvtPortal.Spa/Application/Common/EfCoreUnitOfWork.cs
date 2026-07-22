// File summary: Implements the application Unit of Work abstraction using the portal's coordinated EF Core contexts.
// Major updates:
// - 2026-06-25 pending Added EF Core transaction coordination for MediatR command handlers.
// - 2026-06-26 pending Included RVTSearchContext persistence for transactional command handlers.
// - 2026-07-08 pending Included ASP.NET Identity context enlistment so user/domain/search writes share one boundary.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Common;

public sealed class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext applicationContext;
    private readonly RVTDbContext domainContext;
    private readonly RVTSearchContext searchContext;

    // Function summary: Initializes the EF Core-backed Unit of Work for domain, search, and Identity writes.
    public EfCoreUnitOfWork(
        RVTDbContext domainContext,
        RVTSearchContext searchContext,
        ApplicationDbContext applicationContext)
    {
        this.domainContext = domainContext;
        this.searchContext = searchContext;
        this.applicationContext = applicationContext;
    }

    // Function summary: Persists all tracked domain, search, and Identity changes through the shared EF contexts.
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var domainChanges = await domainContext.SaveChangesAsync(cancellationToken);
        var searchChanges = await searchContext.SaveChangesAsync(cancellationToken);
        var applicationChanges = await applicationContext.SaveChangesAsync(cancellationToken);
        return domainChanges + searchChanges + applicationChanges;
    }

    // Function summary: Runs the supplied operation in one EF transaction when all configured providers support it.
    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        // Only the non-relational test provider (InMemory) can reach this branch: it has no transaction
        // support at all, so there is nothing to enlist and the operation runs unwrapped.
        if (!SupportsTransactions())
        {
            return await operation(cancellationToken);
        }

        EnsureSharedConnection();

        // A caller already owns a transaction. Enlist any context that is not in it yet, rather than
        // running the multi-context save outside a transaction boundary (which would allow a partial
        // commit if a later context's SaveChanges failed).
        if (HasActiveTransaction())
        {
            return await ExecuteInAmbientTransactionAsync(operation, cancellationToken);
        }

        // The retry execution strategy forbids user-initiated transactions unless the whole
        // begin/commit block is run through it, so the transaction lives inside ExecuteAsync.
        var strategy = domainContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(
            async executionToken =>
            {
                // On a retry the previous attempt's writes were rolled back, but EF's change trackers still hold
                // everything it staged as Added/Modified. Re-running the handler would stage it a second time and
                // insert duplicates. Reset the trackers before re-running - the first attempt is left untouched,
                // and the handler re-reads whatever it needs inside the operation.
                if (attempt++ > 0)
                {
                    domainContext.ChangeTracker.Clear();
                    searchContext.ChangeTracker.Clear();
                    applicationContext.ChangeTracker.Clear();
                }

                await using var transaction = await domainContext.Database.BeginTransactionAsync(executionToken);
                await using var searchTransaction = await searchContext.Database.UseTransactionAsync(
                    transaction.GetDbTransaction(),
                    executionToken);
                await using var applicationTransaction = await applicationContext.Database.UseTransactionAsync(
                    transaction.GetDbTransaction(),
                    executionToken);
                try
                {
                    var response = await operation(executionToken);

                    // A handler signals failure by returning a result (not throwing); committing its staged
                    // writes anyway is how a partial delete/update gets persisted. Roll back instead.
                    if (response is ITransactionOutcome { ShouldCommit: false })
                    {
                        await transaction.RollbackAsync(executionToken);
                    }
                    else
                    {
                        await transaction.CommitAsync(executionToken);
                    }

                    return response;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }

    // Function summary: Enlists any not-yet-enlisted context in the caller's transaction and runs the operation.
    private async Task<TResponse> ExecuteInAmbientTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        var ambient = domainContext.Database.CurrentTransaction
            ?? searchContext.Database.CurrentTransaction
            ?? applicationContext.Database.CurrentTransaction;
        var ambientTransaction = ambient!.GetDbTransaction();

        // Commit/rollback stays with whoever opened the transaction; we only widen its reach.
        await using var domainEnlistment = await EnlistAsync(domainContext, ambientTransaction, cancellationToken);
        await using var searchEnlistment = await EnlistAsync(searchContext, ambientTransaction, cancellationToken);
        await using var applicationEnlistment = await EnlistAsync(applicationContext, ambientTransaction, cancellationToken);

        var response = await operation(cancellationToken);

        // Commit/rollback of an ambient transaction belongs to whoever opened it, so this method cannot roll it
        // back on a should-not-commit result. That path is currently unreachable - no command handler sends
        // another transactional command, so nothing runs inside a pre-existing transaction. If nesting is ever
        // introduced, fail loudly here rather than let the outer boundary commit a partial write.
        if (response is ITransactionOutcome { ShouldCommit: false })
        {
            throw new InvalidOperationException(
                "A transactional command returned a should-not-commit result while running inside a caller-owned " +
                "transaction. Nested transactional commands are not supported; the outer transaction would " +
                "otherwise commit the partial write. See EfCoreUnitOfWork.ExecuteInAmbientTransactionAsync.");
        }

        return response;
    }

    // Function summary: Enlists one context in an existing transaction, or does nothing if it is already enlisted.
    private static async Task<IDbContextTransaction?> EnlistAsync(
        DbContext context,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await context.Database.UseTransactionAsync(transaction, cancellationToken);
    }

    // Function summary: Detects EF providers that can safely open explicit database transactions.
    private bool SupportsTransactions()
    {
        return SupportsTransactions(domainContext) &&
            SupportsTransactions(searchContext) &&
            SupportsTransactions(applicationContext);
    }

    // Function summary: Detects whether any coordinated context is already inside a caller-owned transaction.
    private bool HasActiveTransaction()
    {
        return domainContext.Database.CurrentTransaction != null ||
            searchContext.Database.CurrentTransaction != null ||
            applicationContext.Database.CurrentTransaction != null;
    }

    // Function summary: Asserts the shared-connection invariant that cross-context transaction enlistment requires.
    private void EnsureSharedConnection()
    {
        var connection = domainContext.Database.GetDbConnection();
        if (ReferenceEquals(connection, searchContext.Database.GetDbConnection()) &&
            ReferenceEquals(connection, applicationContext.Database.GetDbConnection()))
        {
            return;
        }

        throw new InvalidOperationException(
            "RVTDbContext, RVTSearchContext, and ApplicationDbContext must share one scoped DbConnection so that " +
            "domain, search, and Identity writes can enlist in a single transaction. Check ConfigureDatabases in Program.cs.");
    }

    // Function summary: Detects transaction support for one EF context.
    private static bool SupportsTransactions(DbContext context)
    {
        return context.Database.IsRelational() &&
            !string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal);
    }
}

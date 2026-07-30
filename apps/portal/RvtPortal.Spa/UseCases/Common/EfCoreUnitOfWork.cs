// File summary: Implements the application Unit of Work abstraction using the portal's coordinated EF Core contexts.
// Major updates:
// - 2026-07-30 pending Removed the non-relational (InMemory) bypass once the Spa test host moved onto PostgreSQL.
// - 2026-07-25 pending Preserved primary transaction failures across rollback and reverse-order disposal faults.
// - 2026-06-25 pending Added EF Core transaction coordination for MediatR command handlers.
// - 2026-06-26 pending Included RVTSearchContext persistence for transactional command handlers.
// - 2026-07-08 pending Included ASP.NET Identity context enlistment so user/domain/search writes share one boundary.

using System.Data.Common;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.UseCases.Common;

public sealed class EfCoreUnitOfWork :
    IUnitOfWork,
    Application.Common.IApplicationUnitOfWork
{
    internal const string SecondaryTransactionFailuresDataKey =
        "RvtPortal.Spa.EfCoreUnitOfWork.SecondaryTransactionFailures";

    private readonly ApplicationDbContext _applicationContext;
    private readonly RVTDbContext _domainContext;
    private readonly RVTSearchContext _searchContext;

    // Function summary: Initializes the EF Core-backed Unit of Work for domain, search, and Identity writes.
    public EfCoreUnitOfWork(
        RVTDbContext domainContext,
        RVTSearchContext searchContext,
        ApplicationDbContext applicationContext)
    {
        _domainContext = domainContext;
        _searchContext = searchContext;
        _applicationContext = applicationContext;
    }

    // Function summary: Persists all tracked domain, search, and Identity changes through the shared EF contexts.
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        int domainChanges = await _domainContext.SaveChangesAsync(cancellationToken);
        int searchChanges = await _searchContext.SaveChangesAsync(cancellationToken);
        int applicationChanges = await _applicationContext.SaveChangesAsync(cancellationToken);
        return domainChanges + searchChanges + applicationChanges;
    }

    // Function summary: Runs the supplied operation in one EF transaction spanning all three contexts.
    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
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
        IExecutionStrategy strategy = _domainContext.Database.CreateExecutionStrategy();
        int attempt = 0;
        return await strategy.ExecuteAsync(
            async executionToken =>
            {
                // On a retry the previous attempt's writes were rolled back, but EF's change trackers still hold
                // everything it staged as Added/Modified. Re-running the handler would stage it a second time and
                // insert duplicates. Reset the trackers before re-running - the first attempt is left untouched,
                // and the handler re-reads whatever it needs inside the operation.
                if (attempt++ > 0)
                {
                    _domainContext.ChangeTracker.Clear();
                    _searchContext.ChangeTracker.Clear();
                    _applicationContext.ChangeTracker.Clear();
                }

                IDbContextTransaction? domainTransaction = null;
                IDbContextTransaction? searchTransaction = null;
                IDbContextTransaction? applicationTransaction = null;
                ExceptionDispatchInfo? primaryFailure = null;
                List<Exception>? secondaryFailures = null;
                TResponse? response = default!;

                try
                {
                    domainTransaction = await _domainContext.Database
                        .BeginTransactionAsync(executionToken);
                    searchTransaction = await _searchContext.Database
                        .UseTransactionAsync(
                            domainTransaction.GetDbTransaction(),
                            executionToken);
                    applicationTransaction = await _applicationContext.Database
                        .UseTransactionAsync(
                            domainTransaction.GetDbTransaction(),
                            executionToken);
                    response = await operation(executionToken);

                    // A handler signals failure by returning a result (not throwing); committing its staged
                    // writes anyway is how a partial delete/update gets persisted. Roll back instead.
                    if (response is ITransactionOutcome { ShouldCommit: false })
                    {
                        await domainTransaction.RollbackAsync(executionToken);
                    }
                    else
                    {
                        await domainTransaction.CommitAsync(executionToken);
                    }
                }
                catch (Exception exception)
                {
                    // Rollback and wrapper cleanup are best-effort diagnostics once an operation or commit has
                    // failed. They must never change the exception identity/type the execution strategy sees.
                    primaryFailure = ExceptionDispatchInfo.Capture(exception);
                    if (domainTransaction is not null)
                    {
                        try
                        {
                            await domainTransaction.RollbackAsync(
                                CancellationToken.None);
                        }
                        catch (Exception rollbackFailure)
                        {
                            (secondaryFailures ??= []).Add(rollbackFailure);
                        }
                    }
                }

                // Dispose every wrapper in the reverse order in which it was created, even if one dispose fails.
                await DisposeTransactionAsync(applicationTransaction);
                await DisposeTransactionAsync(searchTransaction);
                await DisposeTransactionAsync(domainTransaction);

                if (primaryFailure is not null)
                {
                    if (secondaryFailures is { Count: > 0 })
                    {
                        TryAttachSecondaryFailures(
                            primaryFailure.SourceException,
                            secondaryFailures);
                    }

                    primaryFailure.Throw();
                }

                return response;

                async Task DisposeTransactionAsync(
                    IDbContextTransaction? transaction)
                {
                    if (transaction is null)
                    {
                        return;
                    }

                    try
                    {
                        await transaction.DisposeAsync();
                    }
                    catch (Exception disposeFailure)
                    {
                        if (primaryFailure is null)
                        {
                            // A disposal failure is primary only when the operation and commit/rollback path
                            // completed without another exception. Capture it before continuing reverse cleanup.
                            primaryFailure =
                                ExceptionDispatchInfo.Capture(disposeFailure);
                        }
                        else
                        {
                            (secondaryFailures ??= []).Add(disposeFailure);
                        }
                    }
                }
            },
            cancellationToken);
    }

    // Function summary: Retains cleanup faults when the primary exception supports diagnostics without risking replacement.
    private static void TryAttachSecondaryFailures(
        Exception primaryFailure,
        IReadOnlyCollection<Exception> secondaryFailures)
    {
        try
        {
            primaryFailure.Data[SecondaryTransactionFailuresDataKey] =
                new AggregateException(
                    "Secondary failures occurred while rolling back or disposing the coordinated transaction.",
                    secondaryFailures);
        }
        catch (Exception)
        {
            // Exception.Data is virtual and may reject reads or writes. Diagnostics are best-effort only;
            // preserving the original operation/commit exception remains the correctness boundary.
        }
    }

    // Function summary: Enlists any not-yet-enlisted context in the caller's transaction and runs the operation.
    private async Task<TResponse> ExecuteInAmbientTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? ambient = _domainContext.Database.CurrentTransaction
            ?? _searchContext.Database.CurrentTransaction
            ?? _applicationContext.Database.CurrentTransaction;
        DbTransaction ambientTransaction = ambient!.GetDbTransaction();
        IDbContextTransaction? domainEnlistment = null;
        IDbContextTransaction? searchEnlistment = null;
        IDbContextTransaction? applicationEnlistment = null;
        ExceptionDispatchInfo? primaryFailure = null;
        List<Exception>? secondaryFailures = null;
        TResponse? response = default!;

        try
        {
            // Commit/rollback stays with whoever opened the transaction; we only widen its reach.
            domainEnlistment = await EnlistAsync(
                _domainContext,
                ambientTransaction,
                cancellationToken);
            searchEnlistment = await EnlistAsync(
                _searchContext,
                ambientTransaction,
                cancellationToken);
            applicationEnlistment = await EnlistAsync(
                _applicationContext,
                ambientTransaction,
                cancellationToken);
            response = await operation(cancellationToken);

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
        }
        catch (Exception exception)
        {
            primaryFailure = ExceptionDispatchInfo.Capture(exception);
        }

        await DisposeEnlistmentAsync(applicationEnlistment);
        await DisposeEnlistmentAsync(searchEnlistment);
        await DisposeEnlistmentAsync(domainEnlistment);

        if (primaryFailure is not null)
        {
            if (secondaryFailures is { Count: > 0 })
            {
                TryAttachSecondaryFailures(
                    primaryFailure.SourceException,
                    secondaryFailures);
            }

            primaryFailure.Throw();
        }

        return response;

        async Task DisposeEnlistmentAsync(
            IDbContextTransaction? enlistment)
        {
            if (enlistment is null)
            {
                return;
            }

            try
            {
                await enlistment.DisposeAsync();
            }
            catch (Exception disposeFailure)
            {
                if (primaryFailure is null)
                {
                    primaryFailure =
                        ExceptionDispatchInfo.Capture(disposeFailure);
                }
                else
                {
                    (secondaryFailures ??= []).Add(disposeFailure);
                }
            }
        }
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

    // Function summary: Detects whether any coordinated context is already inside a caller-owned transaction.
    private bool HasActiveTransaction()
    {
        return _domainContext.Database.CurrentTransaction != null ||
            _searchContext.Database.CurrentTransaction != null ||
            _applicationContext.Database.CurrentTransaction != null;
    }

    // Function summary: Asserts the shared-connection invariant that cross-context transaction enlistment requires.
    private void EnsureSharedConnection()
    {
        DbConnection connection = _domainContext.Database.GetDbConnection();
        if (ReferenceEquals(connection, _searchContext.Database.GetDbConnection()) &&
            ReferenceEquals(connection, _applicationContext.Database.GetDbConnection()))
        {
            return;
        }

        throw new InvalidOperationException(
            "RVTDbContext, RVTSearchContext, and ApplicationDbContext must share one scoped DbConnection so that " +
            "domain, search, and Identity writes can enlist in a single transaction. Check ConfigureDatabases in Program.cs.");
    }
}

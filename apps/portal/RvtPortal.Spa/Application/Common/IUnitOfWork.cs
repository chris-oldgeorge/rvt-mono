// File summary: Defines transaction-aware application abstractions used by MediatR command handlers.
// Major updates:
// - 2026-06-25 pending Added Unit of Work and transactional request marker interfaces for command pipelines.

namespace RvtPortal.Spa.Application.Common;

public interface ITransactionalRequest
{
}

public interface IUnitOfWork
{
    // Function summary: Runs an operation inside the current persistence transaction boundary.
    Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken);

    // Function summary: Persists all tracked changes for the current command boundary.
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

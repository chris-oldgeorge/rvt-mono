// File summary: Verifies the MediatR transaction pipeline coordinates Unit of Work persistence.
// Major updates:
// - 2026-06-25 pending Added transaction behavior regression tests for command pipeline save and rollback paths.
// - 2026-06-26 pending Added business-readable rollback scenario coverage for RC testability.

using MediatR;
using RvtPortal.Spa.Application.Common;

namespace RvtPortal.Spa.Tests;

public class TransactionPipelineBehaviorTests
{
    [Fact]
    // Function summary: Verifies non-transactional requests bypass Unit of Work persistence.
    public async Task NonTransactionalRequest_BypassesUnitOfWork()
    {
        RecordingUnitOfWork unitOfWork = new();
        TransactionPipelineBehavior<QueryRequest, string> behavior = new(unitOfWork);

        string response = await behavior.Handle(new QueryRequest(), _ => Task.FromResult("query"), CancellationToken.None);

        Assert.Equal("query", response);
        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    // Function summary: Verifies transactional requests save and commit exactly once.
    public async Task TransactionalRequest_SavesAndCommitsOnce()
    {
        RecordingUnitOfWork unitOfWork = new();
        TransactionPipelineBehavior<CommandRequest, string> behavior = new(unitOfWork);

        string response = await behavior.Handle(new CommandRequest(), _ => Task.FromResult("command"), CancellationToken.None);

        Assert.Equal("command", response);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    // Function summary: Verifies handler failures roll back and skip save.
    public async Task TransactionalRequest_RollsBackWhenHandlerFails()
    {
        RecordingUnitOfWork unitOfWork = new();
        TransactionPipelineBehavior<CommandRequest, string> behavior = new(unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new CommandRequest(),
            _ => throw new InvalidOperationException("handler failed"),
            CancellationToken.None));

        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    // Function summary: Verifies save failures roll back the transaction.
    public async Task TransactionalRequest_RollsBackWhenSaveFails()
    {
        RecordingUnitOfWork unitOfWork = new() { ThrowOnSave = true };
        TransactionPipelineBehavior<CommandRequest, string> behavior = new(unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new CommandRequest(),
            _ => Task.FromResult("command"),
            CancellationToken.None));

        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    private sealed record QueryRequest : IRequest<string>;

    private sealed record CommandRequest : IRequest<string>, ITransactionalRequest;

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public int SaveCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public bool ThrowOnSave { get; init; }

        // Function summary: Records a transaction boundary and models commit/rollback behavior.
        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            try
            {
                TResponse? response = await operation(cancellationToken);
                CommitCount++;
                return response;
            }
            catch
            {
                RollbackCount++;
                throw;
            }
        }

        // Function summary: Records save calls and optionally simulates persistence failure.
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("save failed");
            }

            return Task.FromResult(1);
        }
    }
}

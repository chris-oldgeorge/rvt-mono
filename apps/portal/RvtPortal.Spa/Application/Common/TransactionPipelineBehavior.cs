// File summary: Provides the MediatR transaction pipeline used by command handlers that opt into Unit of Work persistence.
// Major updates:
// - 2026-06-25 pending Added transactional pipeline behavior for command handlers implementing ITransactionalRequest.

using MediatR;

namespace RvtPortal.Spa.Application.Common;

public sealed class TransactionPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork unitOfWork;

    // Function summary: Initializes the transaction behavior with the configured Unit of Work.
    public TransactionPipelineBehavior(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    // Function summary: Wraps transactional MediatR requests in one save-and-commit boundary.
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return next(cancellationToken);
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var response = await next(token);
                await unitOfWork.SaveChangesAsync(token);
                return response;
            },
            cancellationToken);
    }
}

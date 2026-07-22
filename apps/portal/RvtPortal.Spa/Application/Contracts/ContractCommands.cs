// File summary: Handles transactional CQRS commands for contract mutation workflows.
// Major updates:
// - 2026-06-26 pending Moved contract create/update/delete writes behind MediatR transactional commands.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;

namespace RvtPortal.Spa.Application.Contracts;

public sealed record CreateContractCommand(ContractMutationRequest Request)
    : IRequest<ContractCommandResult>, ITransactionalRequest;

public sealed record UpdateContractCommand(Guid ContractId, ContractMutationRequest Request)
    : IRequest<ContractCommandResult>, ITransactionalRequest;

public sealed record DeleteContractCommand(Guid ContractId)
    : IRequest<ContractCommandResult>, ITransactionalRequest;

public sealed class ContractCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateContractCommandHandler : IRequestHandler<CreateContractCommand, ContractCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional contract create command handler.
    public CreateContractCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates a contract after validating number, dates, company, and site.
    public async Task<ContractCommandResult> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var result = new ContractCommandResult();
        await ContractCommandWorkflow.ValidateContractAsync(domainContext, request.Request, null, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var contract = ContractCommandWorkflow.CreateContract(request.Request);
        domainContext.Contracts.Add(contract);
        result.ContractId = contract.Id;
        result.ContractNumber = contract.ContractNumber;
        return result;
    }
}

public sealed class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, ContractCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional contract update command handler.
    public UpdateContractCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates contract fields after validating number, dates, company, and site.
    public async Task<ContractCommandResult> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var result = new ContractCommandResult { ContractId = request.ContractId };
        var contract = await domainContext.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, cancellationToken);
        if (contract == null)
        {
            result.NotFound = true;
            return result;
        }

        await ContractCommandWorkflow.ValidateContractAsync(domainContext, request.Request, request.ContractId, result.Errors, cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        ContractCommandWorkflow.ApplyContractMutation(contract, request.Request);
        result.ContractNumber = contract.ContractNumber;
        return result;
    }
}

public sealed class DeleteContractCommandHandler : IRequestHandler<DeleteContractCommand, ContractCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional contract delete command handler.
    public DeleteContractCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Deletes a contract by id.
    public async Task<ContractCommandResult> Handle(DeleteContractCommand request, CancellationToken cancellationToken)
    {
        var result = new ContractCommandResult { ContractId = request.ContractId };
        var contract = await domainContext.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, cancellationToken);
        if (contract == null)
        {
            result.NotFound = true;
            return result;
        }

        result.ContractNumber = contract.ContractNumber;
        domainContext.Contracts.Remove(contract);
        return result;
    }
}

internal static class ContractCommandWorkflow
{
    // Function summary: Validates a contract mutation request.
    public static async Task ValidateContractAsync(
        RVTDbContext domainContext,
        ContractMutationRequest request,
        Guid? currentId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        await ValidateContractNumberAsync(domainContext, request, currentId, errors, cancellationToken);
        ValidateContractDates(request, errors);
        await ValidateCompanyAsync(domainContext, request.CompanyId, errors, cancellationToken);
        await ValidateContractSiteAsync(domainContext, request, currentId, errors, cancellationToken);
    }

    // Function summary: Creates a contract entity from a mutation request.
    public static Contract CreateContract(ContractMutationRequest request)
    {
        return new Contract
        {
            ContractNumber = request.ContractNumber.Trim(),
            CompanyId = request.CompanyId,
            SiteiD = request.SiteId,
            OnHireDate = request.OnHireDate.Date,
            OffHireDate = request.OffHireDate?.Date
        };
    }

    // Function summary: Applies a mutation request to an existing contract.
    public static void ApplyContractMutation(Contract contract, ContractMutationRequest request)
    {
        contract.ContractNumber = request.ContractNumber.Trim();
        contract.CompanyId = request.CompanyId;
        contract.SiteiD = request.SiteId;
        contract.OnHireDate = request.OnHireDate.Date;
        contract.OffHireDate = request.OffHireDate?.Date;
    }

    private static async Task ValidateContractNumberAsync(
        RVTDbContext domainContext,
        ContractMutationRequest request,
        Guid? currentId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var contractNumber = request.ContractNumber?.Trim();
        if (string.IsNullOrWhiteSpace(contractNumber))
        {
            AddError(errors, nameof(ContractMutationRequest.ContractNumber), "The Contract Number field is required.");
            return;
        }

        if (contractNumber.Length > 20)
        {
            AddError(errors, nameof(ContractMutationRequest.ContractNumber), "Contract number must be 20 characters or fewer.");
            return;
        }

        if (await domainContext.Contracts.AnyAsync(
            contract => contract.Id != currentId && contract.ContractNumber == contractNumber,
            cancellationToken))
        {
            AddError(errors, nameof(ContractMutationRequest.ContractNumber), "The Contract Number is already registered");
        }
    }

    private static void ValidateContractDates(ContractMutationRequest request, Dictionary<string, string[]> errors)
    {
        if (request.OnHireDate == default)
        {
            AddError(errors, nameof(ContractMutationRequest.OnHireDate), "On Hire Date is required.");
            return;
        }

        if (request.OffHireDate.HasValue && request.OnHireDate.Date > request.OffHireDate.Value.Date)
        {
            AddError(errors, nameof(ContractMutationRequest.OffHireDate), "OffHireDate must be greater than OnHireDate");
        }
    }

    private static async Task ValidateCompanyAsync(
        RVTDbContext domainContext,
        Guid companyId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (!await domainContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken))
        {
            AddError(errors, nameof(ContractMutationRequest.CompanyId), "Please select a Company.");
        }
    }

    private static async Task ValidateContractSiteAsync(
        RVTDbContext domainContext,
        ContractMutationRequest request,
        Guid? currentId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (!request.SiteId.HasValue)
        {
            return;
        }

        var siteExists = await domainContext.Sites.AnyAsync(site => site.Id == request.SiteId.Value, cancellationToken);
        if (!siteExists)
        {
            AddError(errors, nameof(ContractMutationRequest.SiteId), "Please select a Site.");
            return;
        }

        var conflictingCompany = await domainContext.Contracts.AnyAsync(
            contract => contract.Id != currentId &&
                contract.SiteiD == request.SiteId.Value &&
                contract.CompanyId != request.CompanyId,
            cancellationToken);
        if (conflictingCompany)
        {
            AddError(errors, nameof(ContractMutationRequest.SiteId), "This site already have contract(s) from another Company.");
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}

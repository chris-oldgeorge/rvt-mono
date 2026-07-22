// File summary: Handles transactional CQRS commands for company lifecycle workflows.
// Major updates:
// - 2026-06-26 pending Moved company create/update/delete writes behind MediatR transactional commands.

using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Companies;

public sealed record CreateCompanyCommand(CompanyMutationRequest Request)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;

public sealed record UpdateCompanyCommand(Guid CompanyId, CompanyMutationRequest Request)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;

public sealed record DeleteCompanyCommand(Guid CompanyId)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;

public sealed class CompanyCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, CompanyCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional company create command handler.
    public CreateCompanyCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Creates a company after validating its display name.
    public async Task<CompanyCommandResult> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var result = new CompanyCommandResult();
        var companyName = await CompanyCommandWorkflow.ValidateCompanyNameAsync(
            domainContext,
            request.Request.CompanyName,
            null,
            result.Errors,
            cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        var company = new Company { Id = Guid.NewGuid(), CompanyName = companyName!, Contracts = [] };
        domainContext.Companies.Add(company);
        result.CompanyId = company.Id;
        result.CompanyName = company.CompanyName;
        return result;
    }
}

public sealed class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, CompanyCommandResult>
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes the transactional company update command handler.
    public UpdateCompanyCommandHandler(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    // Function summary: Updates a company name after validating uniqueness.
    public async Task<CompanyCommandResult> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var result = new CompanyCommandResult { CompanyId = request.CompanyId };
        var company = await domainContext.Companies.SingleOrDefaultAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (company == null)
        {
            result.NotFound = true;
            return result;
        }

        var companyName = await CompanyCommandWorkflow.ValidateCompanyNameAsync(
            domainContext,
            request.Request.CompanyName,
            request.CompanyId,
            result.Errors,
            cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        company.CompanyName = companyName!;
        result.CompanyName = company.CompanyName;
        return result;
    }
}

public sealed class DeleteCompanyCommandHandler : IRequestHandler<DeleteCompanyCommand, CompanyCommandResult>
{
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional company delete command handler.
    public DeleteCompanyCommandHandler(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        this.domainContext = domainContext;
        this.userManager = userManager;
    }

    // Function summary: Deletes a company and removes its company-user account data in one transaction.
    public async Task<CompanyCommandResult> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var result = new CompanyCommandResult { CompanyId = request.CompanyId };
        var company = await domainContext.Companies.SingleOrDefaultAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (company == null)
        {
            result.NotFound = true;
            return result;
        }

        var companyUsers = await userManager.Users
            .Where(user => user.CompanyId == request.CompanyId)
            .ToListAsync(cancellationToken);
        var userIds = companyUsers
            .Select(user => Guid.TryParse(user.Id, out var userId) ? userId : (Guid?)null)
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .ToList();
        if (userIds.Count > 0)
        {
            var siteUsers = await domainContext.SiteUsers
                .Where(siteUser => userIds.Contains(siteUser.UserId))
                .ToListAsync(cancellationToken);
            domainContext.SiteUsers.RemoveRange(siteUsers);
        }

        foreach (var user in companyUsers)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                CompanyCommandWorkflow.AddIdentityErrors(result.Errors, deleteResult.Errors);
                return result;
            }
        }

        result.CompanyName = company.CompanyName;
        domainContext.Companies.Remove(company);
        return result;
    }
}

internal static class CompanyCommandWorkflow
{
    // Function summary: Validates and normalizes a company name mutation.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    public static async Task<string?> ValidateCompanyNameAsync(
        RVTDbContext domainContext,
        string? companyName,
        Guid? currentCompanyId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var trimmedName = companyName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            AddError(errors, nameof(CompanyMutationRequest.CompanyName), "Company name is required.");
            return null;
        }

        if (trimmedName.Length > 50)
        {
            AddError(errors, nameof(CompanyMutationRequest.CompanyName), "Company name must be 50 characters or fewer.");
            return null;
        }

        var exists = await domainContext.Companies.AnyAsync(
            company =>
                (!currentCompanyId.HasValue || company.Id != currentCompanyId.Value) &&
                company.CompanyName.ToLower() == trimmedName.ToLower(),
            cancellationToken);
        if (exists)
        {
            AddError(errors, nameof(CompanyMutationRequest.CompanyName), "The Company name is already registered");
            return null;
        }

        return trimmedName;
    }

    public static void AddIdentityErrors(Dictionary<string, string[]> errors, IEnumerable<IdentityError> identityErrors)
    {
        foreach (var error in identityErrors)
        {
            AddError(errors, error.Code, error.Description);
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }
}

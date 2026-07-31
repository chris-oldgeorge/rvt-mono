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
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Common;

namespace RvtPortal.Spa.UseCases.Companies;

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
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the transactional company create command handler.
    public CreateCompanyCommandHandler(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Creates a company after validating its display name.
    public async Task<CompanyCommandResult> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        CompanyCommandResult result = new();
        string? companyName = await CompanyCommandWorkflow.ValidateCompanyNameAsync(
            _domainContext,
            request.Request.CompanyName,
            null,
            result.Errors,
            cancellationToken);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        Company company = new() { Id = Guid.NewGuid(), CompanyName = companyName!, Contracts = [] };
        _domainContext.Companies.Add(company);
        result.CompanyId = company.Id;
        result.CompanyName = company.CompanyName;
        return result;
    }
}

public sealed class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, CompanyCommandResult>
{
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes the transactional company update command handler.
    public UpdateCompanyCommandHandler(RVTDbContext domainContext)
    {
        _domainContext = domainContext;
    }

    // Function summary: Updates a company name after validating uniqueness.
    public async Task<CompanyCommandResult> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        CompanyCommandResult result = new() { CompanyId = request.CompanyId };
        Company? company = await _domainContext.Companies.SingleOrDefaultAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (company == null)
        {
            result.NotFound = true;
            return result;
        }

        string? companyName = await CompanyCommandWorkflow.ValidateCompanyNameAsync(
            _domainContext,
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
    private readonly RVTDbContext _domainContext;
    private readonly UserManager<ApplicationUser> _userManager;

    // Function summary: Initializes the transactional company delete command handler.
    public DeleteCompanyCommandHandler(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        _domainContext = domainContext;
        _userManager = userManager;
    }

    // Function summary: Deletes a company and removes its company-user account data in one transaction.
    public async Task<CompanyCommandResult> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        CompanyCommandResult result = new() { CompanyId = request.CompanyId };
        Company? company = await _domainContext.Companies.SingleOrDefaultAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (company == null)
        {
            result.NotFound = true;
            return result;
        }

        List<ApplicationUser> companyUsers = await _userManager.Users
            .Where(user => user.CompanyId == request.CompanyId)
            .ToListAsync(cancellationToken);
        List<Guid> userIds = [.. companyUsers
            .Select(user => Guid.TryParse(user.Id, out Guid userId) ? userId : (Guid?)null)
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)];
        if (userIds.Count > 0)
        {
            List<SiteUsers> siteUsers = await _domainContext.SiteUsers
                .Where(siteUser => userIds.Contains(siteUser.UserId))
                .ToListAsync(cancellationToken);
            _domainContext.SiteUsers.RemoveRange(siteUsers);
        }

        foreach (ApplicationUser? user in companyUsers)
        {
            IdentityResult deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                CompanyCommandWorkflow.AddIdentityErrors(result.Errors, deleteResult.Errors);
                return result;
            }
        }

        result.CompanyName = company.CompanyName;
        _domainContext.Companies.Remove(company);
        return result;
    }
}

internal static class CompanyCommandWorkflow
{
    // Function summary: Validates and normalizes a company name mutation.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form Npgsql translates - the StringComparison and ToLowerInvariant overloads throw on translation, and this one never executes in .NET. See docs/development/portal/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/globalization-suppressions.md")]
    public static async Task<string?> ValidateCompanyNameAsync(
        RVTDbContext domainContext,
        string? companyName,
        Guid? currentCompanyId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        string? trimmedName = companyName?.Trim();
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

        bool exists = await domainContext.Companies.AnyAsync(
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
        foreach (IdentityError error in identityErrors)
        {
            AddError(errors, error.Code, error.Description);
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }
}

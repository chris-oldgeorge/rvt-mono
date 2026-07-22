// File summary: Handles transactional CQRS commands for admin-managed user account workflows.
// Major updates:
// - 2026-06-26 pending Preserved company assignment for installer accounts used by installer object authorization.
// - 2026-06-26 pending Moved admin user account lifecycle writes behind MediatR transactional commands.

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Users;

public sealed record CreateUserCommand(UserMutationRequest Request)
    : IRequest<UserAccountCommandResult>, ITransactionalRequest;

public sealed record UpdateUserCommand(string UserId, UserMutationRequest Request, string CurrentRole)
    : IRequest<UserAccountCommandResult>, ITransactionalRequest;

public sealed record DisableUserCommand(string UserId)
    : IRequest<UserAccountCommandResult>, ITransactionalRequest;

public sealed record EnableUserCommand(string UserId)
    : IRequest<UserAccountCommandResult>, ITransactionalRequest;

public sealed record DeleteUserCommand(string UserId)
    : IRequest<UserAccountCommandResult>, ITransactionalRequest;

public sealed class UserAccountCommandResult : ITransactionOutcome
{
    public bool NotFound { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public bool ShouldCommit => !NotFound && Errors.Count == 0;
}

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserAccountCommandResult>
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user create command handler.
    public CreateUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    // Function summary: Creates a user account and assigns the requested role.
    public async Task<UserAccountCommandResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var result = new UserAccountCommandResult();
        var user = new ApplicationUser
        {
            Email = request.Request.Email.Trim(),
            UserName = request.Request.Email.Trim(),
            CompanyId = UserAccountCommandWorkflow.RequiresCompanyAssignment(request.Request.Role) ? request.Request.CompanyId : null,
            Name = request.Request.Name?.Trim(),
            PhoneNumber = request.Request.MobilePhone?.Trim(),
            CompanyRole = request.Request.Role == RoleNames.CompanyUser ? request.Request.CompanyRole?.Trim() : null,
            EmailConfirmed = false
        };
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, createResult.Errors);
            return result;
        }

        var roleResult = await userManager.AddToRoleAsync(user, request.Request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, roleResult.Errors);
            return result;
        }

        result.UserId = user.Id;
        result.Email = user.Email;
        return result;
    }
}

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserAccountCommandResult>
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user update command handler.
    public UpdateUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    // Function summary: Updates user account fields and role membership.
    public async Task<UserAccountCommandResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var result = new UserAccountCommandResult { UserId = request.UserId };
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            result.NotFound = true;
            return result;
        }

        if (!string.Equals(request.CurrentRole, request.Request.Role, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(request.CurrentRole))
            {
                var removeResult = await userManager.RemoveFromRoleAsync(user, request.CurrentRole);
                if (!removeResult.Succeeded)
                {
                    UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, removeResult.Errors);
                    return result;
                }
            }

            var roleResult = await userManager.AddToRoleAsync(user, request.Request.Role);
            if (!roleResult.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(request.CurrentRole))
                {
                    await userManager.AddToRoleAsync(user, request.CurrentRole);
                }
                UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, roleResult.Errors);
                return result;
            }
        }

        UserAccountCommandWorkflow.ApplyUserMutation(user, request.Request);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, updateResult.Errors);
            return result;
        }

        result.Email = user.Email;
        return result;
    }
}

public sealed class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, UserAccountCommandResult>
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user disable command handler.
    public DisableUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    // Function summary: Disables a user and refreshes their security stamp.
    public async Task<UserAccountCommandResult> Handle(DisableUserCommand request, CancellationToken cancellationToken)
    {
        var result = new UserAccountCommandResult { UserId = request.UserId };
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            result.NotFound = true;
            return result;
        }

        user.IsDisabled = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, updateResult.Errors);
            return result;
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, stampResult.Errors);
            return result;
        }

        result.Email = user.Email;
        return result;
    }
}

public sealed class EnableUserCommandHandler : IRequestHandler<EnableUserCommand, UserAccountCommandResult>
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user enable command handler.
    public EnableUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    // Function summary: Enables a disabled user account.
    public async Task<UserAccountCommandResult> Handle(EnableUserCommand request, CancellationToken cancellationToken)
    {
        var result = new UserAccountCommandResult { UserId = request.UserId };
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            result.NotFound = true;
            return result;
        }

        user.IsDisabled = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, updateResult.Errors);
            return result;
        }

        result.Email = user.Email;
        return result;
    }
}

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, UserAccountCommandResult>
{
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes the transactional user delete command handler.
    public DeleteUserCommandHandler(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        this.domainContext = domainContext;
        this.userManager = userManager;
    }

    // Function summary: Deletes a user account and removes its site-assignment data atomically.
    public async Task<UserAccountCommandResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var result = new UserAccountCommandResult { UserId = request.UserId };
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            result.NotFound = true;
            return result;
        }

        result.Email = user.Email;
        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            UserAccountCommandWorkflow.AddIdentityErrors(result.Errors, deleteResult.Errors);
            return result;
        }

        if (Guid.TryParse(user.Id, out var userId))
        {
            var siteUsers = await domainContext.SiteUsers
                .Where(siteUser => siteUser.UserId == userId)
                .ToListAsync(cancellationToken);
            domainContext.SiteUsers.RemoveRange(siteUsers);
        }

        return result;
    }
}

internal static class UserAccountCommandWorkflow
{
    // Function summary: Applies mutable user profile fields from an admin mutation request.
    public static void ApplyUserMutation(ApplicationUser user, UserMutationRequest request)
    {
        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.Name = request.Name?.Trim();
        user.PhoneNumber = request.MobilePhone?.Trim();
        user.CompanyRole = request.Role == RoleNames.CompanyUser ? request.CompanyRole?.Trim() : null;
        user.CompanyId = RequiresCompanyAssignment(request.Role) ? request.CompanyId : null;
    }

    public static bool RequiresCompanyAssignment(string role) => role is RoleNames.CompanyUser or RoleNames.RVTInstaller;

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

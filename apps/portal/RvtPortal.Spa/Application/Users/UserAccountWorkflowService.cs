// File summary: Coordinates admin-managed user account lifecycle and site-assignment workflows.
// Major updates:
// - 2026-07-09 pending Moved user validation, role authorization, account links, cache clearing, and write orchestration out of UsersController.
// - 2026-07-22 pending Removed request-origin fallback from admin-managed account notification links.

using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Notifications;
using RvtPortal.Spa.Application.Auth;
using RvtPortal.Spa.Application.Companies;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Users;

public interface IUserAccountWorkflowService
{
    // Function summary: Creates an admin-managed user and sends the initial password-set link when configured.
    Task<UserAccountWorkflowResult> CreateAsync(
        UserMutationRequest request,
        UserListActor actor,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken);

    // Function summary: Updates an admin-managed user after validating role and account rules.
    Task<UserAccountWorkflowResult> UpdateAsync(
        string userId,
        UserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Sends a fresh confirmation/password-set link to an existing user.
    Task<UserAccountMessageResult> ResendConfirmationAsync(
        string userId,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken);

    // Function summary: Sends a password-reset link to an existing user.
    Task<UserAccountMessageResult> SendResetPasswordLinkAsync(
        string userId,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken);

    // Function summary: Disables a user account when the current admin may edit it.
    Task<UserAccountWorkflowResult> DisableAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Enables a user account when the current admin may edit it.
    Task<UserAccountWorkflowResult> EnableAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Deletes a user account when the current admin may delete it.
    Task<UserDeleteWorkflowResult> DeleteAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Adds a user to a site and returns the refreshed assignment model.
    Task<SiteAssignmentWorkflowResult> AddToSiteAsync(
        SiteUserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Marks a site user as the site contact and returns the refreshed assignment model.
    Task<SiteAssignmentWorkflowResult> SetSiteContactAsync(
        SiteUserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Clears a site contact assignment and returns the refreshed assignment model.
    Task<SiteAssignmentWorkflowResult> RemoveSiteContactAsync(
        Guid siteId,
        Guid userId,
        UserListActor actor,
        CancellationToken cancellationToken);

    // Function summary: Removes a user from a site and returns the refreshed assignment model.
    Task<SiteAssignmentWorkflowResult> RemoveFromSiteAsync(
        Guid siteId,
        Guid userId,
        UserListActor actor,
        CancellationToken cancellationToken);
}

public sealed record UserAccountRequestOrigin(string Scheme, string Host, string PathBase);

public class UserAccountMessageResult
{
    public bool NotFound { get; init; }
    public Dictionary<string, string[]> Errors { get; } = [];
}

public sealed class UserAccountWorkflowResult : UserAccountMessageResult
{
    public bool Forbidden { get; init; }
    public string? UserId { get; init; }
    public UserDetailModel? Detail { get; init; }
}

public sealed class UserDeleteWorkflowResult : UserAccountMessageResult
{
    public bool Forbidden { get; init; }
    public string? Email { get; init; }
}

public sealed class SiteAssignmentWorkflowResult
{
    public bool UserNotFound { get; init; }
    public bool SiteNotFound { get; init; }
    public Dictionary<string, string[]> Errors { get; } = [];
    public SiteAssignmentModel? Assignment { get; init; }
}

public interface IUserAccountNotificationService
{
    // Function summary: Sends the password-set email for a newly created or unconfirmed account.
    Task SendPasswordSetAsync(ApplicationUser user, UserAccountRequestOrigin origin);

    // Function summary: Sends the password-reset email for an existing account.
    Task SendPasswordResetAsync(ApplicationUser user, UserAccountRequestOrigin origin);
}

public sealed class UserAccountWorkflowService : IUserAccountWorkflowService
{
    private static readonly string[] RoleOrder =
    [
        RoleNames.RVTMasterAdmin,
        RoleNames.RVTAdmin,
        RoleNames.RVTInstaller,
        RoleNames.CompanyUser
    ];

    private readonly IUserAdministrationReadService userReads;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ICompanyService companyService;
    private readonly IMediator mediator;
    private readonly IUserAccountNotificationService notifications;

    // Function summary: Initializes user account orchestration with Identity, command, lookup, and notification dependencies.
    public UserAccountWorkflowService(
        IUserAdministrationReadService userReads,
        UserManager<ApplicationUser> userManager,
        ICompanyService companyService,
        IMediator mediator,
        IUserAccountNotificationService notifications)
    {
        this.userReads = userReads;
        this.userManager = userManager;
        this.companyService = companyService;
        this.mediator = mediator;
        this.notifications = notifications;
    }

    // Function summary: Creates an admin-managed user and sends the initial password-set link when configured.
    public async Task<UserAccountWorkflowResult> CreateAsync(
        UserMutationRequest request,
        UserListActor actor,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken)
    {
        var validationErrors = await ValidateUserRequestAsync(request, actor, currentUserId: null, currentRole: null, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return UserAccountWorkflowResultWithErrors(validationErrors);
        }

        var result = await mediator.Send(new CreateUserCommand(request), cancellationToken);
        if (result.Errors.Count > 0 || string.IsNullOrWhiteSpace(result.UserId))
        {
            return UserAccountWorkflowResultWithErrors(result.Errors);
        }

        var user = await userManager.FindByIdAsync(result.UserId);
        if (user == null)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = result.UserId };
        }

        await notifications.SendPasswordSetAsync(user, origin);
        return new UserAccountWorkflowResult
        {
            UserId = user.Id,
            Detail = await userReads.GetDetailAsync(user.Id, actor, cancellationToken)
        };
    }

    // Function summary: Updates an admin-managed user after validating role and account rules.
    public async Task<UserAccountWorkflowResult> UpdateAsync(
        string userId,
        UserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = userId };
        }

        var currentRole = await GetUserRoleAsync(user);
        if (!CanEditUser(currentRole, actor))
        {
            return new UserAccountWorkflowResult { Forbidden = true, UserId = userId };
        }

        var validationErrors = await ValidateUserRequestAsync(request, actor, userId, currentRole, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return UserAccountWorkflowResultWithErrors(validationErrors, userId);
        }

        var result = await mediator.Send(new UpdateUserCommand(userId, request, currentRole), cancellationToken);
        if (result.NotFound)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = userId };
        }
        if (result.Errors.Count > 0)
        {
            return UserAccountWorkflowResultWithErrors(result.Errors, userId);
        }

        return new UserAccountWorkflowResult
        {
            UserId = userId,
            Detail = await userReads.GetDetailAsync(userId, actor, cancellationToken)
        };
    }

    // Function summary: Sends a fresh confirmation/password-set link to an existing user.
    public async Task<UserAccountMessageResult> ResendConfirmationAsync(
        string userId,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserAccountMessageResult { NotFound = true };
        }

        await notifications.SendPasswordSetAsync(user, origin);
        return new UserAccountMessageResult();
    }

    // Function summary: Sends a password-reset link to an existing user.
    public async Task<UserAccountMessageResult> SendResetPasswordLinkAsync(
        string userId,
        UserAccountRequestOrigin origin,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserAccountMessageResult { NotFound = true };
        }

        await notifications.SendPasswordResetAsync(user, origin);
        return new UserAccountMessageResult();
    }

    // Function summary: Disables a user account when the current admin may edit it.
    public async Task<UserAccountWorkflowResult> DisableAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = userId };
        }
        if (!CanEditUser(await GetUserRoleAsync(user), actor))
        {
            return new UserAccountWorkflowResult { Forbidden = true, UserId = userId };
        }

        var result = await mediator.Send(new DisableUserCommand(userId), cancellationToken);
        return await BuildPostCommandResultAsync(result, userId, actor, cancellationToken);
    }

    // Function summary: Enables a user account when the current admin may edit it.
    public async Task<UserAccountWorkflowResult> EnableAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = userId };
        }
        if (!CanEditUser(await GetUserRoleAsync(user), actor))
        {
            return new UserAccountWorkflowResult { Forbidden = true, UserId = userId };
        }

        var result = await mediator.Send(new EnableUserCommand(userId), cancellationToken);
        return await BuildPostCommandResultAsync(result, userId, actor, cancellationToken);
    }

    // Function summary: Deletes a user account when the current admin may delete it.
    public async Task<UserDeleteWorkflowResult> DeleteAsync(
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new UserDeleteWorkflowResult { NotFound = true };
        }

        var role = await GetUserRoleAsync(user);
        if (!CanDeleteUser(role, actor) || string.Equals(user.Id, actor.CurrentUserId, StringComparison.Ordinal))
        {
            return new UserDeleteWorkflowResult { Forbidden = true };
        }

        var result = await mediator.Send(new DeleteUserCommand(userId), cancellationToken);
        if (result.NotFound)
        {
            return new UserDeleteWorkflowResult { NotFound = true };
        }
        if (result.Errors.Count > 0)
        {
            var workflowResult = new UserDeleteWorkflowResult { Email = result.Email };
            CopyErrors(result.Errors, workflowResult.Errors);
            return workflowResult;
        }

        return new UserDeleteWorkflowResult { Email = result.Email };
    }

    // Function summary: Adds a user to a site and returns the refreshed assignment model.
    public async Task<SiteAssignmentWorkflowResult> AddToSiteAsync(
        SiteUserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddUserToSiteCommand(request.UserId, request.SiteId), cancellationToken);
        if (result.Created)
        {
        }

        return await BuildSiteAssignmentResultAsync(result, request.SiteId, actor, cancellationToken);
    }

    // Function summary: Marks a site user as the site contact and returns the refreshed assignment model.
    public async Task<SiteAssignmentWorkflowResult> SetSiteContactAsync(
        SiteUserMutationRequest request,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetSiteContactCommand(request.UserId, request.SiteId), cancellationToken);
        return await BuildSiteAssignmentResultAsync(result, request.SiteId, actor, cancellationToken);
    }

    // Function summary: Clears a site contact assignment and returns the refreshed assignment model.
    public async Task<SiteAssignmentWorkflowResult> RemoveSiteContactAsync(
        Guid siteId,
        Guid userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveSiteContactCommand(userId, siteId), cancellationToken);
        return await BuildSiteAssignmentResultAsync(result, siteId, actor, cancellationToken);
    }

    // Function summary: Removes a user from a site and returns the refreshed assignment model.
    public async Task<SiteAssignmentWorkflowResult> RemoveFromSiteAsync(
        Guid siteId,
        Guid userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveUserFromSiteCommand(userId, siteId), cancellationToken);
        if (result.Removed)
        {
        }

        return await BuildSiteAssignmentResultAsync(result, siteId, actor, cancellationToken);
    }

    // Function summary: Validates admin user mutation requests before dispatching transactional commands.
    private async Task<Dictionary<string, string[]>> ValidateUserRequestAsync(
        UserMutationRequest request,
        UserListActor actor,
        string? currentUserId,
        string? currentRole,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(UserMutationRequest.Email), request.Email, "Email is required.");
        AddRequired(errors, nameof(UserMutationRequest.Role), request.Role, "Role is required.");

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!RoleOrder.Contains(request.Role, StringComparer.Ordinal))
            {
                AddError(errors, nameof(UserMutationRequest.Role), "Role is not valid.");
            }
            else if (!CanAssignRole(request.Role, actor))
            {
                AddError(errors, nameof(UserMutationRequest.Role), "You do not have permission to assign this role.");
            }
        }

        if (RequiresCompanyAssignment(request.Role))
        {
            if (!request.CompanyId.HasValue)
            {
                AddError(errors, nameof(UserMutationRequest.CompanyId), "Company is required for Company User and Installer accounts.");
            }
            else if (await companyService.ReadOneAsync(request.CompanyId.Value) == null)
            {
                AddError(errors, nameof(UserMutationRequest.CompanyId), "Company was not found.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existing = await userManager.FindByEmailAsync(request.Email.Trim());
            if (existing != null && !string.Equals(existing.Id, currentUserId, StringComparison.Ordinal))
            {
                AddError(errors, nameof(UserMutationRequest.Email), "Email already registered");
            }
        }

        if (!string.IsNullOrWhiteSpace(currentRole) && !CanEditUser(currentRole, actor))
        {
            AddError(errors, nameof(UserMutationRequest.Role), "You do not have permission to edit this user.");
        }

        return errors;
    }

    // Function summary: Builds the standard post-mutation user detail result.
    private async Task<UserAccountWorkflowResult> BuildPostCommandResultAsync(
        UserAccountCommandResult result,
        string userId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        if (result.NotFound)
        {
            return new UserAccountWorkflowResult { NotFound = true, UserId = userId };
        }
        if (result.Errors.Count > 0)
        {
            return UserAccountWorkflowResultWithErrors(result.Errors, userId);
        }

        return new UserAccountWorkflowResult
        {
            UserId = userId,
            Detail = await userReads.GetDetailAsync(userId, actor, cancellationToken)
        };
    }

    // Function summary: Builds a site-assignment result with refreshed read models when the command succeeded.
    private async Task<SiteAssignmentWorkflowResult> BuildSiteAssignmentResultAsync(
        UserSiteAssignmentCommandResult result,
        Guid siteId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        if (result.UserNotFound || result.SiteNotFound)
        {
            return new SiteAssignmentWorkflowResult
            {
                UserNotFound = result.UserNotFound,
                SiteNotFound = result.SiteNotFound
            };
        }

        var workflowResult = new SiteAssignmentWorkflowResult
        {
            Assignment = await userReads.GetSiteAssignmentsAsync(siteId, actor, cancellationToken)
        };
        CopyErrors(result.Errors, workflowResult.Errors);
        return workflowResult;
    }

    // Function summary: Retrieves the first role assigned to a user.
    private async Task<string> GetUserRoleAsync(ApplicationUser user)
    {
        return (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
    }

    // Function summary: Evaluates whether the current admin may assign the requested role.
    private static bool CanAssignRole(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            role is RoleNames.CompanyUser or RoleNames.RVTInstaller;
    }

    private static bool RequiresCompanyAssignment(string? role) => role is RoleNames.CompanyUser or RoleNames.RVTInstaller;

    // Function summary: Evaluates whether the current admin may edit the target role.
    private static bool CanEditUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role is not RoleNames.RVTAdmin and not RoleNames.RVTMasterAdmin);
    }

    // Function summary: Evaluates whether the current admin may delete the target role.
    private static bool CanDeleteUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role == RoleNames.CompanyUser);
    }

    // Function summary: Adds a required-field error when a value is empty.
    private static void AddRequired(Dictionary<string, string[]> errors, string key, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, key, message);
        }
    }

    // Function summary: Adds a validation error to the mutable error dictionary.
    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? [.. existing, message]
            : [message];
    }

    // Function summary: Copies command errors into workflow result errors.
    private static void CopyErrors(IReadOnlyDictionary<string, string[]> source, Dictionary<string, string[]> target)
    {
        foreach (var error in source)
        {
            target[error.Key] = error.Value;
        }
    }

    // Function summary: Builds a user account workflow result from validation or command errors.
    private static UserAccountWorkflowResult UserAccountWorkflowResultWithErrors(
        IReadOnlyDictionary<string, string[]> errors,
        string? userId = null)
    {
        var result = new UserAccountWorkflowResult { UserId = userId };
        CopyErrors(errors, result.Errors);
        return result;
    }
}

public sealed class UserAccountNotificationService : IUserAccountNotificationService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IConfiguration configuration;
    private readonly SpaOptions spaOptions;
    private readonly IAccountMessenger accountMessenger;

    // Function summary: Initializes account notification sending with Identity token generation and message delivery dependencies.
    public UserAccountNotificationService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IOptions<SpaOptions> spaOptions,
        IAccountMessenger accountMessenger)
    {
        this.userManager = userManager;
        this.configuration = configuration;
        this.spaOptions = spaOptions.Value;
        this.accountMessenger = accountMessenger;
    }

    // Function summary: Sends the password-set email for a newly created or unconfirmed account.
    public async Task SendPasswordSetAsync(ApplicationUser user, UserAccountRequestOrigin origin)
    {
        if (configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            return;
        }

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = BuildClientUrl("/confirm-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id,
            ["code"] = code
        });
        var delivery = await accountMessenger.SendPasswordSetAsync(user.Email ?? "", callbackUrl, CancellationToken.None);
        if (!delivery.Succeeded)
        {
            throw new InvalidOperationException($"Email failed to send ({delivery.ProviderResponse})");
        }
    }

    // Function summary: Sends the password-reset email for an existing account.
    public async Task SendPasswordResetAsync(ApplicationUser user, UserAccountRequestOrigin origin)
    {
        if (configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            return;
        }

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        var callbackUrl = BuildClientUrl("/reset-password", new Dictionary<string, string?>
        {
            ["code"] = code
        });
        var delivery = await accountMessenger.SendPasswordResetAsync(user.Email ?? "", callbackUrl, CancellationToken.None);
        if (!delivery.Succeeded)
        {
            throw new InvalidOperationException($"Email failed to send ({delivery.ProviderResponse})");
        }
    }

    // Function summary: Builds an SPA client URL only from the configured public base URL.
    private string BuildClientUrl(string path, IDictionary<string, string?> query)
    {
        return SpaPublicLinkBuilder.Build(spaOptions, path, query);
    }
}

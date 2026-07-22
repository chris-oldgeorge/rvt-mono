// File summary: Coordinates authentication, password, email-confirmation, and profile workflows for the auth API.
// Major updates:
// - 2026-07-09 pending Moved AuthController Identity, profile, reset-link, and email orchestration into an application service.
// - 2026-07-22 pending Removed request-host link generation, made reset failures uniform, and added confirmed profile email changes.
// - 2026-07-22 pending Made email-and-username confirmation atomic through verified rollback on username failure.

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic;
using RVT.BusinessLogic.Notifications;
using RvtPortal.Spa.Application.Companies;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Auth;

public interface IAuthApplicationService
{
    // Function summary: Builds the current authentication state for the supplied principal.
    Task<AuthWorkflowResult<AuthStateResponse>> CurrentStateAsync(ClaimsPrincipal principal);

    // Function summary: Signs in a user by email and password.
    Task<AuthWorkflowResult<AuthStateResponse>> LoginAsync(LoginRequest request, bool alreadyAuthenticated);

    // Function summary: Signs out the current session.
    Task<AuthStateResponse> LogoutAsync();

    // Function summary: Sends a password-reset email when the account is eligible while keeping a generic public response.
    Task<AuthWorkflowResult<MessageResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, AuthRequestOrigin origin);

    // Function summary: Resets a password from a supplied reset token.
    Task<AuthWorkflowResult<MessageResponse>> ResetPasswordAsync(ResetPasswordRequest request);

    // Function summary: Confirms an email from a supplied confirmation link.
    Task<AuthWorkflowResult<ConfirmEmailResponse>> ConfirmEmailAsync(string? userId, string? code);

    // Function summary: Confirms a pending profile email change through Identity's change-email token.
    Task<AuthWorkflowResult<ConfirmEmailResponse>> ConfirmEmailChangeAsync(string? userId, string? email, string? code);

    // Function summary: Sets the initial password after email confirmation and signs in the user.
    Task<AuthWorkflowResult<AuthStateResponse>> SetInitialPasswordAsync(SetInitialPasswordRequest request);

    // Function summary: Changes the signed-in user's password.
    Task<AuthWorkflowResult<MessageResponse>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request);

    // Function summary: Builds the signed-in user's profile.
    Task<AuthWorkflowResult<ProfileResponse>> ProfileAsync(ClaimsPrincipal principal);

    // Function summary: Updates the signed-in user's profile.
    Task<AuthWorkflowResult<ProfileResponse>> UpdateProfileAsync(ClaimsPrincipal principal, UpdateProfileRequest request);
}

public sealed record AuthRequestOrigin(string Scheme, string Host, string PathBase, string? CorrelationId = null);

public sealed class SpaOptions
{
    public const string SectionName = "Spa";
    public string PublicBaseUrl { get; set; } = "";
}

public static class SpaPublicLinkBuilder
{
    // Function summary: Builds an account-action URL exclusively from the configured public SPA base URI.
    public static string Build(SpaOptions options, string path, IDictionary<string, string?> query)
    {
        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var baseUri) ||
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(baseUri.Host) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException(
                "Spa:PublicBaseUrl must be configured as an absolute HTTPS base URI without credentials, query, or fragment before account links can be sent.");
        }

        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{baseUrl}/{path.TrimStart('/')}", query);
    }
}

public enum AuthWorkflowStatus
{
    Success,
    AlreadySignedIn,
    AccountDisabled,
    LockedOut,
    SignInNotAllowed,
    InvalidCredentials,
    Unauthorized,
    MissingConfirmationValues,
    MalformedConfirmationCode,
    ConfirmationCouldNotBeVerified,
    ConfirmationFailed,
    InitialPasswordUserNotFound,
    EmailNotConfirmed,
    PasswordAlreadySet,
    EmailFailed,
    ValidationFailed
}

public sealed class AuthWorkflowResult<T>
{
    public AuthWorkflowStatus Status { get; init; }
    public T? Value { get; init; }
    public string? Detail { get; init; }
    public Dictionary<string, string[]> Errors { get; } = [];

    public static AuthWorkflowResult<T> Success(T value)
    {
        return new AuthWorkflowResult<T> { Status = AuthWorkflowStatus.Success, Value = value };
    }

    public static AuthWorkflowResult<T> Failure(AuthWorkflowStatus status, string? detail = null)
    {
        return new AuthWorkflowResult<T> { Status = status, Detail = detail };
    }
}

public sealed class AuthApplicationService : IAuthApplicationService
{
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ICompanyService companyService;
    private readonly IConfiguration configuration;
    private readonly SpaOptions spaOptions;
    private readonly IAccountMessenger accountMessenger;
    private readonly ILogger<AuthApplicationService> logger;

    // Function summary: Initializes auth workflows with Identity, company profile, configuration, and email dependencies.
    public AuthApplicationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ICompanyService companyService,
        IConfiguration configuration,
        IOptions<SpaOptions> spaOptions,
        IAccountMessenger accountMessenger,
        ILogger<AuthApplicationService> logger)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
        this.companyService = companyService;
        this.configuration = configuration;
        this.spaOptions = spaOptions.Value;
        this.accountMessenger = accountMessenger;
        this.logger = logger;
    }

    // Function summary: Builds the current authentication state for the supplied principal.
    public async Task<AuthWorkflowResult<AuthStateResponse>> CurrentStateAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AuthWorkflowResult<AuthStateResponse>.Success(AuthStateResponse.Anonymous());
        }

        var user = await userManager.GetUserAsync(principal);
        if (user == null || user.IsDisabled)
        {
            await signInManager.SignOutAsync();
            return AuthWorkflowResult<AuthStateResponse>.Success(AuthStateResponse.Anonymous());
        }

        return AuthWorkflowResult<AuthStateResponse>.Success(await BuildAuthStateAsync(user));
    }

    // Function summary: Signs in a user by email and password.
    public async Task<AuthWorkflowResult<AuthStateResponse>> LoginAsync(LoginRequest request, bool alreadyAuthenticated)
    {
        if (alreadyAuthenticated)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.AlreadySignedIn);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user != null && user.IsDisabled)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.AccountDisabled);
        }

        var result = user == null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded && user is not null)
        {
            return AuthWorkflowResult<AuthStateResponse>.Success(await BuildAuthStateAsync(user));
        }
        if (result.IsLockedOut)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.LockedOut);
        }
        if (result.RequiresTwoFactor || result.IsNotAllowed)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.SignInNotAllowed);
        }

        return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.InvalidCredentials);
    }

    // Function summary: Signs out the current session.
    public async Task<AuthStateResponse> LogoutAsync()
    {
        await signInManager.SignOutAsync();
        return AuthStateResponse.Anonymous();
    }

    // Function summary: Sends a password-reset email when the account is eligible while keeping a generic public response.
    public async Task<AuthWorkflowResult<MessageResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, AuthRequestOrigin origin)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
        }

        if (configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
        }

        try
        {
            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = BuildClientUrl("/reset-password", new Dictionary<string, string?>
            {
                ["code"] = code
            });
            var delivery = await accountMessenger.SendPasswordResetAsync(user.Email ?? request.Email, callbackUrl, CancellationToken.None);
            if (!delivery.Succeeded)
            {
                logger.LogWarning(
                    "Password-reset email delivery failed. CorrelationId: {CorrelationId}; ProviderResponse: {ProviderResponse}",
                    origin.CorrelationId ?? "unavailable",
                    delivery.ProviderResponse);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Password-reset email workflow failed. CorrelationId: {CorrelationId}",
                origin.CorrelationId ?? "unavailable");
        }

        return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
    }

    // Function summary: Resets a password from a supplied reset token.
    public async Task<AuthWorkflowResult<MessageResponse>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordChangedMessage());
        }

        var result = await userManager.ResetPasswordAsync(user, request.Code, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => string.Equals(error.Code, "InvalidToken", StringComparison.Ordinal)))
            {
                return AuthWorkflowResult<MessageResponse>.Success(PasswordChangedMessage());
            }

            return IdentityErrorResult<MessageResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        return AuthWorkflowResult<MessageResponse>.Success(PasswordChangedMessage());
    }

    // Function summary: Confirms an email from a supplied confirmation link.
    public async Task<AuthWorkflowResult<ConfirmEmailResponse>> ConfirmEmailAsync(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.MissingConfirmationValues);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null || user.EmailConfirmed)
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
        }
        if (!TryDecodeConfirmationCode(code, out var decodedCode))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.MalformedConfirmationCode);
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedCode);
        return result.Succeeded
            ? AuthWorkflowResult<ConfirmEmailResponse>.Success(new ConfirmEmailResponse
            {
                UserId = user.Id,
                Email = user.Email ?? ""
            })
            : AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
    }

    // Function summary: Applies a pending profile email only after Identity validates the change-email token.
    public async Task<AuthWorkflowResult<ConfirmEmailResponse>> ConfirmEmailChangeAsync(string? userId, string? email, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.MissingConfirmationValues);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null || !TryDecodeConfirmationCode(code, out var decodedCode))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(
                user == null ? AuthWorkflowStatus.ConfirmationFailed : AuthWorkflowStatus.MalformedConfirmationCode);
        }

        var originalEmail = user.Email;
        var originalUserName = user.UserName;
        var originalEmailConfirmed = user.EmailConfirmed;
        var originalSecurityStamp = user.SecurityStamp;
        var newEmail = email.Trim();
        var result = await userManager.ChangeEmailAsync(user, newEmail, decodedCode);
        if (!result.Succeeded)
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
        }

        var userNameResult = await userManager.SetUserNameAsync(user, newEmail);
        if (!userNameResult.Succeeded)
        {
            user.Email = originalEmail;
            user.UserName = originalUserName;
            user.EmailConfirmed = originalEmailConfirmed;
            user.SecurityStamp = originalSecurityStamp;
            var rollbackResult = await userManager.UpdateAsync(user);
            if (!rollbackResult.Succeeded ||
                !string.Equals(user.Email, originalEmail, StringComparison.Ordinal) ||
                !string.Equals(user.UserName, originalUserName, StringComparison.Ordinal) ||
                user.EmailConfirmed != originalEmailConfirmed ||
                !string.Equals(user.SecurityStamp, originalSecurityStamp, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unable to restore the user's confirmed email state after a username update failure.");
            }

            return IdentityErrorResult<ConfirmEmailResponse>(AuthWorkflowStatus.ValidationFailed, userNameResult.Errors);
        }

        return AuthWorkflowResult<ConfirmEmailResponse>.Success(new ConfirmEmailResponse
        {
            UserId = user.Id,
            Email = user.Email ?? ""
        });
    }

    // Function summary: Sets the initial password after email confirmation and signs in the user.
    public async Task<AuthWorkflowResult<AuthStateResponse>> SetInitialPasswordAsync(SetInitialPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.InitialPasswordUserNotFound);
        }
        if (!user.EmailConfirmed)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.EmailNotConfirmed);
        }
        if (!TryDecodeConfirmationCode(request.Code, out var decodedCode))
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.MalformedConfirmationCode);
        }

        var isValidConfirmationToken = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.EmailConfirmationTokenProvider,
            "EmailConfirmation",
            decodedCode);
        if (!isValidConfirmationToken)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.ConfirmationCouldNotBeVerified);
        }
        if (await userManager.HasPasswordAsync(user))
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.PasswordAlreadySet);
        }

        var result = await userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<AuthStateResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return AuthWorkflowResult<AuthStateResponse>.Success(await BuildAuthStateAsync(user));
    }

    // Function summary: Changes the signed-in user's password.
    public async Task<AuthWorkflowResult<MessageResponse>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            return AuthWorkflowResult<MessageResponse>.Failure(AuthWorkflowStatus.Unauthorized);
        }

        var result = await userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<MessageResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        await signInManager.RefreshSignInAsync(user);
        return AuthWorkflowResult<MessageResponse>.Success(new MessageResponse { Message = "Your password has been changed." });
    }

    // Function summary: Builds the signed-in user's profile.
    public async Task<AuthWorkflowResult<ProfileResponse>> ProfileAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        return user == null
            ? AuthWorkflowResult<ProfileResponse>.Failure(AuthWorkflowStatus.Unauthorized)
            : AuthWorkflowResult<ProfileResponse>.Success(await BuildProfileAsync(user));
    }

    // Function summary: Updates the signed-in user's profile.
    public async Task<AuthWorkflowResult<ProfileResponse>> UpdateProfileAsync(ClaimsPrincipal principal, UpdateProfileRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            return AuthWorkflowResult<ProfileResponse>.Failure(AuthWorkflowStatus.Unauthorized);
        }

        user.Name = request.Name;
        user.PhoneNumber = request.MobilePhone;
        user.CompanyRole = request.CompanyRole;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<ProfileResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        if (!string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            var newEmail = request.Email.Trim();
            var code = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = BuildClientUrl("/api/auth/change-email", new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["email"] = newEmail,
                ["code"] = code
            });
            var delivery = await accountMessenger.SendEmailChangeAsync(newEmail, callbackUrl, CancellationToken.None);
            if (!delivery.Succeeded)
            {
                logger.LogWarning("Profile email-change confirmation delivery failed. ProviderResponse: {ProviderResponse}", delivery.ProviderResponse);
            }
        }

        await signInManager.RefreshSignInAsync(user);
        return AuthWorkflowResult<ProfileResponse>.Success(await BuildProfileAsync(user));
    }

    // Function summary: Builds the authenticated state for one Identity user.
    private async Task<AuthStateResponse> BuildAuthStateAsync(ApplicationUser user)
    {
        if (user.IsDisabled)
        {
            await signInManager.SignOutAsync();
            return AuthStateResponse.Anonymous();
        }

        return new AuthStateResponse
        {
            IsAuthenticated = true,
            User = await BuildUserAsync(user)
        };
    }

    // Function summary: Builds the API user shape for auth-state responses.
    private async Task<AuthUserResponse> BuildUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email ?? "",
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            CompanyId = user.CompanyId,
            CompanyRole = user.CompanyRole,
            Roles = roles.ToList()
        };
    }

    // Function summary: Builds the signed-in user's editable profile response.
    private async Task<ProfileResponse> BuildProfileAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        string? companyName = null;
        if (user.CompanyId.HasValue)
        {
            companyName = (await companyService.ReadOneAsync(user.CompanyId.Value))?.CompanyName;
        }

        return new ProfileResponse
        {
            Id = user.Id,
            Email = user.Email ?? "",
            Name = user.Name,
            MobilePhone = user.PhoneNumber,
            Role = roles.FirstOrDefault(),
            CompanyRole = user.CompanyRole,
            CompanyName = companyName
        };
    }

    // Function summary: Builds an SPA client URL only from the configured public base URL.
    private string BuildClientUrl(string path, IDictionary<string, string?> query)
    {
        return SpaPublicLinkBuilder.Build(spaOptions, path, query);
    }

    // Function summary: Attempts to decode a base64-url email-confirmation code.
    private static bool TryDecodeConfirmationCode(string code, out string decodedCode)
    {
        try
        {
            decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            return true;
        }
        catch (FormatException)
        {
            decodedCode = "";
            return false;
        }
    }

    // Function summary: Builds the public generic password-reset response.
    private static MessageResponse PasswordResetMessage()
    {
        return new MessageResponse { Message = "If the account can be reset, a password reset email has been sent." };
    }

    // Function summary: Builds the public generic password-changed response.
    private static MessageResponse PasswordChangedMessage()
    {
        return new MessageResponse { Message = "Your password has been reset." };
    }

    // Function summary: Converts Identity errors into a workflow validation result.
    private static AuthWorkflowResult<T> IdentityErrorResult<T>(
        AuthWorkflowStatus status,
        IEnumerable<IdentityError> errors)
    {
        var result = AuthWorkflowResult<T>.Failure(status);
        foreach (var group in errors.GroupBy(error => error.Code))
        {
            result.Errors[group.Key] = group.Select(error => error.Description).ToArray();
        }

        return result;
    }
}

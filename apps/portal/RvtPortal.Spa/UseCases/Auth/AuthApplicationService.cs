// File summary: Coordinates authentication, password, email-confirmation, and profile workflows for the auth API.
// Major updates:
// - 2026-07-09 pending Moved AuthController Identity, profile, reset-link, and email orchestration into an application service.
// - 2026-07-22 pending Removed request-host link generation, made reset failures uniform, and added confirmed profile email changes.
// - 2026-07-22 pending Made confirmed email-and-username changes atomic in an Identity database transaction.
// - 2026-07-30 pending Removed the non-relational compensation path once the Spa test host moved onto PostgreSQL.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using RvtPortal.Application.Notifications;
using RvtPortal.Application.Ports.Notifications;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Companies;

namespace RvtPortal.Spa.UseCases.Auth;

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
        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out Uri? baseUri) ||
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(baseUri.Host) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException(
                "Spa:PublicBaseUrl must be configured as an absolute HTTPS base URI without credentials, query, or fragment before account links can be sent.");
        }

        string baseUrl = options.PublicBaseUrl.TrimEnd('/');
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
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _applicationContext;
    private readonly ICompanyService _companyService;
    private readonly IConfiguration _configuration;
    private readonly SpaOptions _spaOptions;
    private readonly IAccountMessenger _accountMessenger;
    private readonly ILogger<AuthApplicationService> _logger;

    // Function summary: Initializes auth workflows with Identity, company profile, _configuration, and email dependencies.
    [SuppressMessage(
        "Maintainability",
        "S107:Methods should not have too many parameters",
        Justification = "Constructor injection exposes the independently scoped Identity, persistence, messaging, and observability dependencies.")]
    public AuthApplicationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext applicationContext,
        ICompanyService companyService,
        IConfiguration configuration,
        IOptions<SpaOptions> spaOptions,
        IAccountMessenger accountMessenger,
        ILogger<AuthApplicationService> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _applicationContext = applicationContext;
        _companyService = companyService;
        _configuration = configuration;
        _spaOptions = spaOptions.Value;
        _accountMessenger = accountMessenger;
        _logger = logger;
    }

    // Function summary: Builds the current authentication state for the supplied principal.
    public async Task<AuthWorkflowResult<AuthStateResponse>> CurrentStateAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AuthWorkflowResult<AuthStateResponse>.Success(AuthStateResponse.Anonymous());
        }

        ApplicationUser? user = await _userManager.GetUserAsync(principal);
        if (user == null || user.IsDisabled)
        {
            await _signInManager.SignOutAsync();
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

        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user != null && user.IsDisabled)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.AccountDisabled);
        }

        SignInResult result = user == null
            ? SignInResult.Failed
            : await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
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
        await _signInManager.SignOutAsync();
        return AuthStateResponse.Anonymous();
    }

    // Function summary: Sends a password-reset email when the account is eligible while keeping a generic public response.
    public async Task<AuthWorkflowResult<MessageResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, AuthRequestOrigin origin)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
        }

        if (_configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
        }

        try
        {
            string code = await _userManager.GeneratePasswordResetTokenAsync(user);
            string callbackUrl = BuildClientUrl("/reset-password", new Dictionary<string, string?>
            {
                ["code"] = code
            });
            EmailDeliveryResult delivery = await _accountMessenger.SendPasswordResetAsync(user.Email ?? request.Email, callbackUrl, CancellationToken.None);
            if (!delivery.Succeeded)
            {
                _logger.LogWarning(
                    "Password-reset email delivery failed. CorrelationId: {CorrelationId}; ProviderResponse: {ProviderResponse}",
                    origin.CorrelationId ?? "unavailable",
                    delivery.ProviderResponse);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Password-reset email workflow failed. CorrelationId: {CorrelationId}",
                origin.CorrelationId ?? "unavailable");
        }

        return AuthWorkflowResult<MessageResponse>.Success(PasswordResetMessage());
    }

    // Function summary: Resets a password from a supplied reset token.
    public async Task<AuthWorkflowResult<MessageResponse>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return AuthWorkflowResult<MessageResponse>.Success(PasswordChangedMessage());
        }

        IdentityResult result = await _userManager.ResetPasswordAsync(user, request.Code, request.Password);
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

        ApplicationUser? user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.EmailConfirmed)
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
        }

        if (!TryDecodeConfirmationCode(code, out string? decodedCode))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.MalformedConfirmationCode);
        }

        IdentityResult result = await _userManager.ConfirmEmailAsync(user, decodedCode);
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

        if (!TryDecodeConfirmationCode(code, out string? decodedCode))
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.MalformedConfirmationCode);
        }

        return await ConfirmEmailChangeInTransactionAsync(userId, email.Trim(), decodedCode);
    }

    // Function summary: Applies confirmed email and username together in the Identity database transaction.
    private async Task<AuthWorkflowResult<ConfirmEmailResponse>> ConfirmEmailChangeInTransactionAsync(
        string userId,
        string newEmail,
        string decodedCode)
    {
        IExecutionStrategy strategy = _applicationContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _applicationContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await _applicationContext.Database.BeginTransactionAsync();
            try
            {
                ApplicationUser? user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    await transaction.RollbackAsync();
                    _applicationContext.ChangeTracker.Clear();
                    return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
                }

                AuthWorkflowResult<ConfirmEmailResponse> transition = await ApplyConfirmedEmailTransitionAsync(user, newEmail, decodedCode);
                if (transition.Status == AuthWorkflowStatus.Success)
                {
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                    _applicationContext.ChangeTracker.Clear();
                }

                return transition;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _applicationContext.ChangeTracker.Clear();
                throw;
            }
        });
    }

    // Function summary: Runs the two Identity writes that form one confirmed email transition.
    private async Task<AuthWorkflowResult<ConfirmEmailResponse>> ApplyConfirmedEmailTransitionAsync(
        ApplicationUser user,
        string newEmail,
        string decodedCode)
    {
        IdentityResult emailResult = await _userManager.ChangeEmailAsync(user, newEmail, decodedCode);
        if (!emailResult.Succeeded)
        {
            return AuthWorkflowResult<ConfirmEmailResponse>.Failure(AuthWorkflowStatus.ConfirmationFailed);
        }

        IdentityResult userNameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!userNameResult.Succeeded)
        {
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
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.InitialPasswordUserNotFound);
        }

        if (!user.EmailConfirmed)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.EmailNotConfirmed);
        }

        if (!TryDecodeConfirmationCode(request.Code, out string? decodedCode))
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.MalformedConfirmationCode);
        }

        bool isValidConfirmationToken = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.EmailConfirmationTokenProvider,
            "EmailConfirmation",
            decodedCode);
        if (!isValidConfirmationToken)
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.ConfirmationCouldNotBeVerified);
        }

        if (await _userManager.HasPasswordAsync(user))
        {
            return AuthWorkflowResult<AuthStateResponse>.Failure(AuthWorkflowStatus.PasswordAlreadySet);
        }

        IdentityResult result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<AuthStateResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        return AuthWorkflowResult<AuthStateResponse>.Success(await BuildAuthStateAsync(user));
    }

    // Function summary: Changes the signed-in user's password.
    public async Task<AuthWorkflowResult<MessageResponse>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return AuthWorkflowResult<MessageResponse>.Failure(AuthWorkflowStatus.Unauthorized);
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<MessageResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        await _signInManager.RefreshSignInAsync(user);
        return AuthWorkflowResult<MessageResponse>.Success(new MessageResponse { Message = "Your password has been changed." });
    }

    // Function summary: Builds the signed-in user's profile.
    public async Task<AuthWorkflowResult<ProfileResponse>> ProfileAsync(ClaimsPrincipal principal)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(principal);
        return user == null
            ? AuthWorkflowResult<ProfileResponse>.Failure(AuthWorkflowStatus.Unauthorized)
            : AuthWorkflowResult<ProfileResponse>.Success(await BuildProfileAsync(user));
    }

    // Function summary: Updates the signed-in user's profile.
    public async Task<AuthWorkflowResult<ProfileResponse>> UpdateProfileAsync(ClaimsPrincipal principal, UpdateProfileRequest request)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return AuthWorkflowResult<ProfileResponse>.Failure(AuthWorkflowStatus.Unauthorized);
        }

        user.Name = request.Name;
        user.PhoneNumber = request.MobilePhone;
        user.CompanyRole = request.CompanyRole;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityErrorResult<ProfileResponse>(AuthWorkflowStatus.ValidationFailed, result.Errors);
        }

        if (!string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !_configuration.GetValue<bool>("Auth:SkipPasswordResetEmail"))
        {
            string newEmail = request.Email.Trim();
            string code = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            string callbackUrl = BuildClientUrl("/api/auth/change-email", new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["email"] = newEmail,
                ["code"] = code
            });
            EmailDeliveryResult delivery = await _accountMessenger.SendEmailChangeAsync(newEmail, callbackUrl, CancellationToken.None);
            if (!delivery.Succeeded)
            {
                _logger.LogWarning("Profile email-change confirmation delivery failed. ProviderResponse: {ProviderResponse}", delivery.ProviderResponse);
            }
        }

        await _signInManager.RefreshSignInAsync(user);
        return AuthWorkflowResult<ProfileResponse>.Success(await BuildProfileAsync(user));
    }

    // Function summary: Builds the authenticated state for one Identity user.
    private async Task<AuthStateResponse> BuildAuthStateAsync(ApplicationUser user)
    {
        if (user.IsDisabled)
        {
            await _signInManager.SignOutAsync();
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
        IList<string> roles = await _userManager.GetRolesAsync(user);
        return new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email ?? "",
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            CompanyId = user.CompanyId,
            CompanyRole = user.CompanyRole,
            Roles = [.. roles]
        };
    }

    // Function summary: Builds the signed-in user's editable profile response.
    private async Task<ProfileResponse> BuildProfileAsync(ApplicationUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        string? companyName = null;
        if (user.CompanyId.HasValue)
        {
            companyName = (await _companyService.ReadOneAsync(user.CompanyId.Value))?.CompanyName;
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
        return SpaPublicLinkBuilder.Build(_spaOptions, path, query);
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
        AuthWorkflowResult<T> result = AuthWorkflowResult<T>.Failure(status);
        foreach (IGrouping<string, IdentityError> group in errors.GroupBy(error => error.Code))
        {
            result.Errors[group.Key] = [.. group.Select(error => error.Description)];
        }

        return result;
    }
}

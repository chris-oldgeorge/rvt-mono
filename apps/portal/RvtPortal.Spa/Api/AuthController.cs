// File summary: Exposes API endpoints used by the React portal for auth controller workflows.
// Major updates:
// - 2026-07-09 pending Routed auth identity, profile, and reset-link workflows through an application service.
// - 2026-06-29 Routed password-reset email diagnostics through injected EmailSender logging.
// - 2026-06-26 pending Simplified login to one email-based Identity sign-in path.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-25 pending Rate limited anonymous login, forgot-password, and reset-password endpoints.
// - 2026-06-25 pending Built reset/confirmation links from the configured public base URL to resist host-header injection.
// - 2026-06-25 pending Removed user-enumeration oracles from reset-password and confirm-email responses.
// - 2026-07-22 pending Added profile email-change confirmation and made forgot-password delivery failures publicly indistinguishable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RvtPortal.Spa.Application.Auth;

namespace RvtPortal.Spa.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string InvalidConfirmationLinkTitle = "Invalid confirmation link";
    private readonly IAuthApplicationService auth;

    // Function summary: Initializes this HTTP adapter with authentication workflow orchestration.
    public AuthController(IAuthApplicationService auth)
    {
        this.auth = auth;
    }

    [HttpGet("me")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthStateResponse), StatusCodes.Status200OK)]
    // Function summary: Returns the current authentication state.
    public async Task<ActionResult<AuthStateResponse>> Me()
    {
        return (await auth.CurrentStateAsync(User)).Value!;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(AuthStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    // Function summary: Signs in a user through the auth application service.
    public async Task<ActionResult<AuthStateResponse>> Login(LoginRequest request)
    {
        var result = await auth.LoginAsync(request, User.Identity?.IsAuthenticated == true);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.AlreadySignedIn => BadRequest(new ProblemDetails
            {
                Title = "Already signed in",
                Detail = "You are already logged in.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.AccountDisabled => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Account disabled",
                Detail = "Your account has been disabled.",
                Status = StatusCodes.Status403Forbidden
            }),
            AuthWorkflowStatus.LockedOut => StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Title = "User locked out",
                Detail = "User Locked out.",
                Status = StatusCodes.Status423Locked
            }),
            AuthWorkflowStatus.SignInNotAllowed => Unauthorized(new ProblemDetails
            {
                Title = "Sign in failed",
                Detail = "Unknown error. Contact support",
                Status = StatusCodes.Status401Unauthorized
            }),
            _ => Unauthorized(new ProblemDetails
            {
                Title = "Sign in failed",
                Detail = "We could not find a user with that username and password.",
                Status = StatusCodes.Status401Unauthorized
            })
        };
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(AuthStateResponse), StatusCodes.Status200OK)]
    // Function summary: Signs out the current session.
    public async Task<ActionResult<AuthStateResponse>> Logout()
    {
        return await auth.LogoutAsync();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    // Function summary: Starts the forgot-password workflow using a generic public response.
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await auth.ForgotPasswordAsync(request, BuildRequestOrigin());
        return result.Value!;
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    // Function summary: Resets a password through the auth application service.
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request)
    {
        var result = await auth.ResetPasswordAsync(request);
        return result.Status == AuthWorkflowStatus.ValidationFailed
            ? IdentityErrors("Password reset failed", result.Errors)
            : result.Value!;
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConfirmEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Confirms a user's email from a confirmation link.
    public async Task<ActionResult<ConfirmEmailResponse>> ConfirmEmail([FromQuery] string? userId, [FromQuery] string? code)
    {
        var result = await auth.ConfirmEmailAsync(userId, code);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.MissingConfirmationValues => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "A user and confirmation code must be supplied.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.MalformedConfirmationCode => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "The confirmation code is malformed.",
                Status = StatusCodes.Status400BadRequest
            }),
            _ => ConfirmationFailed()
        };
    }

    [HttpGet("change-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConfirmEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Confirms a pending profile email change through Identity.
    public async Task<ActionResult<ConfirmEmailResponse>> ConfirmEmailChange(
        [FromQuery] string? userId,
        [FromQuery] string? email,
        [FromQuery] string? code)
    {
        var result = await auth.ConfirmEmailChangeAsync(userId, email, code);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.MissingConfirmationValues => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "A user, email, and confirmation code must be supplied.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.MalformedConfirmationCode => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "The confirmation code is malformed.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.ValidationFailed => IdentityErrors("Email change failed", result.Errors),
            _ => ConfirmationFailed()
        };
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Sets the initial password after email confirmation.
    public async Task<ActionResult<AuthStateResponse>> SetInitialPassword(SetInitialPasswordRequest request)
    {
        var result = await auth.SetInitialPasswordAsync(request);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.InitialPasswordUserNotFound => BadRequest(new ProblemDetails
            {
                Title = "User not found",
                Detail = "Unable to load the confirmed user.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.EmailNotConfirmed => BadRequest(new ProblemDetails
            {
                Title = "Email not confirmed",
                Detail = "The user's email must be confirmed before a password can be set.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.MalformedConfirmationCode => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "The confirmation code is malformed.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.ConfirmationCouldNotBeVerified => BadRequest(new ProblemDetails
            {
                Title = InvalidConfirmationLinkTitle,
                Detail = "The confirmation link could not be verified.",
                Status = StatusCodes.Status400BadRequest
            }),
            AuthWorkflowStatus.PasswordAlreadySet => BadRequest(new ProblemDetails
            {
                Title = "Password already set",
                Detail = "The user's password has already been set.",
                Status = StatusCodes.Status400BadRequest
            }),
            _ => IdentityErrors("Could not set password", result.Errors)
        };
    }

    [HttpPost("password")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Changes the signed-in user's password.
    public async Task<ActionResult<MessageResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var result = await auth.ChangePasswordAsync(User, request);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.Unauthorized => Unauthorized(),
            _ => IdentityErrors("Password change failed", result.Errors)
        };
    }

    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    // Function summary: Retrieves the signed-in user's profile.
    public async Task<ActionResult<ProfileResponse>> Profile()
    {
        var result = await auth.ProfileAsync(User);
        return result.Status == AuthWorkflowStatus.Unauthorized ? Unauthorized() : result.Value!;
    }

    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Updates the signed-in user's profile.
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        var result = await auth.UpdateProfileAsync(User, request);
        return result.Status switch
        {
            AuthWorkflowStatus.Success => result.Value!,
            AuthWorkflowStatus.Unauthorized => Unauthorized(),
            _ => IdentityErrors("Profile update failed", result.Errors)
        };
    }

    // Function summary: Captures the current request origin for auth links without passing HTTP types into application services.
    private AuthRequestOrigin BuildRequestOrigin()
    {
        return new AuthRequestOrigin(
            Request.Scheme,
            Request.Host.ToString(),
            Request.PathBase.ToString(),
            HttpContext.GetCorrelationId());
    }

    // Function summary: Builds the public response for expired, reused, or unknown confirmation links.
    private NotFoundObjectResult ConfirmationFailed()
    {
        return NotFound(new ProblemDetails
        {
            Title = "Confirmation failed",
            Detail = "The link has been used or has expired.",
            Status = StatusCodes.Status404NotFound
        });
    }

    // Function summary: Builds a validation problem response from grouped Identity errors.
    private static BadRequestObjectResult IdentityErrors(string title, IReadOnlyDictionary<string, string[]> errors)
    {
        var details = new ValidationProblemDetails(errors.ToDictionary(error => error.Key, error => error.Value))
        {
            Title = title,
            Status = StatusCodes.Status400BadRequest
        };
        return new BadRequestObjectResult(details);
    }
}

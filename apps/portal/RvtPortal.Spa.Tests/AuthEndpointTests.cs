// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-26 pending Covered email-only login after removing legacy username fallback.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public class AuthEndpointTests
{
    private const string AdminEmail = "admin@rvt.test";
    private const string InstallerEmail = "installer@rvt.test";
    private const string DisabledEmail = "disabled@rvt.test";
    private const string Password = "P8sSw0rd9$";
    private const string NewPassword = "N3wP8sSw0rd9$";

    [RequiresPostgresFact]
    // Function summary: Handles the me returns anonymous state when user is not signed in workflow for this module.
    public async Task Me_ReturnsAnonymousState_WhenUserIsNotSignedIn()
    {
        using SpaTestApplicationFactory factory = new();
        HttpClient client = CreateClient(factory);

        AuthStateResponse? auth = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.NotNull(auth);
        Assert.False(auth.IsAuthenticated);
        Assert.Null(auth.User);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the login returns auth state and cookie for valid user workflow for this module.
    public async Task Login_ReturnsAuthStateAndCookie_ForValidUser()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await LoginAsync(client, AdminEmail, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthStateResponse? auth = await response.Content.ReadFromJsonAsync<AuthStateResponse>();
        Assert.True(auth?.IsAuthenticated);
        Assert.Equal(AdminEmail, auth?.User?.Email);
        Assert.Contains(RoleNames.RVTAdmin, auth?.User?.Roles ?? []);

        AuthStateResponse? me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");
        Assert.True(me?.IsAuthenticated);
        Assert.Equal(AdminEmail, me?.User?.Email);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the login does not redirect to https in development API proxy path workflow for this module.
    public async Task Login_DoesNotRedirectToHttps_InDevelopmentApiProxyPath()
    {
        using SpaTestApplicationFactory factory = new("Development");
        await factory.SeedUserAsync("dev.proxy@rvt.test", Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await LoginAsync(client, "dev.proxy@rvt.test", Password);
        AuthStateResponse? me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(me?.IsAuthenticated);
        Assert.Equal("dev.proxy@rvt.test", me?.User?.Email);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the login returns generic unauthorized message for invalid credentials workflow for this module.
    public async Task Login_ReturnsGenericUnauthorizedMessage_ForInvalidCredentials()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await LoginAsync(client, AdminEmail, "bad-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("We could not find a user with that username and password.", problem?.Detail);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies login accepts registered email only and does not fall back to a legacy username.
    public async Task Login_ReturnsUnauthorized_ForLegacyUsernameOnlyMatch()
    {
        using SpaTestApplicationFactory factory = new();
        ApplicationUser user = await factory.SeedUserAsync("email.identity@rvt.test", Password, RoleNames.RVTAdmin);
        await SetUserNameAsync(factory, user, "legacy.username@rvt.test");
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await LoginAsync(client, "legacy.username@rvt.test", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("We could not find a user with that username and password.", problem?.Detail);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the login returns forbidden for disabled user workflow for this module.
    public async Task Login_ReturnsForbidden_ForDisabledUser()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(DisabledEmail, Password, RoleNames.RVTAdmin, isDisabled: true);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await LoginAsync(client, DisabledEmail, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("Your account has been disabled.", problem?.Detail);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the logout clears signed in session workflow for this module.
    public async Task Logout_ClearsSignedInSession()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage logout = await client.PostAsync("/api/auth/logout", null);
        AuthStateResponse? me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.False(me?.IsAuthenticated);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the forgot password returns same message for known and unknown email workflow for this module.
    public async Task ForgotPassword_ReturnsSameMessage_ForKnownAndUnknownEmail()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = AdminEmail });
        HttpResponseMessage unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "missing@rvt.test" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        MessageResponse? knownMessage = await known.Content.ReadFromJsonAsync<MessageResponse>();
        MessageResponse? unknownMessage = await unknown.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Equal(knownMessage?.Message, unknownMessage?.Message);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the reset password changes password with valid token workflow for this module.
    public async Task ResetPassword_ChangesPassword_WithValidToken()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        string token = await factory.GeneratePasswordResetTokenAsync(AdminEmail);
        HttpClient client = CreateClient(factory);

        HttpResponseMessage reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = AdminEmail,
            Code = token,
            Password = NewPassword,
            ConfirmPassword = NewPassword
        });
        HttpResponseMessage login = await LoginAsync(client, AdminEmail, NewPassword);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the reset password invalid token returns generic success not enumerable workflow for this module.
    public async Task ResetPassword_WithInvalidToken_ReturnsGenericSuccess_NotEnumerable()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        // A known email with an invalid token must look identical to an unknown email (200 generic),
        // so the endpoint cannot be used to confirm which emails are registered.
        HttpResponseMessage reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = AdminEmail,
            Code = "this-is-not-a-valid-token",
            Password = NewPassword,
            ConfirmPassword = NewPassword
        });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the confirm email unknown user returns same response as used link workflow for this module.
    public async Task ConfirmEmail_UnknownUser_ReturnsSameNotFoundAsUsedLink()
    {
        using SpaTestApplicationFactory factory = new();
        HttpClient client = CreateClient(factory);

        HttpResponseMessage response = await client.GetAsync($"/api/auth/confirm-email?userId={Guid.NewGuid()}&code=ZHVtbXk");
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Confirmation failed", document.RootElement.GetProperty("title").GetString());
    }

    [RequiresPostgresFact]
    // Function summary: Handles the confirm email confirms user and set initial password signs in workflow for this module.
    public async Task ConfirmEmail_ConfirmsUserAndSetInitialPasswordSignsIn()
    {
        using SpaTestApplicationFactory factory = new();
        ApplicationUser user = await factory.SeedUserAsync("new.user@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        string token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        HttpClient client = CreateClient(factory);

        HttpResponseMessage confirm = await client.GetAsync($"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}");
        ConfirmEmailResponse? confirmation = await confirm.Content.ReadFromJsonAsync<ConfirmEmailResponse>();
        HttpResponseMessage setPassword = await client.PostAsJsonAsync("/api/auth/confirm-email", new SetInitialPasswordRequest
        {
            UserId = user.Id,
            Code = encodedToken,
            NewPassword = Password,
            ConfirmPassword = Password
        });
        AuthStateResponse? me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(user.Id, confirmation?.UserId);
        Assert.Equal(HttpStatusCode.OK, setPassword.StatusCode);
        Assert.True(me?.IsAuthenticated);
        Assert.Equal(user.Email, me?.User?.Email);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the confirm email requires original code to set initial password workflow for this module.
    public async Task ConfirmEmail_RequiresOriginalCodeToSetInitialPassword()
    {
        using SpaTestApplicationFactory factory = new();
        ApplicationUser user = await factory.SeedUserAsync("verified.link@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        string token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        HttpClient client = CreateClient(factory);

        HttpResponseMessage confirm = await client.GetAsync($"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}");
        HttpResponseMessage setPassword = await client.PostAsJsonAsync("/api/auth/confirm-email", new SetInitialPasswordRequest
        {
            UserId = user.Id,
            Code = "not-a-valid-code",
            NewPassword = Password,
            ConfirmPassword = Password
        });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, setPassword.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the confirm email returns not found when link is reused workflow for this module.
    public async Task ConfirmEmail_ReturnsNotFound_WhenLinkIsReused()
    {
        using SpaTestApplicationFactory factory = new();
        ApplicationUser user = await factory.SeedUserAsync("single.use@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        string token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        HttpClient client = CreateClient(factory);
        string url = $"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}";

        HttpResponseMessage first = await client.GetAsync(url);
        HttpResponseMessage second = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the profile and password endpoints update signed in user workflow for this module.
    public async Task ProfileAndPasswordEndpoints_UpdateSignedInUser()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        ProfileResponse? profile = await client.GetFromJsonAsync<ProfileResponse>("/api/auth/profile");
        HttpResponseMessage update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = AdminEmail,
            Name = "Updated Admin",
            MobilePhone = "07123456789",
            CompanyRole = "Operations"
        });
        HttpResponseMessage password = await client.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            OldPassword = Password,
            NewPassword = NewPassword,
            ConfirmPassword = NewPassword
        });

        Assert.Equal(AdminEmail, profile?.Email);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        ProfileResponse? updated = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("Updated Admin", updated?.Name);
        Assert.Equal(HttpStatusCode.OK, password.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the protected endpoints return401 for anonymous and403 for wrong role workflow for this module.
    public async Task ProtectedEndpoints_Return401ForAnonymous_And403ForWrongRole()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller);
        HttpClient anonymousClient = CreateClient(factory);
        HttpClient installerClient = CreateClient(factory);

        HttpResponseMessage anonymousCompanies = await anonymousClient.GetAsync("/api/companies");
        HttpResponseMessage anonymousLookups = await anonymousClient.GetAsync("/api/lookups/companies?query=a");
        await LoginAsync(installerClient, InstallerEmail, Password);
        HttpResponseMessage installerCompanies = await installerClient.GetAsync("/api/companies");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCompanies.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLookups.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, installerCompanies.StatusCode);
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(SpaTestApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });
    }

    // Function summary: Updates only the Identity username so login tests can exercise legacy username/email divergence.
    private static async Task SetUserNameAsync(SpaTestApplicationFactory factory, ApplicationUser user, string userName)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser scopedUser = await userManager.FindByIdAsync(user.Id) ?? throw new InvalidOperationException($"User {user.Id} was not found.");
        IdentityResult result = await userManager.SetUserNameAsync(scopedUser, userName);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private sealed class ProblemDetailsResponse
    {
        public string? Detail { get; set; }
    }
}

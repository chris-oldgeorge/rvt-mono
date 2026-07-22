// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-26 pending Covered email-only login after removing legacy username fallback.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class AuthEndpointTests
{
    private const string AdminEmail = "admin@rvt.test";
    private const string InstallerEmail = "installer@rvt.test";
    private const string DisabledEmail = "disabled@rvt.test";
    private const string Password = "P8sSw0rd9$";
    private const string NewPassword = "N3wP8sSw0rd9$";

    [Fact]
    // Function summary: Handles the me returns anonymous state when user is not signed in workflow for this module.
    public async Task Me_ReturnsAnonymousState_WhenUserIsNotSignedIn()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);

        var auth = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.NotNull(auth);
        Assert.False(auth!.IsAuthenticated);
        Assert.Null(auth.User);
    }

    [Fact]
    // Function summary: Handles the login returns auth state and cookie for valid user workflow for this module.
    public async Task Login_ReturnsAuthStateAndCookie_ForValidUser()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        var response = await LoginAsync(client, AdminEmail, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthStateResponse>();
        Assert.True(auth?.IsAuthenticated);
        Assert.Equal(AdminEmail, auth?.User?.Email);
        Assert.Contains(RoleNames.RVTAdmin, auth?.User?.Roles ?? []);

        var me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");
        Assert.True(me?.IsAuthenticated);
        Assert.Equal(AdminEmail, me?.User?.Email);
    }

    [Fact]
    // Function summary: Handles the login does not redirect to https in development API proxy path workflow for this module.
    public async Task Login_DoesNotRedirectToHttps_InDevelopmentApiProxyPath()
    {
        using var factory = new SpaTestApplicationFactory("Development");
        await factory.SeedUserAsync("dev.proxy@rvt.test", Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        var response = await LoginAsync(client, "dev.proxy@rvt.test", Password);
        var me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(me?.IsAuthenticated);
        Assert.Equal("dev.proxy@rvt.test", me?.User?.Email);
    }

    [Fact]
    // Function summary: Handles the login returns generic unauthorized message for invalid credentials workflow for this module.
    public async Task Login_ReturnsGenericUnauthorizedMessage_ForInvalidCredentials()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        var response = await LoginAsync(client, AdminEmail, "bad-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("We could not find a user with that username and password.", problem?.Detail);
    }

    [Fact]
    // Function summary: Verifies login accepts registered email only and does not fall back to a legacy username.
    public async Task Login_ReturnsUnauthorized_ForLegacyUsernameOnlyMatch()
    {
        using var factory = new SpaTestApplicationFactory();
        var user = await factory.SeedUserAsync("email.identity@rvt.test", Password, RoleNames.RVTAdmin);
        await SetUserNameAsync(factory, user, "legacy.username@rvt.test");
        var client = CreateClient(factory);

        var response = await LoginAsync(client, "legacy.username@rvt.test", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("We could not find a user with that username and password.", problem?.Detail);
    }

    [Fact]
    // Function summary: Handles the login returns forbidden for disabled user workflow for this module.
    public async Task Login_ReturnsForbidden_ForDisabledUser()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(DisabledEmail, Password, RoleNames.RVTAdmin, isDisabled: true);
        var client = CreateClient(factory);

        var response = await LoginAsync(client, DisabledEmail, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.Equal("Your account has been disabled.", problem?.Detail);
    }

    [Fact]
    // Function summary: Handles the logout clears signed in session workflow for this module.
    public async Task Logout_ClearsSignedInSession()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.False(me?.IsAuthenticated);
    }

    [Fact]
    // Function summary: Handles the forgot password returns same message for known and unknown email workflow for this module.
    public async Task ForgotPassword_ReturnsSameMessage_ForKnownAndUnknownEmail()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = AdminEmail });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "missing@rvt.test" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        var knownMessage = await known.Content.ReadFromJsonAsync<MessageResponse>();
        var unknownMessage = await unknown.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Equal(knownMessage?.Message, unknownMessage?.Message);
    }

    [Fact]
    // Function summary: Handles the reset password changes password with valid token workflow for this module.
    public async Task ResetPassword_ChangesPassword_WithValidToken()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var token = await factory.GeneratePasswordResetTokenAsync(AdminEmail);
        var client = CreateClient(factory);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = AdminEmail,
            Code = token,
            Password = NewPassword,
            ConfirmPassword = NewPassword
        });
        var login = await LoginAsync(client, AdminEmail, NewPassword);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    // Function summary: Handles the reset password invalid token returns generic success not enumerable workflow for this module.
    public async Task ResetPassword_WithInvalidToken_ReturnsGenericSuccess_NotEnumerable()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        // A known email with an invalid token must look identical to an unknown email (200 generic),
        // so the endpoint cannot be used to confirm which emails are registered.
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = AdminEmail,
            Code = "this-is-not-a-valid-token",
            Password = NewPassword,
            ConfirmPassword = NewPassword
        });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    [Fact]
    // Function summary: Handles the confirm email unknown user returns same response as used link workflow for this module.
    public async Task ConfirmEmail_UnknownUser_ReturnsSameNotFoundAsUsedLink()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);

        var response = await client.GetAsync($"/api/auth/confirm-email?userId={Guid.NewGuid()}&code=ZHVtbXk");
        var body = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Confirmation failed", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    // Function summary: Handles the confirm email confirms user and set initial password signs in workflow for this module.
    public async Task ConfirmEmail_ConfirmsUserAndSetInitialPasswordSignsIn()
    {
        using var factory = new SpaTestApplicationFactory();
        var user = await factory.SeedUserAsync("new.user@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        var token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var client = CreateClient(factory);

        var confirm = await client.GetAsync($"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}");
        var confirmation = await confirm.Content.ReadFromJsonAsync<ConfirmEmailResponse>();
        var setPassword = await client.PostAsJsonAsync("/api/auth/confirm-email", new SetInitialPasswordRequest
        {
            UserId = user.Id,
            Code = encodedToken,
            NewPassword = Password,
            ConfirmPassword = Password
        });
        var me = await client.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(user.Id, confirmation?.UserId);
        Assert.Equal(HttpStatusCode.OK, setPassword.StatusCode);
        Assert.True(me?.IsAuthenticated);
        Assert.Equal(user.Email, me?.User?.Email);
    }

    [Fact]
    // Function summary: Handles the confirm email requires original code to set initial password workflow for this module.
    public async Task ConfirmEmail_RequiresOriginalCodeToSetInitialPassword()
    {
        using var factory = new SpaTestApplicationFactory();
        var user = await factory.SeedUserAsync("verified.link@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        var token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var client = CreateClient(factory);

        var confirm = await client.GetAsync($"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}");
        var setPassword = await client.PostAsJsonAsync("/api/auth/confirm-email", new SetInitialPasswordRequest
        {
            UserId = user.Id,
            Code = "not-a-valid-code",
            NewPassword = Password,
            ConfirmPassword = Password
        });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, setPassword.StatusCode);
    }

    [Fact]
    // Function summary: Handles the confirm email returns not found when link is reused workflow for this module.
    public async Task ConfirmEmail_ReturnsNotFound_WhenLinkIsReused()
    {
        using var factory = new SpaTestApplicationFactory();
        var user = await factory.SeedUserAsync("single.use@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        var token = await factory.GenerateEmailConfirmationTokenAsync(user.Email!);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var client = CreateClient(factory);
        var url = $"/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedToken)}";

        var first = await client.GetAsync(url);
        var second = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    // Function summary: Handles the profile and password endpoints update signed in user workflow for this module.
    public async Task ProfileAndPasswordEndpoints_UpdateSignedInUser()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/auth/profile");
        var update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = AdminEmail,
            Name = "Updated Admin",
            MobilePhone = "07123456789",
            CompanyRole = "Operations"
        });
        var password = await client.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            OldPassword = Password,
            NewPassword = NewPassword,
            ConfirmPassword = NewPassword
        });

        Assert.Equal(AdminEmail, profile?.Email);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("Updated Admin", updated?.Name);
        Assert.Equal(HttpStatusCode.OK, password.StatusCode);
    }

    [Fact]
    // Function summary: Handles the protected endpoints return401 for anonymous and403 for wrong role workflow for this module.
    public async Task ProtectedEndpoints_Return401ForAnonymous_And403ForWrongRole()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller);
        var anonymousClient = CreateClient(factory);
        var installerClient = CreateClient(factory);

        var anonymousCompanies = await anonymousClient.GetAsync("/api/companies");
        var anonymousLookups = await anonymousClient.GetAsync("/api/lookups/companies?query=a");
        await LoginAsync(installerClient, InstallerEmail, Password);
        var installerCompanies = await installerClient.GetAsync("/api/companies");

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
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var scopedUser = await userManager.FindByIdAsync(user.Id) ?? throw new InvalidOperationException($"User {user.Id} was not found.");
        var result = await userManager.SetUserNameAsync(scopedUser, userName);
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

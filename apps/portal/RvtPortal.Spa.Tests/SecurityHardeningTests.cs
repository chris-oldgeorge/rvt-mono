// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-24 pending Documented shared-key report-content APIs as intentional ASP.NET anonymous routes.
// - 2026-07-22 pending Covered configured auth origins, confirmed email changes, uniform reset failures, and explicit proxy trust.
// - 2026-07-22 pending Covered admin pending-email changes and rollback-safe confirmation retries.
// - 2026-07-22 pending Proved relational rollback on result/exception and unconfirmed invitation replacement onboarding.

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RVT.Entities;
using RvtPortal.Application.Notifications;
using RvtPortal.Application.Ports.Notifications;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Users;
using RvtPortal.Spa.Data;

using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public class SecurityHardeningTests
{
    private const string AdminEmail = "security.admin@rvt.test";
    private const string Password = "P8sSw0rd9$";

    [RequiresPostgresFact]
    // Function summary: Handles the API controller endpoints have explicit authorization decision workflow for this module.
    public void ApiControllerEndpoints_HaveExplicitAuthorizationDecision()
    {
        using SpaTestApplicationFactory factory = new();
        factory.CreateClient();

        var endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                Action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()
            })
            .Where(item => item.Action is not null && IsApiRoute(item.Endpoint))
            .ToList();

        List<string> missingDecision = [.. endpoints
            .Where(item => !HasAuthorizationDecision(item.Endpoint))
            .Select(item => $"{item.Action!.ControllerName}.{item.Action.ActionName} => {item.Endpoint.RoutePattern.RawText}")];
        List<string> undocumentedAnonymous = [.. endpoints
            .Where(item => HasAnonymousDecision(item.Endpoint) && !IsDocumentedAnonymousApiRoute(item.Endpoint))
            .Select(item => $"{item.Action!.ControllerName}.{item.Action.ActionName} => {item.Endpoint.RoutePattern.RawText}")];

        Assert.NotEmpty(endpoints);
        Assert.Empty(missingDecision);
        Assert.Empty(undocumentedAnonymous);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the cookie auth session uses strict same site cookie workflow for this module.
    public async Task CookieAuthSession_UsesStrictSameSiteCookie()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);

        using HttpResponseMessage response = await LoginAsync(client);
        IEnumerable<string> setCookie = response.Headers.GetValues("Set-Cookie");

        Assert.Contains(setCookie, cookie => cookie.Contains(".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [RequiresPostgresFact]
    // Function summary: Handles the unsafe API mutation with cross site origin is blocked before controller workflow for this module.
    public async Task UnsafeApiMutation_WithCrossSiteOrigin_IsBlockedBeforeController()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client);
        HttpRequestMessage request = new(HttpMethod.Put, "/api/auth/profile")
        {
            Content = JsonContent.Create(new UpdateProfileRequest
            {
                Email = AdminEmail,
                Name = "Cross Site Name",
                MobilePhone = "07123456789",
                CompanyRole = "Operations"
            })
        };
        request.Headers.Add("Origin", "https://attacker.example");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Cross-site API request blocked.", document.RootElement.GetProperty("title").GetString());
    }

    [RequiresPostgresFact]
    // Function summary: Handles the API responses include server timing header workflow for this module.
    public async Task ApiResponses_IncludeServerTimingHeader()
    {
        using SpaTestApplicationFactory factory = new();
        HttpClient client = CreateClient(factory);

        using HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.True(response.Headers.TryGetValues("Server-Timing", out IEnumerable<string>? values));
        Assert.Contains(values, value => value.StartsWith("app;dur=", StringComparison.OrdinalIgnoreCase));
    }

    [RequiresPostgresFact]
    // Function summary: Handles the representative read endpoints include server timing for performance tracking workflow for this module.
    public async Task RepresentativeReadEndpoints_IncludeServerTimingForPerformanceTracking()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainCompaniesAsync(new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "Security Performance Co",
            Contracts = []
        });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client);

        foreach (string? path in new[] { "/api/companies?page=1&pageSize=5", "/api/dashboard/summary" })
        {
            using HttpResponseMessage response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Server-Timing", out IEnumerable<string>? values), $"Missing Server-Timing for {path}.");
            Assert.Contains(values, value => value.StartsWith("app;dur=", StringComparison.OrdinalIgnoreCase));
        }
    }

    [RequiresPostgresFact]
    // Function summary: Handles the mutation requests create safe audit log without payload values workflow for this module.
    public async Task MutationRequests_CreateSafeAuditLogWithoutPayloadValues()
    {
        ListLoggerProvider logs = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(logs);
                logging.SetMinimumLevel(LogLevel.Information);
            });
        });
        HttpClient client = CreateClient(app);
        await LoginAsync(client);
        logs.Clear();

        using HttpResponseMessage response = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = AdminEmail,
            Name = "Audited Admin Secret",
            MobilePhone = "07123456789",
            CompanyRole = "Operations"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(logs.Messages, message => message.Contains("API mutation PUT /api/auth/profile completed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("Audited Admin Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("07123456789", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    // Function summary: Handles the request DTO value type properties are nullable or explicitly required workflow for this module.
    public void RequestDtoValueTypeProperties_AreNullableOrExplicitlyRequired()
    {
        List<string> violations = [.. typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "RvtPortal.Spa.Api" && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => IsNonNullableValueType(property) && !IsRequired(property))
                .Select(property => $"{type.Name}.{property.Name}"))];

        Assert.Empty(violations);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the unsafe API mutation with same site fetch metadata is blocked workflow for this module.
    public async Task UnsafeApiMutation_WithSameSiteFetchMetadata_IsBlockedBeforeController()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client);
        HttpRequestMessage request = new(HttpMethod.Put, "/api/auth/profile")
        {
            Content = JsonContent.Create(new UpdateProfileRequest
            {
                Email = AdminEmail,
                Name = "Same Site Name",
                MobilePhone = "07123456789",
                CompanyRole = "Operations"
            })
        };
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Cross-site API request blocked.", document.RootElement.GetProperty("title").GetString());
    }

    [RequiresPostgresFact]
    // Function summary: Handles the lookups endpoint requires admin role workflow for this module.
    public async Task LookupsEndpoint_RequiresAdminRole()
    {
        const string companyUserEmail = "company.lookup@rvt.test";
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(companyUserEmail, Password, RoleNames.CompanyUser);
        HttpClient client = CreateClient(factory);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = companyUserEmail,
            Password = Password,
            RememberMe = true
        });

        using HttpResponseMessage response = await client.GetAsync("/api/lookups/companies?query=a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the lookups endpoint allows admin role workflow for this module.
    public async Task LookupsEndpoint_AllowsAdminRole()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client);

        using HttpResponseMessage response = await client.GetAsync("/api/lookups/companies?query=a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the API responses include hardening security headers workflow for this module.
    public async Task ApiResponses_IncludeHardeningSecurityHeaders()
    {
        using SpaTestApplicationFactory factory = new();
        HttpClient client = CreateClient(factory);

        using HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out IEnumerable<string>? nosniff));
        Assert.Contains(nosniff, value => value.Equals("nosniff", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out IEnumerable<string>? frameOptions));
        Assert.Contains(frameOptions, value => value.Equals("DENY", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out _));
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out IEnumerable<string>? csp));
        Assert.Contains(csp, value => value.Contains("frame-ancestors 'none'", StringComparison.OrdinalIgnoreCase));
    }

    [RequiresPostgresFact]
    // Function summary: Handles the auth login endpoint is rate limited after configured attempts workflow for this module.
    public async Task AuthLoginEndpoint_IsRateLimited_AfterConfiguredAttempts()
    {
        using SpaTestApplicationFactory factory = new(authRatePermitLimit: 3);
        HttpClient client = CreateClient(factory);

        List<HttpStatusCode> statuses = new();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "ratelimit@rvt.test",
                Password = "WrongPassword1$",
                RememberMe = false
            });
            statuses.Add(response.StatusCode);
        }

        // Permit limit is 3, so the fourth and later attempts in the window are rejected.
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies a disallowed Host is rejected before any password-reset email can be delivered.
    public async Task ForgotPassword_WithDisallowedHost_IsRejectedBeforeDelivery()
    {
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        HttpClient client = CreateClient(app);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest { Email = AdminEmail })
        };
        request.Headers.Host = "attacker.example";

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(messenger.PasswordResetCallbackUrl);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies missing public-origin configuration keeps the anonymous response generic and suppresses delivery.
    public async Task ForgotPassword_WithoutPublicBaseUrl_ReturnsGenericSuccessWithoutDelivery()
    {
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, publicBaseUrl: "");
        HttpClient client = CreateClient(app);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest { Email = AdminEmail })
        };
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(messenger.PasswordResetCallbackUrl);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies configured public origin controls password-reset links for an allowed request host.
    public async Task ForgotPassword_WithPublicBaseUrl_SendsConfiguredHostLink()
    {
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        HttpClient client = CreateClient(app);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest { Email = AdminEmail })
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("https://portal.example.test/reset-password?", messenger.PasswordResetCallbackUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker.example", messenger.PasswordResetCallbackUrl, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies the sibling admin notification workflow cannot fall back to an attacker-controlled request origin.
    public async Task AdminAccountNotification_WithoutPublicBaseUrl_DoesNotSendHostDerivedLink()
    {
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        ApplicationUser user = await factory.SeedUserAsync("admin-created.user@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, publicBaseUrl: "");
        using IServiceScope scope = app.Services.CreateScope();
        IUserAccountNotificationService notifications = scope.ServiceProvider.GetRequiredService<IUserAccountNotificationService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => notifications.SendPasswordSetAsync(
            user,
            new UserAccountRequestOrigin("https", "attacker.example", "")));

        Assert.Null(messenger.EmailChangeCallbackUrl);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies profile email changes remain pending until the Identity change-email token is confirmed.
    public async Task ProfileEmailChange_RemainsPendingUntilConfirmation()
    {
        const string newEmail = "security.changed@rvt.test";
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        ApplicationUser seededUser = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        HttpClient client = CreateClient(app);
        await LoginAsync(client);

        using HttpResponseMessage update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = newEmail,
            Name = "Pending Email Admin",
            MobilePhone = "07123456789",
            CompanyRole = "Operations"
        });
        ProfileResponse? pendingProfile = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        ApplicationUser pendingUser = await FindUserByIdAsync(app, seededUser.Id);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(AdminEmail, pendingProfile?.Email);
        Assert.Equal(AdminEmail, pendingUser.Email);
        Assert.Equal(AdminEmail, pendingUser.UserName);
        Assert.True(pendingUser.EmailConfirmed);
        Assert.Equal("Pending Email Admin", pendingUser.Name);
        Assert.Equal(newEmail, messenger.EmailChangeRecipient);
        Assert.NotNull(messenger.EmailChangeCallbackUrl);

        Uri confirmationUri = new(messenger.EmailChangeCallbackUrl);
        using HttpResponseMessage confirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser confirmedUser = await FindUserByIdAsync(app, seededUser.Id);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(newEmail, confirmedUser.Email);
        Assert.Equal(newEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies an admin email edit stays pending while non-email edits apply and reset delivery uses the confirmed address.
    public async Task AdminEmailChange_RemainsPendingAndResetUsesConfirmedAddress()
    {
        const string originalEmail = "admin.target@rvt.test";
        const string requestedEmail = "admin.requested@rvt.test";
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTMasterAdmin);
        ApplicationUser target = await factory.SeedUserAsync(originalEmail, Password, RoleNames.RVTAdmin);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        HttpClient client = CreateClient(app);
        await LoginAsync(client);

        using HttpResponseMessage update = await client.PutAsJsonAsync($"/api/users/{target.Id}", new UserMutationRequest
        {
            Email = requestedEmail,
            Name = "Pending Admin Target",
            MobilePhone = "07111111111",
            Role = RoleNames.RVTAdmin
        });
        ApplicationUser pendingUser = await FindUserByIdAsync(app, target.Id);
        using HttpResponseMessage reset = await client.PostAsync($"/api/users/{target.Id}/reset-password-link", null);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(originalEmail, pendingUser.Email);
        Assert.Equal(originalEmail, pendingUser.UserName);
        Assert.True(pendingUser.EmailConfirmed);
        Assert.Equal("Pending Admin Target", pendingUser.Name);
        Assert.Equal("07111111111", pendingUser.PhoneNumber);
        Assert.Equal(requestedEmail, messenger.EmailChangeRecipient);
        Assert.NotNull(messenger.EmailChangeCallbackUrl);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(originalEmail, messenger.PasswordResetRecipient);

        Uri confirmationUri = new(messenger.EmailChangeCallbackUrl);
        using HttpResponseMessage confirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser confirmedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(requestedEmail, confirmedUser.Email);
        Assert.Equal(requestedEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies username failure restores every Identity email field and leaves the same token safe to retry.
    public async Task EmailChangeConfirmation_WhenUserNameUpdateFails_RollsBackAndTokenCanRetry()
    {
        const string requestedEmail = "reserved.username@rvt.test";
        RecordingAccountMessenger messenger = new();
        await using SqliteConnection identityConnection = new("Data Source=:memory:");
        await identityConnection.OpenAsync();
        using SpaTestApplicationFactory factory = new();
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(
            factory,
            messenger,
            "https://portal.example.test",
            identityConnection: identityConnection);
        await EnsureIdentityDatabaseAsync(app);
        ApplicationUser target = await SeedUserAsync(app, AdminEmail, Password, RoleNames.RVTAdmin);
        ApplicationUser blocker = await SeedUserAsync(app, "blocker.email@rvt.test", Password, RoleNames.RVTAdmin);
        await SetUserNameAsync(app, blocker.Id, requestedEmail);
        HttpClient client = CreateClient(app);
        await LoginAsync(client);
        using HttpResponseMessage update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = requestedEmail,
            Name = "Retry Safe Admin",
            MobilePhone = "07222222222",
            CompanyRole = "Operations"
        });
        Uri confirmationUri = new(messenger.EmailChangeCallbackUrl!);

        using HttpResponseMessage firstConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser rolledBackUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.BadRequest, firstConfirmation.StatusCode);
        Assert.Equal(AdminEmail, rolledBackUser.Email);
        Assert.Equal(AdminEmail, rolledBackUser.UserName);
        Assert.True(rolledBackUser.EmailConfirmed);

        await SetUserNameAsync(app, blocker.Id, "released.username@rvt.test");
        using HttpResponseMessage retryConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser confirmedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, retryConfirmation.StatusCode);
        Assert.Equal(requestedEmail, confirmedUser.Email);
        Assert.Equal(requestedEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies an exception after email persistence rolls back the relational transaction and preserves token retryability.
    public async Task EmailChangeConfirmation_WhenUserNameUpdateThrows_RollsBackTransactionAndTokenCanRetry()
    {
        const string requestedEmail = "exception.retry@rvt.test";
        RecordingAccountMessenger messenger = new();
        ThrowWhenEmailAndUserNameAlignValidator throwingValidator = new(requestedEmail);
        await using SqliteConnection identityConnection = new("Data Source=:memory:");
        await identityConnection.OpenAsync();
        using SpaTestApplicationFactory factory = new();
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(
            factory,
            messenger,
            "https://portal.example.test",
            identityConnection: identityConnection,
            userValidator: throwingValidator);
        await EnsureIdentityDatabaseAsync(app);
        using (IServiceScope validatorScope = app.Services.CreateScope())
        {
            Assert.Contains(
                validatorScope.ServiceProvider.GetServices<IUserValidator<ApplicationUser>>(),
                validator => ReferenceEquals(validator, throwingValidator));
        }

        ApplicationUser target = await SeedUserAsync(app, AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(app);
        await LoginAsync(client);
        using HttpResponseMessage update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = requestedEmail,
            Name = "Exception Safe Admin",
            MobilePhone = "07333333333",
            CompanyRole = "Operations"
        });
        Uri confirmationUri = new(messenger.EmailChangeCallbackUrl!);

        using HttpResponseMessage failedConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser rolledBackUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.InternalServerError, failedConfirmation.StatusCode);
        Assert.Equal(AdminEmail, rolledBackUser.Email);
        Assert.Equal(AdminEmail, rolledBackUser.UserName);
        Assert.True(rolledBackUser.EmailConfirmed);

        throwingValidator.ThrowEnabled = false;
        using HttpResponseMessage retryConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        ApplicationUser confirmedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, retryConfirmation.StatusCode);
        Assert.Equal(requestedEmail, confirmedUser.Email);
        Assert.Equal(requestedEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies changing an invited user's destination restarts normal confirmation and initial-password onboarding.
    public async Task AdminEmailChange_ForUnconfirmedInvite_ReissuesOnboardingToReplacementAddress()
    {
        const string originalEmail = "invited.original@rvt.test";
        const string requestedEmail = "invited.replacement@rvt.test";
        const string initialPassword = "N3wInvitedPass!";
        RecordingAccountMessenger messenger = new();
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTMasterAdmin);
        ApplicationUser target = await factory.SeedUserAsync(originalEmail, null, RoleNames.RVTAdmin, emailConfirmed: false);
        string oldToken = await factory.GenerateEmailConfirmationTokenAsync(originalEmail);
        string oldResetToken = await factory.GeneratePasswordResetTokenAsync(originalEmail);
        string oldEncodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(oldToken));
        string oldConfirmationPath = $"/api/auth/confirm-email?userId={Uri.EscapeDataString(target.Id)}&code={Uri.EscapeDataString(oldEncodedToken)}";
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        HttpClient adminClient = CreateClient(app);
        HttpClient invitedClient = CreateClient(app);
        await LoginAsync(adminClient);

        using HttpResponseMessage update = await adminClient.PutAsJsonAsync($"/api/users/{target.Id}", new UserMutationRequest
        {
            Email = requestedEmail,
            Name = "Replacement Invite",
            MobilePhone = "07444444444",
            Role = RoleNames.RVTAdmin
        });
        ApplicationUser pendingUser = await FindUserByIdAsync(app, target.Id);
        using HttpResponseMessage resetBeforeConfirmation = await invitedClient.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = requestedEmail,
            Code = oldResetToken,
            Password = initialPassword,
            ConfirmPassword = initialPassword
        });
        using HttpResponseMessage loginBeforeConfirmation = await invitedClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = requestedEmail,
            Password = initialPassword,
            RememberMe = false
        });
        using HttpResponseMessage forgotBeforeConfirmation = await invitedClient.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = requestedEmail });
        using HttpResponseMessage oldConfirmation = await invitedClient.GetAsync(oldConfirmationPath);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(requestedEmail, pendingUser.Email);
        Assert.Equal(requestedEmail, pendingUser.UserName);
        Assert.False(pendingUser.EmailConfirmed);
        Assert.Equal("Replacement Invite", pendingUser.Name);
        Assert.Equal("07444444444", pendingUser.PhoneNumber);
        Assert.Equal(requestedEmail, messenger.PasswordSetRecipient);
        Assert.NotNull(messenger.PasswordSetCallbackUrl);
        Assert.Equal(HttpStatusCode.OK, resetBeforeConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginBeforeConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forgotBeforeConfirmation.StatusCode);
        Assert.Null(messenger.PasswordResetRecipient);
        Assert.Equal(HttpStatusCode.NotFound, oldConfirmation.StatusCode);

        Uri newConfirmationUri = new(messenger.PasswordSetCallbackUrl);
        Dictionary<string, StringValues> newConfirmationQuery = QueryHelpers.ParseQuery(newConfirmationUri.Query);
        string newConfirmationCode = newConfirmationQuery["code"].ToString();
        string newConfirmationPath = $"/api/auth/confirm-email?userId={Uri.EscapeDataString(target.Id)}&code={Uri.EscapeDataString(newConfirmationCode)}";
        using HttpResponseMessage newConfirmation = await invitedClient.GetAsync(newConfirmationPath);
        using HttpResponseMessage setInitialPassword = await invitedClient.PostAsJsonAsync("/api/auth/confirm-email", new SetInitialPasswordRequest
        {
            UserId = target.Id,
            Code = newConfirmationCode,
            NewPassword = initialPassword,
            ConfirmPassword = initialPassword
        });
        AuthStateResponse? authState = await invitedClient.GetFromJsonAsync<AuthStateResponse>("/api/auth/me");
        ApplicationUser onboardedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, newConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, setInitialPassword.StatusCode);
        Assert.True(authState?.IsAuthenticated);
        Assert.Equal(requestedEmail, authState?.User?.Email);
        Assert.True(onboardedUser.EmailConfirmed);
        Assert.Equal(requestedEmail, onboardedUser.Email);
        Assert.Equal(requestedEmail, onboardedUser.UserName);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies email-provider failures are indistinguishable from unknown accounts to anonymous callers.
    public async Task ForgotPassword_EmailProviderFailure_MatchesUnknownAccountResponse()
    {
        const string providerDetail = "sendgrid-private-diagnostic";
        ListLoggerProvider logs = new();
        RecordingAccountMessenger messenger = new(EmailDeliveryResult.Failure(providerDetail));
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync("unconfirmed@rvt.test", Password, RoleNames.RVTAdmin, emailConfirmed: false);
        using WebApplicationFactory<Program> app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test", logs);
        HttpClient client = CreateClient(app);

        using HttpResponseMessage known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = AdminEmail });
        using HttpResponseMessage unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "missing@rvt.test" });
        using HttpResponseMessage unconfirmed = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "unconfirmed@rvt.test" });
        string knownBody = await known.Content.ReadAsStringAsync();
        string unknownBody = await unknown.Content.ReadAsStringAsync();
        string unconfirmedBody = await unconfirmed.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Equal(unconfirmed.StatusCode, known.StatusCode);
        Assert.Equal(unknownBody, knownBody);
        Assert.Equal(unconfirmedBody, knownBody);
        Assert.DoesNotContain(providerDetail, knownBody, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message =>
            message.Contains(providerDetail, StringComparison.Ordinal) &&
            message.Contains("CorrelationId", StringComparison.Ordinal));
    }

    [RequiresPostgresTheory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "127.0.0.1")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "127.0.0.0/8")]
    // Function summary: Verifies explicitly trusted proxy addresses and networks can supply the original HTTPS scheme.
    public async Task ForwardedProto_FromConfiguredProxyOrNetwork_IsHonored(string settingKey, string settingValue)
    {
        using SpaTestApplicationFactory factory = new();
        using WebApplicationFactory<Program> app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(settingKey, settingValue);
        });

        HttpContext context = await app.Server.SendAsync(request => ConfigureForwardedRequest(request, IPAddress.Loopback));

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("203.0.113.25"), context.Connection.RemoteIpAddress);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies forwarded headers are ignored when the immediate proxy is not explicitly trusted.
    public async Task ForwardedProto_FromUntrustedProxy_IsIgnored()
    {
        using SpaTestApplicationFactory factory = new(authRatePermitLimit: 1);
        using WebApplicationFactory<Program> app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", "198.51.100.10");
        });

        HttpContext context = await app.Server.SendAsync(request => ConfigureForwardedRequest(request, IPAddress.Loopback));

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies forwarded-host trust remains disabled and framework loopback defaults are cleared.
    public void ForwardedHeaders_TrustOnlyConfiguredSources_AndNeverForwardedHost()
    {
        using SpaTestApplicationFactory factory = new();
        using WebApplicationFactory<Program> app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", "198.51.100.10");
        });
        _ = app.CreateClient();

        ForwardedHeadersOptions options = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.Parse("198.51.100.10"), options.KnownProxies.Single());
        Assert.Empty(options.KnownIPNetworks);
    }

    [RequiresPostgresFact]
    // Function summary: Handles the supplied correlation id with unsafe characters is not reflected workflow for this module.
    public async Task SuppliedCorrelationId_WithUnsafeCharacters_IsNotReflected()
    {
        using SpaTestApplicationFactory factory = new();
        HttpClient client = CreateClient(factory);
        const string malicious = "forged value <script> with spaces";
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/health");
        request.Headers.TryAddWithoutValidation(ApiDiagnostics.CorrelationIdHeader, malicious);

        using HttpResponseMessage response = await client.SendAsync(request);
        string? echoed = response.Headers.TryGetValues(ApiDiagnostics.CorrelationIdHeader, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

        Assert.NotNull(echoed);
        Assert.NotEqual(malicious, echoed);
        Assert.DoesNotContain("<", echoed);
        Assert.DoesNotContain(" ", echoed);
    }

    // Function summary: Evaluates API route for the current decision point.
    private static bool IsApiRoute(RouteEndpoint endpoint)
    {
        return endpoint.RoutePattern.RawText?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true;
    }

    // Function summary: Evaluates authorization decision for the current decision point.
    private static bool HasAuthorizationDecision(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
    }

    // Function summary: Evaluates anonymous decision for the current decision point.
    private static bool HasAnonymousDecision(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
    }

    // Function summary: Evaluates documented anonymous API route for the current decision point.
    private static bool IsDocumentedAnonymousApiRoute(RouteEndpoint endpoint)
    {
        string route = endpoint.RoutePattern.RawText ?? "";
        return route.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase) ||
            route.StartsWith("api/health", StringComparison.OrdinalIgnoreCase) ||
            route.StartsWith("api/report-content", StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Evaluates non nullable value type for the current decision point.
    private static bool IsNonNullableValueType(PropertyInfo property)
    {
        return property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null;
    }

    // Function summary: Evaluates required for the current decision point.
    private static bool IsRequired(PropertyInfo property)
    {
        return property.GetCustomAttribute<RequiredAttribute>() is not null ||
            property.GetCustomAttribute<RequiredMemberAttribute>() is not null;
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = AdminEmail,
            Password = Password,
            RememberMe = true
        });
    }

    // Function summary: Configures one test-server request with an explicit immediate peer and forwarded metadata.
    private static void ConfigureForwardedRequest(HttpContext context, IPAddress immediatePeer)
    {
        context.Connection.RemoteIpAddress = immediatePeer;
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/api/health";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.25";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
    }

    // Function summary: Configures real auth workflows with a deterministic outbound-account-message boundary.
    private static WebApplicationFactory<Program> ConfigureAuthDelivery(
        SpaTestApplicationFactory factory,
        IAccountMessenger messenger,
        string publicBaseUrl,
        ILoggerProvider? loggerProvider = null,
        SqliteConnection? identityConnection = null,
        IUserValidator<ApplicationUser>? userValidator = null)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:SkipPasswordResetEmail"] = "false",
                    ["Spa:PublicBaseUrl"] = publicBaseUrl,
                    // Test clients use localhost by default; the explicit public host remains the only deployed origin.
                    ["AllowedHosts"] = "portal.example.test;localhost"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountMessenger>();
                services.AddSingleton(messenger);
                if (identityConnection is not null)
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(identityConnection));
                }

                if (userValidator is not null)
                {
                    services.AddSingleton(userValidator);
                }
            });
            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging =>
                {
                    logging.AddProvider(loggerProvider);
                    logging.SetMinimumLevel(LogLevel.Information);
                });
            }
        });
    }

    // Function summary: Creates the relational Identity schema used to prove real transaction rollback behavior.
    private static async Task EnsureIdentityDatabaseAsync(WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        Assert.True(context.Database.IsRelational());
    }

    // Function summary: Seeds a user and role through an arbitrary application factory, including relational Identity controls.
    private static async Task<ApplicationUser> SeedUserAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string? password,
        string roleName)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (string? role in new[] { RoleNames.RVTMasterAdmin, RoleNames.RVTAdmin, RoleNames.RVTInstaller, RoleNames.CompanyUser })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        ApplicationUser user = new()
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            Name = email.Split('@')[0]
        };
        IdentityResult createResult = password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
        IdentityResult addRoleResult = await userManager.AddToRoleAsync(user, roleName);
        Assert.True(addRoleResult.Succeeded, string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
        return user;
    }

    // Function summary: Loads one Identity user from the application under test for persistence assertions.
    private static async Task<ApplicationUser> FindUserByIdAsync(WebApplicationFactory<Program> factory, string userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User {userId} was not found.");
    }

    // Function summary: Sets one test user's username through Identity to create or release deterministic collision state.
    private static async Task SetUserNameAsync(WebApplicationFactory<Program> factory, string userId, string userName)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User {userId} was not found.");
        IdentityResult result = await userManager.SetUserNameAsync(user, userName);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private sealed class RecordingAccountMessenger : IAccountMessenger
    {
        private readonly EmailDeliveryResult delivery;

        public RecordingAccountMessenger(EmailDeliveryResult? delivery = null)
        {
            this.delivery = delivery ?? EmailDeliveryResult.Success();
        }

        public string? PasswordResetCallbackUrl { get; private set; }
        public string? PasswordResetRecipient { get; private set; }
        public string? PasswordSetRecipient { get; private set; }
        public string? PasswordSetCallbackUrl { get; private set; }
        public string? EmailChangeRecipient { get; private set; }
        public string? EmailChangeCallbackUrl { get; private set; }

        public Task<EmailDeliveryResult> SendPasswordSetAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        {
            PasswordSetRecipient = email;
            PasswordSetCallbackUrl = callbackUrl;
            return Task.FromResult(delivery);
        }

        public Task<EmailDeliveryResult> SendPasswordResetAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        {
            PasswordResetRecipient = email;
            PasswordResetCallbackUrl = callbackUrl;
            return Task.FromResult(delivery);
        }

        public Task<EmailDeliveryResult> SendEmailChangeAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        {
            EmailChangeRecipient = email;
            EmailChangeCallbackUrl = callbackUrl;
            return Task.FromResult(delivery);
        }
    }

    private sealed class ThrowWhenEmailAndUserNameAlignValidator : IUserValidator<ApplicationUser>
    {
        private readonly string email;

        public ThrowWhenEmailAndUserNameAlignValidator(string email)
        {
            this.email = email;
        }

        public bool ThrowEnabled { get; set; } = true;

        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
        {
            if (ThrowEnabled &&
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(user.UserName, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Forced username persistence exception.");
            }

            return Task.FromResult(IdentityResult.Success);
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        // Function summary: Handles the new workflow for this module.
        private readonly ConcurrentQueue<string> messages = new();

        // Function summary: Maps list into the shape required by callers.
        public IReadOnlyCollection<string> Messages => [.. messages];

        // Function summary: Creates logger data for the current workflow.
        public ILogger CreateLogger(string categoryName)
        {
            return new ListLogger(categoryName, messages);
        }

        // Function summary: Handles the clear workflow for this module.
        public void Clear()
        {
            while (messages.TryDequeue(out _))
            {
            }
        }

        // Function summary: Handles the dispose workflow for this module.
        public void Dispose()
        {
        }
    }

    private sealed class ListLogger : ILogger
    {
        private readonly string categoryName;
        private readonly ConcurrentQueue<string> messages;

        // Function summary: Handles the list logger workflow for this module.
        public ListLogger(string categoryName, ConcurrentQueue<string> messages)
        {
            this.categoryName = categoryName;
            this.messages = messages;
        }

        // Function summary: Handles the tstate workflow for this module.
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Function summary: Evaluates enabled for the current decision point.
        public bool IsEnabled(LogLevel logLevel) => true;

        // Function summary: Handles the tstate workflow for this module.
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue($"{logLevel}: {categoryName}: {formatter(state, exception)}");
        }
    }
}

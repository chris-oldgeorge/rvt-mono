// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-24 pending Documented shared-key report-content APIs as intentional ASP.NET anonymous routes.
// - 2026-07-22 pending Covered configured auth origins, confirmed email changes, uniform reset failures, and explicit proxy trust.
// - 2026-07-22 pending Covered admin pending-email changes and rollback-safe confirmation retries.

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic.Notifications;
using RVT.BusinessLogic.Ports.Notifications;
using RVT.Entities;
using RvtPortal.Spa.Application.Users;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class SecurityHardeningTests
{
    private const string AdminEmail = "security.admin@rvt.test";
    private const string Password = "P8sSw0rd9$";

    [Fact]
    // Function summary: Handles the API controller endpoints have explicit authorization decision workflow for this module.
    public void ApiControllerEndpoints_HaveExplicitAuthorizationDecision()
    {
        using var factory = new SpaTestApplicationFactory();
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

        var missingDecision = endpoints
            .Where(item => !HasAuthorizationDecision(item.Endpoint))
            .Select(item => $"{item.Action!.ControllerName}.{item.Action.ActionName} => {item.Endpoint.RoutePattern.RawText}")
            .ToList();
        var undocumentedAnonymous = endpoints
            .Where(item => HasAnonymousDecision(item.Endpoint) && !IsDocumentedAnonymousApiRoute(item.Endpoint))
            .Select(item => $"{item.Action!.ControllerName}.{item.Action.ActionName} => {item.Endpoint.RoutePattern.RawText}")
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.Empty(missingDecision);
        Assert.Empty(undocumentedAnonymous);
    }

    [Fact]
    // Function summary: Handles the cookie auth session uses strict same site cookie workflow for this module.
    public async Task CookieAuthSession_UsesStrictSameSiteCookie()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);

        using var response = await LoginAsync(client);
        var setCookie = response.Headers.GetValues("Set-Cookie");

        Assert.Contains(setCookie, cookie => cookie.Contains(".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    // Function summary: Handles the unsafe API mutation with cross site origin is blocked before controller workflow for this module.
    public async Task UnsafeApiMutation_WithCrossSiteOrigin_IsBlockedBeforeController()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
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

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Cross-site API request blocked.", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    // Function summary: Handles the API responses include server timing header workflow for this module.
    public async Task ApiResponses_IncludeServerTimingHeader()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/health");

        Assert.True(response.Headers.TryGetValues("Server-Timing", out var values));
        Assert.Contains(values, value => value.StartsWith("app;dur=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    // Function summary: Handles the representative read endpoints include server timing for performance tracking workflow for this module.
    public async Task RepresentativeReadEndpoints_IncludeServerTimingForPerformanceTracking()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainCompaniesAsync(new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "Security Performance Co",
            Contracts = []
        });
        var client = CreateClient(factory);
        await LoginAsync(client);

        foreach (var path in new[] { "/api/companies?page=1&pageSize=5", "/api/dashboard/summary" })
        {
            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Server-Timing", out var values), $"Missing Server-Timing for {path}.");
            Assert.Contains(values, value => value.StartsWith("app;dur=", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    // Function summary: Handles the mutation requests create safe audit log without payload values workflow for this module.
    public async Task MutationRequests_CreateSafeAuditLogWithoutPayloadValues()
    {
        var logs = new ListLoggerProvider();
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(logs);
                logging.SetMinimumLevel(LogLevel.Information);
            });
        });
        var client = CreateClient(app);
        await LoginAsync(client);
        logs.Clear();

        using var response = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
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
        var violations = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "RvtPortal.Spa.Api" && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => IsNonNullableValueType(property) && !IsRequired(property))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    // Function summary: Handles the unsafe API mutation with same site fetch metadata is blocked workflow for this module.
    public async Task UnsafeApiMutation_WithSameSiteFetchMetadata_IsBlockedBeforeController()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
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

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Cross-site API request blocked.", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    // Function summary: Handles the lookups endpoint requires admin role workflow for this module.
    public async Task LookupsEndpoint_RequiresAdminRole()
    {
        const string companyUserEmail = "company.lookup@rvt.test";
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(companyUserEmail, Password, RoleNames.CompanyUser);
        var client = CreateClient(factory);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = companyUserEmail,
            Password = Password,
            RememberMe = true
        });

        using var response = await client.GetAsync("/api/lookups/companies?query=a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    // Function summary: Handles the lookups endpoint allows admin role workflow for this module.
    public async Task LookupsEndpoint_AllowsAdminRole()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client);

        using var response = await client.GetAsync("/api/lookups/companies?query=a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    // Function summary: Handles the API responses include hardening security headers workflow for this module.
    public async Task ApiResponses_IncludeHardeningSecurityHeaders()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/health");

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains(nosniff, value => value.Equals("nosniff", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Contains(frameOptions, value => value.Equals("DENY", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out _));
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var csp));
        Assert.Contains(csp, value => value.Contains("frame-ancestors 'none'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    // Function summary: Handles the auth login endpoint is rate limited after configured attempts workflow for this module.
    public async Task AuthLoginEndpoint_IsRateLimited_AfterConfiguredAttempts()
    {
        using var factory = new SpaTestApplicationFactory(authRatePermitLimit: 3);
        var client = CreateClient(factory);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
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

    [Fact]
    // Function summary: Verifies an attacker-controlled Host cannot become a password-reset email link when public origin configuration is absent.
    public async Task ForgotPassword_WithoutPublicBaseUrl_DoesNotSendHostDerivedLink()
    {
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = ConfigureAuthDelivery(factory, messenger, publicBaseUrl: "");
        var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest { Email = AdminEmail })
        };
        request.Headers.Host = "attacker.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(messenger.PasswordResetCallbackUrl);
    }

    [Fact]
    // Function summary: Verifies configured public origin controls password-reset links even when Host is malicious.
    public async Task ForgotPassword_WithPublicBaseUrl_SendsConfiguredHostLink()
    {
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest { Email = AdminEmail })
        };
        request.Headers.Host = "attacker.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("https://portal.example.test/reset-password?", messenger.PasswordResetCallbackUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker.example", messenger.PasswordResetCallbackUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Verifies the sibling admin notification workflow cannot fall back to an attacker-controlled request origin.
    public async Task AdminAccountNotification_WithoutPublicBaseUrl_DoesNotSendHostDerivedLink()
    {
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        var user = await factory.SeedUserAsync("admin-created.user@rvt.test", null, RoleNames.CompanyUser, emailConfirmed: false);
        using var app = ConfigureAuthDelivery(factory, messenger, publicBaseUrl: "");
        using var scope = app.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<IUserAccountNotificationService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => notifications.SendPasswordSetAsync(
            user,
            new UserAccountRequestOrigin("https", "attacker.example", "")));

        Assert.Null(messenger.EmailChangeCallbackUrl);
    }

    [Fact]
    // Function summary: Verifies profile email changes remain pending until the Identity change-email token is confirmed.
    public async Task ProfileEmailChange_RemainsPendingUntilConfirmation()
    {
        const string newEmail = "security.changed@rvt.test";
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        var seededUser = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        var client = CreateClient(app);
        await LoginAsync(client);

        using var update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = newEmail,
            Name = "Pending Email Admin",
            MobilePhone = "07123456789",
            CompanyRole = "Operations"
        });
        var pendingProfile = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        var pendingUser = await FindUserByIdAsync(app, seededUser.Id);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(AdminEmail, pendingProfile?.Email);
        Assert.Equal(AdminEmail, pendingUser.Email);
        Assert.Equal(AdminEmail, pendingUser.UserName);
        Assert.True(pendingUser.EmailConfirmed);
        Assert.Equal("Pending Email Admin", pendingUser.Name);
        Assert.Equal(newEmail, messenger.EmailChangeRecipient);
        Assert.NotNull(messenger.EmailChangeCallbackUrl);

        var confirmationUri = new Uri(messenger.EmailChangeCallbackUrl!);
        using var confirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        var confirmedUser = await FindUserByIdAsync(app, seededUser.Id);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(newEmail, confirmedUser.Email);
        Assert.Equal(newEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [Fact]
    // Function summary: Verifies an admin email edit stays pending while non-email edits apply and reset delivery uses the confirmed address.
    public async Task AdminEmailChange_RemainsPendingAndResetUsesConfirmedAddress()
    {
        const string originalEmail = "admin.target@rvt.test";
        const string requestedEmail = "admin.requested@rvt.test";
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTMasterAdmin);
        var target = await factory.SeedUserAsync(originalEmail, Password, RoleNames.RVTAdmin);
        using var app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        var client = CreateClient(app);
        await LoginAsync(client);

        using var update = await client.PutAsJsonAsync($"/api/users/{target.Id}", new UserMutationRequest
        {
            Email = requestedEmail,
            Name = "Pending Admin Target",
            MobilePhone = "07111111111",
            Role = RoleNames.RVTAdmin
        });
        var pendingUser = await FindUserByIdAsync(app, target.Id);
        using var reset = await client.PostAsync($"/api/users/{target.Id}/reset-password-link", null);

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

        var confirmationUri = new Uri(messenger.EmailChangeCallbackUrl!);
        using var confirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        var confirmedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(requestedEmail, confirmedUser.Email);
        Assert.Equal(requestedEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [Fact]
    // Function summary: Verifies username failure restores every Identity email field and leaves the same token safe to retry.
    public async Task EmailChangeConfirmation_WhenUserNameUpdateFails_RollsBackAndTokenCanRetry()
    {
        const string requestedEmail = "reserved.username@rvt.test";
        var messenger = new RecordingAccountMessenger();
        using var factory = new SpaTestApplicationFactory();
        var target = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var blocker = await factory.SeedUserAsync("blocker.email@rvt.test", Password, RoleNames.RVTAdmin);
        await SetUserNameAsync(factory, blocker.Id, requestedEmail);
        using var app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test");
        var client = CreateClient(app);
        await LoginAsync(client);
        using var update = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            Email = requestedEmail,
            Name = "Retry Safe Admin",
            MobilePhone = "07222222222",
            CompanyRole = "Operations"
        });
        var confirmationUri = new Uri(messenger.EmailChangeCallbackUrl!);

        using var firstConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        var rolledBackUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.BadRequest, firstConfirmation.StatusCode);
        Assert.Equal(AdminEmail, rolledBackUser.Email);
        Assert.Equal(AdminEmail, rolledBackUser.UserName);
        Assert.True(rolledBackUser.EmailConfirmed);

        await SetUserNameAsync(app, blocker.Id, "released.username@rvt.test");
        using var retryConfirmation = await client.GetAsync(confirmationUri.PathAndQuery);
        var confirmedUser = await FindUserByIdAsync(app, target.Id);

        Assert.Equal(HttpStatusCode.OK, retryConfirmation.StatusCode);
        Assert.Equal(requestedEmail, confirmedUser.Email);
        Assert.Equal(requestedEmail, confirmedUser.UserName);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [Fact]
    // Function summary: Verifies email-provider failures are indistinguishable from unknown accounts to anonymous callers.
    public async Task ForgotPassword_EmailProviderFailure_MatchesUnknownAccountResponse()
    {
        const string providerDetail = "sendgrid-private-diagnostic";
        var logs = new ListLoggerProvider();
        var messenger = new RecordingAccountMessenger(EmailDeliveryResult.Failure(providerDetail));
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync("unconfirmed@rvt.test", Password, RoleNames.RVTAdmin, emailConfirmed: false);
        using var app = ConfigureAuthDelivery(factory, messenger, "https://portal.example.test", logs);
        var client = CreateClient(app);

        using var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = AdminEmail });
        using var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "missing@rvt.test" });
        using var unconfirmed = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "unconfirmed@rvt.test" });
        var knownBody = await known.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        var unconfirmedBody = await unconfirmed.Content.ReadAsStringAsync();

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

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "127.0.0.1")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "127.0.0.0/8")]
    // Function summary: Verifies explicitly trusted proxy addresses and networks can supply the original HTTPS scheme.
    public async Task ForwardedProto_FromConfiguredProxyOrNetwork_IsHonored(string settingKey, string settingValue)
    {
        using var factory = new SpaTestApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(settingKey, settingValue);
        });

        var context = await app.Server.SendAsync(request => ConfigureForwardedRequest(request, IPAddress.Loopback));

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("203.0.113.25"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    // Function summary: Verifies forwarded headers are ignored when the immediate proxy is not explicitly trusted.
    public async Task ForwardedProto_FromUntrustedProxy_IsIgnored()
    {
        using var factory = new SpaTestApplicationFactory(authRatePermitLimit: 1);
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", "198.51.100.10");
        });

        var context = await app.Server.SendAsync(request => ConfigureForwardedRequest(request, IPAddress.Loopback));

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
    }

    [Fact]
    // Function summary: Verifies forwarded-host trust remains disabled and framework loopback defaults are cleared.
    public void ForwardedHeaders_TrustOnlyConfiguredSources_AndNeverForwardedHost()
    {
        using var factory = new SpaTestApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", "198.51.100.10");
        });
        _ = app.CreateClient();

        var options = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.Parse("198.51.100.10"), options.KnownProxies.Single());
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    // Function summary: Handles the supplied correlation id with unsafe characters is not reflected workflow for this module.
    public async Task SuppliedCorrelationId_WithUnsafeCharacters_IsNotReflected()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);
        const string malicious = "forged value <script> with spaces";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.TryAddWithoutValidation(ApiDiagnostics.CorrelationIdHeader, malicious);

        using var response = await client.SendAsync(request);
        var echoed = response.Headers.TryGetValues(ApiDiagnostics.CorrelationIdHeader, out var values)
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
        var route = endpoint.RoutePattern.RawText ?? "";
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
        ILoggerProvider? loggerProvider = null)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:SkipPasswordResetEmail"] = "false",
                    ["Spa:PublicBaseUrl"] = publicBaseUrl
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountMessenger>();
                services.AddSingleton(messenger);
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

    // Function summary: Loads one Identity user from the application under test for persistence assertions.
    private static async Task<ApplicationUser> FindUserByIdAsync(WebApplicationFactory<Program> factory, string userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        return await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User {userId} was not found.");
    }

    // Function summary: Sets one test user's username through Identity to create or release deterministic collision state.
    private static async Task SetUserNameAsync(WebApplicationFactory<Program> factory, string userId, string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User {userId} was not found.");
        var result = await userManager.SetUserNameAsync(user, userName);
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
        public string? EmailChangeRecipient { get; private set; }
        public string? EmailChangeCallbackUrl { get; private set; }

        public Task<EmailDeliveryResult> SendPasswordSetAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        {
            EmailChangeRecipient = email;
            EmailChangeCallbackUrl = callbackUrl;
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

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        // Function summary: Handles the new workflow for this module.
        private readonly ConcurrentQueue<string> messages = new();

        // Function summary: Maps list into the shape required by callers.
        public IReadOnlyCollection<string> Messages => messages.ToList();

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

// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-24 pending Documented shared-key report-content APIs as intentional ASP.NET anonymous routes.

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RVT.Entities;
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

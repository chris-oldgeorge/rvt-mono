// File summary: Bootstraps the RVT Portal API host, service registration, middleware pipeline, and seed-data startup path.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Blocked public monitor-picture static routes in favor of authorized API streaming.
// - 2026-06-25 pending Added security-headers middleware and per-IP rate limiting for anonymous auth endpoints.
// - 2026-06-25 pending Hardened auth cookie (HttpOnly/Secure outside dev) and fail-fast data-protection key persistence.
// - 2026-06-25 pending Raised password length (configurable, default 12) and shortened reset/confirmation token lifespan.
// - 2026-07-08 pending Shared one scoped relational connection across portal EF contexts for coordinated Unit of Work transactions.
// - 2026-07-22 pending Validated the configured public SPA origin/AllowedHosts and restricted forwarded headers to explicit proxy trust.

using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RvtPortal.Spa;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Auth;
using RvtPortal.Spa.Data;
var builder = WebApplication.CreateBuilder(args);
ConfigureServices(builder);
var app = builder.Build();
await InitializeSeedDataAsync(app);
ConfigurePipeline(app);
await app.RunAsync();

// Function summary: Configures services during application startup.
static void ConfigureServices(WebApplicationBuilder builder)
{
    ConfigurePublicHosting(builder);
    ConfigureForwardedHeaders(builder);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddControllers();
    ConfigureCors(builder);
    ConfigureSwagger(builder.Services);
    var databaseOptions = builder.Services.AddRvtDatabaseProvider(builder.Configuration);
    if (builder.Environment.IsEnvironment("Testing") && string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
    {
        databaseOptions.ConnectionString = "Testing";
    }

    ConfigureDatabases(builder.Services, databaseOptions);
    ConfigureIdentity(builder.Services, builder.Configuration);
    ConfigureDistributedCache(builder);
    ConfigureDataProtection(builder);
    ConfigureApplicationCookie(builder.Services, builder.Environment);
    ConfigureRateLimiting(builder);
    ConfigureHealthChecks(builder.Services);
    builder.Services.AddRvtPortalBusinessServices(builder.Configuration);
}

// Function summary: Registers dependency checks used by the readiness probe while leaving liveness process-only.
static void ConfigureHealthChecks(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("identity database", tags: ["ready"])
        .AddDbContextCheck<RVTDbContext>("domain database", tags: ["ready"])
        .AddDbContextCheck<RVTSearchContext>("search database", tags: ["ready"])
        .AddCheck<PortalSchemaReadinessHealthCheck>("schema", tags: ["ready"]);
}

// Function summary: Binds the public SPA origin and rejects unsafe production host configuration before startup.
static void ConfigurePublicHosting(WebApplicationBuilder builder)
{
    builder.Services.AddOptions<SpaOptions>()
        .Bind(builder.Configuration.GetSection(SpaOptions.SectionName));
    if (!IsProductionStyleEnvironment(builder.Environment))
    {
        return;
    }

    var publicBaseUrl = builder.Configuration[$"{SpaOptions.SectionName}:{nameof(SpaOptions.PublicBaseUrl)}"];
    if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri) ||
        !string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(publicBaseUri.Host) ||
        !string.IsNullOrEmpty(publicBaseUri.UserInfo) ||
        !string.IsNullOrEmpty(publicBaseUri.Query) ||
        !string.IsNullOrEmpty(publicBaseUri.Fragment))
    {
        throw new InvalidOperationException(
            "Spa:PublicBaseUrl must be configured as an absolute HTTPS base URI without credentials, query, or fragment outside Development/Testing.");
    }

    var allowedHosts = (builder.Configuration["AllowedHosts"] ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (allowedHosts.Length == 0 ||
        allowedHosts.Any(host => host.Contains('*', StringComparison.Ordinal)) ||
        !allowedHosts.Contains(publicBaseUri.Host, StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"AllowedHosts must contain the exact Spa:PublicBaseUrl host '{publicBaseUri.Host}' and must not contain wildcards outside Development/Testing.");
    }
}

// Function summary: Enables forwarded scheme/client metadata only for explicitly configured immediate proxies or networks.
static void ConfigureForwardedHeaders(WebApplicationBuilder builder)
{
    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    var parsedProxies = knownProxies.Select(ParseKnownProxy).ToArray();
    var knownNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
    var parsedNetworks = knownNetworks.Select(ParseKnownNetwork).ToArray();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var proxy in parsedProxies)
        {
            options.KnownProxies.Add(proxy);
        }
        foreach (var network in parsedNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    });
}

// Function summary: Parses one explicitly trusted proxy address or fails configuration deterministically.
static IPAddress ParseKnownProxy(string value)
{
    return IPAddress.TryParse(value, out var address)
        ? address
        : throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains invalid IP address '{value}'.");
}

// Function summary: Parses one explicitly trusted proxy network or fails configuration deterministically.
static System.Net.IPNetwork ParseKnownNetwork(string value)
{
    return System.Net.IPNetwork.TryParse(value, out var network)
        ? network
        : throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks contains invalid CIDR network '{value}'.");
}

// Function summary: Configures rate limiting during application startup.
static void ConfigureRateLimiting(WebApplicationBuilder builder)
{
    builder.Services.AddRateLimiter(options =>
    {
        // Resolve the limits per request from the request-scoped configuration so the
        // fully merged configuration is honoured (registration-time configuration does
        // not yet include sources layered on after the host's service configuration).
        options.AddPolicy(RateLimitingPolicies.AuthEndpoints, context =>
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
            var windowSeconds = configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);
            return RateLimitPartition.GetFixedWindowLimiter(
                ResolveRateLimitPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit < 1 ? 1 : permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds < 1 ? 1 : windowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });
        options.OnRejected = OnRateLimiterRejectedAsync;
    });
}

// Function summary: Resolves the per-client partition key used by the rate limiter.
static string ResolveRateLimitPartitionKey(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// Function summary: Writes a problem+json response when a request is rate limited.
static ValueTask OnRateLimiterRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
{
    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
    }

    var problem = ApiProblems.Create(
        context.HttpContext,
        StatusCodes.Status429TooManyRequests,
        "Too many requests.",
        "Too many attempts were made in a short period. Wait a moment and try again.");
    return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
        problem,
        options: null,
        contentType: "application/problem+json",
        cancellationToken: cancellationToken));
}

// Function summary: Configures cors during application startup.
static void ConfigureCors(WebApplicationBuilder builder)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("SpaDevClient", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(IsDevelopmentSpaOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                return;
            }

            var configuredOrigins = builder.Configuration.GetSection("Spa:AllowedOrigins").Get<string[]>();
            if (configuredOrigins is { Length: > 0 })
            {
                policy.WithOrigins(configuredOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });
}

// Function summary: Configures swagger during application startup.
static void ConfigureSwagger(IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "RVTmonitoring SPA API",
            Version = "v1",
            Description = "REST API used by the React/Vite portal shell."
        });
    });
}

// Function summary: Configures databases during application startup.
static void ConfigureDatabases(IServiceCollection services, RvtDatabaseOptions databaseOptions)
{
    // All three contexts deliberately share ONE scoped DbConnection. EfCoreUnitOfWork relies on this to
    // enlist domain, search, and Identity writes in a single transaction (DbContext.Database.UseTransaction
    // only accepts a transaction opened on the same connection); EfCoreUnitOfWork.EnsureSharedConnection
    // asserts it. Two consequences follow and are accepted:
    //   * the contexts cannot be pooled (AddDbContextPool), because the options close over a scoped connection;
    //   * queries on different contexts cannot run concurrently within a request - never Task.WhenAll them.
    services.AddScoped<DbConnection>(_ => databaseOptions.CreateDbConnection());
    services.AddDbContext<ApplicationDbContext>((provider, options) =>
        options.UseRvtDatabaseProvider(
            databaseOptions,
            provider.GetRequiredService<DbConnection>(),
            RvtDatabaseServiceCollectionExtensions.IdentityMigrationsHistoryTable));
    services.AddDbContext<RVTDbContext>((provider, options) =>
        options.UseRvtDatabaseProvider(databaseOptions, provider.GetRequiredService<DbConnection>()));
    services.AddDbContext<RVTSearchContext>((provider, options) =>
        options.UseRvtDatabaseProvider(
            databaseOptions,
            provider.GetRequiredService<DbConnection>(),
            RvtDatabaseServiceCollectionExtensions.SearchMigrationsHistoryTable));
    services.AddDatabaseDeveloperPageExceptionFilter();

    if (databaseOptions.ValidateSchemaOnStartup)
    {
        // Fail fast on model/schema drift rather than serving 500s from the first query that hits it.
        services.AddHostedService<SchemaValidationHostedService>();
    }
}

// Function summary: Configures identity during application startup.
static void ConfigureIdentity(IServiceCollection services, IConfiguration configuration)
{
    // Read the configurable values inside the options callbacks so they resolve against the
    // fully merged configuration at runtime rather than the registration-time snapshot.
    services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        var requiredPasswordLength = configuration.GetValue("Identity:Password:RequiredLength", 12);
        options.Password.RequiredLength = requiredPasswordLength < 8 ? 8 : requiredPasswordLength;
    }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
    services.Configure<DataProtectionTokenProviderOptions>(options =>
    {
        // Reset/confirmation tokens: shorter-lived to limit the link-interception window.
        var tokenLifespanMinutes = configuration.GetValue("Identity:TokenLifespanMinutes", 240);
        options.TokenLifespan = TimeSpan.FromMinutes(tokenLifespanMinutes < 1 ? 1 : tokenLifespanMinutes);
    });
}

// Function summary: Configures distributed cache during application startup.
static void ConfigureDistributedCache(WebApplicationBuilder builder)
{
    var redisConnectionString = builder.Configuration["RvtProduction:RedisConnectionString"];
    if (string.IsNullOrWhiteSpace(redisConnectionString))
    {
        builder.Services.AddDistributedMemoryCache();
        return;
    }

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = builder.Configuration["RvtProduction:RedisInstanceName"] ?? "RvtPortal:";
    });
}

// Function summary: Configures data protection during application startup.
static void ConfigureDataProtection(WebApplicationBuilder builder)
{
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName(builder.Configuration["RvtProduction:DataProtectionApplicationName"] ?? "RvtMonitoring");
    var dataProtectionBlobUri = builder.Configuration["RvtProduction:DataProtectionBlobUri"];
    if (string.IsNullOrWhiteSpace(dataProtectionBlobUri))
    {
        if (IsProductionStyleEnvironment(builder.Environment))
        {
            // Without persisted keys, the data-protection key ring is ephemeral and
            // regenerates on restart/scale-out, silently invalidating every auth cookie
            // and antiforgery token. Fail fast rather than ship that misconfiguration.
            throw new InvalidOperationException(
                "RvtProduction:DataProtectionBlobUri must be configured outside Development so data-protection keys are persisted.");
        }

        return;
    }

    var credential = new DefaultAzureCredential();
    dataProtection.PersistKeysToAzureBlobStorage(new Uri(dataProtectionBlobUri), credential);
    var dataProtectionKeyIdentifier = builder.Configuration["RvtProduction:DataProtectionKeyIdentifier"];
    if (!string.IsNullOrWhiteSpace(dataProtectionKeyIdentifier))
    {
        dataProtection.ProtectKeysWithAzureKeyVault(new Uri(dataProtectionKeyIdentifier), credential);
    }
}

// Function summary: Evaluates whether the host runs in a non-local, production-style environment.
static bool IsProductionStyleEnvironment(IHostEnvironment environment)
{
    return !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
}

// Function summary: Configures application cookie during application startup.
static void ConfigureApplicationCookie(IServiceCollection services, IHostEnvironment environment)
{
    services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;

        // Force Secure outside local development so the session cookie never traverses
        // plain HTTP. Development/Testing keep SameAsRequest so http://localhost flows
        // and the in-memory test host continue to work.
        options.Cookie.SecurePolicy = IsProductionStyleEnvironment(environment)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
        options.Events.OnRedirectToLogin = RedirectApiLoginAsync;
        options.Events.OnRedirectToAccessDenied = RedirectApiAccessDeniedAsync;
    });
}

// Function summary: Evaluates principal for the current decision point.
static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
{
    if (context.Principal?.Identity is not ClaimsIdentity claimIdentity || string.IsNullOrWhiteSpace(claimIdentity.Name))
    {
        return;
    }

    var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
    var user = await signInManager.UserManager.FindByNameAsync(claimIdentity.Name);
    var securityStamp = claimIdentity.Claims.FirstOrDefault(c => c.Type == "AspNet.Identity.SecurityStamp")?.Value;
    if (user == null || user.IsDisabled || securityStamp != await signInManager.UserManager.GetSecurityStampAsync(user))
    {
        context.RejectPrincipal();
        await signInManager.SignOutAsync();
    }
}

// Function summary: Handles the redirect API login workflow for this module.
static Task RedirectApiLoginAsync(RedirectContext<CookieAuthenticationOptions> context)
{
    return RedirectApiOrBrowserAsync(context, StatusCodes.Status401Unauthorized);
}

// Function summary: Handles the redirect API access denied workflow for this module.
static Task RedirectApiAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context)
{
    return RedirectApiOrBrowserAsync(context, StatusCodes.Status403Forbidden);
}

// Function summary: Handles the redirect API or browser workflow for this module.
static Task RedirectApiOrBrowserAsync(RedirectContext<CookieAuthenticationOptions> context, int apiStatusCode)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = apiStatusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

// Function summary: Initializes seed data state required by the application.
static async Task InitializeSeedDataAsync(WebApplication app)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    await SeedDatabase.Initialize(scope.ServiceProvider);
}

// Function summary: Configures pipeline during application startup.
static void ConfigurePipeline(WebApplication app)
{
    app.UseForwardedHeaders();
    ConfigureEnvironmentPipeline(app);
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseWhen(ShouldApplyHttpsRedirection, branch => branch.UseHttpsRedirection());
    app.UseMiddleware<ApiCorrelationMiddleware>();
    app.UseMiddleware<ApiObservabilityMiddleware>();
    app.UseMiddleware<ApiCsrfProtectionMiddleware>();
    app.UseMiddleware<ApiExceptionMiddleware>();
    app.MapWhen(IsPublicMonitorPictureRoute, branch => branch.Run(PublicMonitorPictureNotFoundAsync));
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseCors("SpaDevClient");
    app.UseAuthentication();
    app.UseAuthorization();
    MapEndpoints(app);
}

// Function summary: Configures environment pipeline during application startup.
static void ConfigureEnvironmentPipeline(WebApplication app)
{
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "RVTmonitoring SPA API v1");
        });
        return;
    }

    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Function summary: Maps endpoints into the shape required by callers.
static void MapEndpoints(WebApplication app)
{
    app.MapHealthChecks("/api/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapControllers();
    app.MapGet("/error", ErrorEndpoint).ExcludeFromDescription();
    var retiredMvcUtilityMethods = new[] { HttpMethods.Get, HttpMethods.Post };
    app.MapMethods("/test", retiredMvcUtilityMethods, RetiredMvcUtilityRoute).ExcludeFromDescription();
    app.MapMethods("/test/{**path}", retiredMvcUtilityMethods, RetiredMvcUtilityRouteWithPath).ExcludeFromDescription();
    app.MapMethods("/demo", retiredMvcUtilityMethods, RetiredMvcUtilityRoute).ExcludeFromDescription();
    app.MapMethods("/demo/{**path}", retiredMvcUtilityMethods, RetiredMvcUtilityRouteWithPath).ExcludeFromDescription();
    app.MapMethods("/home/exception", retiredMvcUtilityMethods, RetiredMvcUtilityRoute).ExcludeFromDescription();
    app.MapMethods("/home/reset", retiredMvcUtilityMethods, RetiredMvcUtilityRoute).ExcludeFromDescription();
    app.MapFallback("/api/{**path}", ApiEndpointNotFound).ExcludeFromDescription();
    app.MapFallback(context => SpaFallbackAsync(context, app));
}

// Function summary: Emits a minimal probe response that identifies statuses and check names without dependency details.
static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString(),
            StringComparer.Ordinal)
    };
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(response);
}

// Function summary: Identifies retired public monitor-picture static routes before SPA fallback.
static bool IsPublicMonitorPictureRoute(HttpContext context)
{
    return context.Request.Path.StartsWithSegments("/monitor-pictures");
}

// Function summary: Prevents direct public access to protected monitor pictures.
static Task PublicMonitorPictureNotFoundAsync(HttpContext context)
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}

// Function summary: Handles the error endpoint workflow for this module.
static IResult ErrorEndpoint(HttpContext context)
{
    return ProblemJson(
        context,
        StatusCodes.Status500InternalServerError,
        "An unexpected server error occurred.",
        "The portal could not complete the request. Use the correlation id when reviewing server logs.");
}

// Function summary: Handles the API endpoint not found workflow for this module.
static IResult ApiEndpointNotFound(HttpContext context, string path)
{
    return ProblemJson(
        context,
        StatusCodes.Status404NotFound,
        "API endpoint not found.",
        $"The requested API route '/api/{path}' is not available.");
}

// Function summary: Handles the retired mvc utility route workflow for this module.
static IResult RetiredMvcUtilityRoute(HttpContext context)
{
    return ProblemJson(
        context,
        StatusCodes.Status404NotFound,
        "Legacy MVC utility route retired.",
        "This demo, debug, or reset utility route is not exposed by the SPA host.");
}

// Function summary: Handles the retired mvc utility route with path workflow for this module.
static IResult RetiredMvcUtilityRouteWithPath(HttpContext context, string path)
{
    return ProblemJson(
        context,
        StatusCodes.Status404NotFound,
        "Legacy MVC utility route retired.",
        $"The legacy utility route segment '{path}' is not exposed by the SPA host.");
}

// Function summary: Handles the SPA fallback workflow for this module.
static async Task SpaFallbackAsync(HttpContext context, WebApplication app)
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
        return;
    }

    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync("RVTmonitoring SPA host is running. Build ClientApp to serve the React shell.");
}

// Function summary: Handles the problem json workflow for this module.
static IResult ProblemJson(HttpContext context, int statusCode, string title, string detail)
{
    return Results.Json(
        ApiProblems.Create(context, statusCode, title, detail),
        statusCode: statusCode,
        contentType: "application/problem+json");
}

// Function summary: Evaluates development SPA origin for the current decision point.
static bool IsDevelopmentSpaOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }
    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
    {
        return false;
    }
    if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    return uri.Host.StartsWith("10.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.16.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.17.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.18.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.19.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.2", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.30.", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.StartsWith("172.31.", StringComparison.OrdinalIgnoreCase);
}

// Function summary: Handles the should apply https redirection workflow for this module.
static bool ShouldApplyHttpsRedirection(HttpContext context)
{
    if (!context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        return true;
    }
    return !context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/swagger");
}

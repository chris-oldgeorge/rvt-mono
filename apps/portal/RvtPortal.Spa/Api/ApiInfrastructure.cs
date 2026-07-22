// File summary: Exposes API endpoints used by the React portal for api infrastructure workflows.
// Major updates:
// - 2026-07-09 pending Refined generated middleware and helper comments after controller workflow cleanup.
// - 2026-06-29 pending Used typed response header accessors for security headers.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-25 pending Added SecurityHeadersMiddleware and made CSRF Sec-Fetch-Site authoritative (now blocks same-site forgeries).
// - 2026-06-25 pending Sanitized client-supplied X-Correlation-Id to prevent log-line forging.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace RvtPortal.Spa.Api;

public static class RateLimitingPolicies
{
    // Applied to brute-force-sensitive anonymous auth endpoints (login, password reset).
    public const string AuthEndpoints = "auth-endpoints";
}

public static class ApiDiagnostics
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    private const string CorrelationIdItem = "RvtPortal.CorrelationId";

    // Function summary: Returns the current API correlation id from request state or tracing.
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdItem, out var value) && value is string id && !string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    // Function summary: Stores the sanitized correlation id for downstream middleware and problem responses.
    public static void SetCorrelationId(this HttpContext context, string correlationId)
    {
        context.Items[CorrelationIdItem] = correlationId;
    }

    // Function summary: Sanitizes a client-supplied correlation id, rejecting values that could forge log lines.
    public static string? SanitizeCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 128)
        {
            trimmed = trimmed[..128];
        }

        // Allow only a conservative token charset; anything with control characters
        // (CR/LF, etc.) is dropped so it cannot inject forged lines into logs.
        foreach (var character in trimmed)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':'))
            {
                return null;
            }
        }

        return trimmed;
    }
}

public static class ApiProblems
{
    // Function summary: Creates the standard problem-details payload with the current correlation id.
    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string title,
        string? detail = null,
        string? type = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type
        };
        problem.Extensions["correlationId"] = context.GetCorrelationId();
        return problem;
    }
}

public sealed class ApiCorrelationMiddleware
{
    private readonly RequestDelegate next;

    // Function summary: Initializes correlation middleware with the next request delegate.
    public ApiCorrelationMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    // Function summary: Sanitizes or creates the request correlation id and exposes it on API responses.
    public async Task Invoke(HttpContext context)
    {
        var suppliedCorrelationId = context.Request.Headers.TryGetValue(ApiDiagnostics.CorrelationIdHeader, out var headerValue) && headerValue.Count > 0
            ? headerValue[0]
            : null;
        var correlationId = ApiDiagnostics.SanitizeCorrelationId(suppliedCorrelationId) ?? context.TraceIdentifier;
        context.SetCorrelationId(correlationId);

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(ApiDiagnostics.CorrelationIdHeader))
                {
                    context.Response.Headers[ApiDiagnostics.CorrelationIdHeader] = context.GetCorrelationId();
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ApiExceptionMiddleware> logger;

    // Function summary: Initializes exception middleware with logging and the next request delegate.
    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    // Function summary: Converts unhandled API exceptions into safe problem-details responses.
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
        {
            logger.LogError(exception, "Unhandled API exception. CorrelationId: {CorrelationId}", context.GetCorrelationId());
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = ApiProblems.Create(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected API error occurred.",
                "The request could not be completed. Use the correlation id when reviewing server logs.");

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}

public sealed class SecurityHeadersMiddleware
{
    // Conservative defence-in-depth policy. frame-ancestors blocks clickjacking,
    // object-src blocks legacy plugin/embeds, base-uri blocks base-tag hijacking.
    // A script-src/style-src restriction is intentionally omitted because the Vite
    // SPA bootstrap uses an inline module-preload script; tightening those requires
    // a nonce/hash pass and a browser smoke test before it can be enforced safely.
    private const string ContentSecurityPolicy = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    private readonly RequestDelegate next;

    // Function summary: Initializes security-header middleware with the next request delegate.
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    // Function summary: Adds defensive browser security headers before the response starts.
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Swagger UI relies on inline scripts/styles; skip CSP for it so the
            // development API explorer keeps working while every other response
            // (SPA shell, API, static assets) carries the policy.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

public sealed class ApiCsrfProtectionMiddleware
{
    // Function summary: Lists HTTP methods that can mutate server state and require CSRF checks.
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate next;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly ILogger<ApiCsrfProtectionMiddleware> logger;

    // Function summary: Initializes CSRF middleware with configuration, environment, logging, and the next delegate.
    public ApiCsrfProtectionMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<ApiCsrfProtectionMiddleware> logger)
    {
        this.next = next;
        this.configuration = configuration;
        this.environment = environment;
        this.logger = logger;
    }

    // Function summary: Blocks unsafe cross-origin API mutations before they reach controllers.
    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || !UnsafeMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        var requestOrigin = GetRequestOrigin(context);
        var suppliedOrigin = GetSuppliedOrigin(context);
        if (suppliedOrigin is not null && !IsAllowedOrigin(suppliedOrigin, requestOrigin))
        {
            logger.LogWarning(
                "Blocked cross-site API mutation {Method} {Path}. Origin: {Origin}; CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                suppliedOrigin,
                context.GetCorrelationId());
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Cross-site API request blocked.",
                "Unsafe API requests must originate from the portal origin or a configured SPA origin.");
            return;
        }

        await next(context);
    }

    // Function summary: Reads the strongest available browser origin signal for CSRF validation.
    private static string? GetSuppliedOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin;
        }

        // Sec-Fetch-Site is authoritative when the browser supplies it: only
        // same-origin and user-initiated (none) navigations are trusted for an
        // unsafe method. Anything else (same-site, cross-site) is treated as a
        // blocked sentinel rather than falling through to the fail-open path,
        // which previously let same-site forgeries through when Origin/Referer
        // were absent.
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fetchSite))
        {
            if (string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fetchSite, "none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fetchSite;
        }

        var referer = context.Request.Headers.Referer.FirstOrDefault();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            return refererUri.GetLeftPart(UriPartial.Authority);
        }

        return null;
    }

    // Function summary: Checks whether an origin matches the request origin or configured SPA origins.
    private bool IsAllowedOrigin(string origin, string requestOrigin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        var normalizedOrigin = originUri.GetLeftPart(UriPartial.Authority);
        if (string.Equals(normalizedOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var configuredOrigin in configuration.GetSection("Spa:AllowedOrigins").Get<string[]>() ?? [])
        {
            if (Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var configuredUri) &&
                string.Equals(normalizedOrigin, configuredUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return (environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
            IsDevelopmentOrigin(originUri);
    }

    // Function summary: Builds the absolute origin for the current request.
    private static string GetRequestOrigin(HttpContext context)
    {
        return $"{context.Request.Scheme}://{context.Request.Host}";
    }

    // Function summary: Allows localhost origins in development and test environments.
    private static bool IsDevelopmentOrigin(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Writes a serialized problem-details response from middleware.
    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = ApiProblems.Create(context, statusCode, title, detail);
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonSerializerOptions.Web));
    }
}

public sealed class ApiObservabilityMiddleware
{
    // Function summary: Lists HTTP methods that should be logged as API mutations.
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate next;
    private readonly ILogger<ApiObservabilityMiddleware> logger;

    // Function summary: Initializes observability middleware with logging and the next request delegate.
    public ApiObservabilityMiddleware(RequestDelegate next, ILogger<ApiObservabilityMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    // Function summary: Adds server timing and structured API request logging.
    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Server-Timing"))
            {
                context.Response.Headers["Server-Timing"] =
                    string.Create(CultureInfo.InvariantCulture, $"app;dur={stopwatch.Elapsed.TotalMilliseconds:0.###}");
            }

            return Task.CompletedTask;
        });

        await next(context);
        stopwatch.Stop();

        if (UnsafeMethods.Contains(context.Request.Method) && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "API mutation {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}; User: {UserName}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.GetCorrelationId(),
                context.User.Identity?.Name ?? "anonymous");
            return;
        }

        if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "API request {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.GetCorrelationId());
        }
    }
}

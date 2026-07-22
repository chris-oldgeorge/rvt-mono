using Microsoft.Extensions.Options;

namespace Rvt.Reporting.Service.Api;

/// <summary>
/// Protects internal report-generation endpoints with a shared service-to-service secret.
/// Major updates: 2026-06-24 replaced anonymous Azure Function triggers with internal API authentication.
/// </summary>
public sealed class InternalApiKeyFilter : IEndpointFilter
{
    private const string HeaderName = "X-RVT-Internal-Key";
    private readonly InternalApiOptions _options;
    private readonly IHostEnvironment _environment;

    public InternalApiKeyFilter(IOptions<InternalApiOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey) && _environment.IsDevelopment())
        {
            return next(context);
        }

        if (context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedKey)
            && string.Equals(suppliedKey, _options.InternalApiKey, StringComparison.Ordinal))
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(Results.Unauthorized());
    }
}

public sealed class InternalApiOptions
{
    public string? InternalApiKey { get; set; }
}

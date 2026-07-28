using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ReportingMonitor.Api.Security;

public sealed class InternalApiKeyFilter(ReportingMonitorOptions options, IHostEnvironment environment) : IEndpointFilter
{
    public const string HeaderName = "X-RVT-Internal-Key";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(options.InternalApiKey))
        {
            return environment.IsDevelopment()
                ? next(context)
                : ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        string suppliedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
        byte[] expectedBytes = Encoding.UTF8.GetBytes(options.InternalApiKey);
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);

        if (expectedBytes.Length != suppliedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        return next(context);
    }
}

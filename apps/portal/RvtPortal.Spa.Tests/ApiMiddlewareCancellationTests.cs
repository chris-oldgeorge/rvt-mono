using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Tests;

public sealed class ApiMiddlewareCancellationTests
{
    [Fact]
    public async Task ExceptionResponse_StopsWritingWhenRequestIsAborted()
    {
        DefaultHttpContext context = CreateAbortedContext();
        context.Request.Path = "/api/test";
        ApiExceptionMiddleware middleware = new(
            _ => throw new InvalidOperationException("Test failure."),
            NullLogger<ApiExceptionMiddleware>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => middleware.Invoke(context));
    }

    [Fact]
    public async Task CsrfProblemResponse_StopsWritingWhenRequestIsAborted()
    {
        DefaultHttpContext context = CreateAbortedContext();
        context.Request.Path = "/api/test";
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString("portal.example.test");
        context.Request.Headers.Origin = "https://attacker.example";
        ApiCsrfProtectionMiddleware middleware = new(
            _ => throw new InvalidOperationException("Blocked request reached the next middleware."),
            new ConfigurationBuilder().Build(),
            new TestHostEnvironment(),
            NullLogger<ApiCsrfProtectionMiddleware>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => middleware.Invoke(context));
    }

    private static DefaultHttpContext CreateAbortedContext()
    {
        DefaultHttpContext context = new();
        context.RequestAborted = new CancellationToken(canceled: true);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(ApiMiddlewareCancellationTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

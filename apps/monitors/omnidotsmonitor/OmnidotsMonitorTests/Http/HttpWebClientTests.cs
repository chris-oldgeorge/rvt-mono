using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Http;
using Rvt.Monitor.Common.Diagnostics;

namespace OmnidotsAdapterTests.Http;

[TestClass]
[DoNotParallelize]
public sealed class HttpWebClientTests
{
    private const string RawVendorBodyMarker = "raw-vendor-body-marker";

    [TestMethod]
    public async Task PostAsync_NonSuccess_DoesNotExposeVendorBodyInExceptionOrLogs()
    {
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        RvtLogger.CreateLogger(loggerFactory, nameof(HttpWebClientTests));
        var responseContent = new TrackingHttpContent(RawVendorBodyMarker);
        using var client = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = responseContent
            }));
        var subject = new HttpWebClient("https://vendor.example.test", client);

        var exception = await Assert.ThrowsExactlyAsync<AdapterException>(() => subject.PostAsync(
            "/api/v1/configure_measuring_point?token=vendor-token&measuring_point_id=23423",
            new StringContent("{}")));

        Assert.AreEqual("Omnidots API request failed.", exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsFalse(exception.ToString().Contains(RawVendorBodyMarker, StringComparison.Ordinal));
        Assert.IsFalse(loggerProvider.Messages.Any(message =>
            message.Contains(RawVendorBodyMarker, StringComparison.Ordinal)));
        Assert.IsTrue(loggerProvider.Messages.Any(message =>
            message.Contains("statusCode=400", StringComparison.Ordinal)));
        Assert.IsFalse(responseContent.WasConsumed);
    }

    [TestMethod]
    public async Task PostAsync_Success_ReadsAndReturnsVendorBody()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        RvtLogger.CreateLogger(loggerFactory, nameof(HttpWebClientTests));
        var responseContent = new TrackingHttpContent("successful-vendor-body");
        using var client = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent
            }));
        var subject = new HttpWebClient("https://vendor.example.test", client);

        var response = await subject.PostAsync(
            "/api/v1/configure_measuring_point?token=vendor-token&measuring_point_id=23423",
            new StringContent("{}"));

        Assert.AreEqual("successful-vendor-body", response);
        Assert.IsTrue(responseContent.WasConsumed);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class TrackingHttpContent(string value) : HttpContent
    {
        public bool WasConsumed { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasConsumed = true;
            await stream.WriteAsync(Encoding.UTF8.GetBytes(value));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

using System.Net;
using System.Text;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Http;

namespace Rvt.Monitor.CommonTests.Http;

[TestClass]
public sealed class VendorHttpTransportTests
{
    [TestMethod]
    public async Task SendAsync_DisposesTheRequestContentAfterSending()
    {
        TrackingContent requestContent = new("request");
        using HttpClient client = Client(_ => Response(HttpStatusCode.OK, "ok"));
        VendorHttpTransport transport = new(client);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Post, "/path", requestContent, CancellationToken.None);

        Assert.IsTrue(requestContent.IsDisposed);
        Assert.AreEqual("ok", await response.ReadStringAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_DoesNotDownloadTheBodyUntilRead()
    {
        TrackingContent responseContent = new("secret-vendor-body");
        using HttpClient client = Client(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = responseContent
        });
        VendorHttpTransport transport = new(client);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Get, "/path", null, CancellationToken.None);

        Assert.IsFalse(response.IsOk);
        Assert.IsFalse(responseContent.WasConsumed);
    }

    [TestMethod]
    public async Task SendAsync_RetriesContentlessRequestsPerThePolicy()
    {
        int calls = 0;
        using HttpClient client = Client(_ => ++calls < 3
            ? Response(HttpStatusCode.TooManyRequests, "throttled")
            : Response(HttpStatusCode.OK, "eventually"));
        RecordingPolicy policy = new(maximumAttempts: 5);
        VendorHttpTransport transport = new(client, policy);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Get, "/path", null, CancellationToken.None);

        Assert.AreEqual(3, calls);
        Assert.AreEqual(3, policy.Permits);
        Assert.HasCount(2, policy.Delays);
        Assert.AreEqual("eventually", await response.ReadStringAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_NeverRetriesRequestsThatCarryContent()
    {
        int calls = 0;
        using HttpClient client = Client(_ =>
        {
            calls++;
            return Response(HttpStatusCode.ServiceUnavailable, "down");
        });
        RecordingPolicy policy = new(maximumAttempts: 5);
        VendorHttpTransport transport = new(client, policy);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Post, "/path", new StringContent("{}"), CancellationToken.None);

        Assert.AreEqual(1, calls);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [TestMethod]
    public async Task ReadStringAsync_EnforcesTheConfiguredBound()
    {
        using HttpClient client = Client(_ => Response(HttpStatusCode.OK, new string('x', 64)));
        VendorHttpTransport transport = new(client, maxResponseBytes: 16);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Get, "/path", null, CancellationToken.None);

        AdapterException exception = await Assert.ThrowsExactlyAsync<AdapterException>(
            () => response.ReadStringAsync(CancellationToken.None));
        Assert.Contains("16-byte limit", exception.Message);
    }

    [TestMethod]
    public async Task ReadStringAsync_ReturnsBoundedContentThatFits()
    {
        using HttpClient client = Client(_ => Response(HttpStatusCode.OK, "fits"));
        VendorHttpTransport transport = new(client, maxResponseBytes: 64);

        using VendorHttpResponse response = await transport.SendAsync(
            HttpMethod.Get, "/path", null, CancellationToken.None);

        Assert.AreEqual("fits", await response.ReadStringAsync(CancellationToken.None));
    }

    [TestMethod]
    public void Constructor_RejectsANonPositiveBound()
    {
        using HttpClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _ = new VendorHttpTransport(client, maxResponseBytes: 0));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond))
        {
            BaseAddress = new Uri("https://vendor.example.test")
        };

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8)
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    private sealed class RecordingPolicy(int maximumAttempts) : IVendorRequestPolicy
    {
        public int Permits { get; private set; }

        public List<TimeSpan> Delays { get; } = [];

        public Task WaitForPermitAsync(CancellationToken cancellationToken)
        {
            Permits++;
            return Task.CompletedTask;
        }

        public bool ShouldRetry(HttpStatusCode statusCode, int attempt) =>
            attempt < maximumAttempts && (int)statusCode >= 400;

        public TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber) =>
            TimeSpan.FromMilliseconds(retryNumber);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingContent(string value) : HttpContent
    {
        public bool IsDisposed { get; private set; }

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

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}

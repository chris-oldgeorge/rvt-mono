using System.Net;
using MyAtm.Api.Http;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtmMonitorTests;

[TestClass]
public class HttpWebClientTests
{
    [TestMethod]
    public void GetRetryDelay_RetryAfterBeyondConfiguredMaximum_IsCapped()
    {
        using HttpResponseMessage response = CreateResponse(
            HttpStatusCode.TooManyRequests,
            "ignored",
            TimeSpan.FromMinutes(20));
        MyAtmRequestPolicy policy = new(
            new MyAtmVendorOptions
            {
                BaseUrl = "https://vendor.example/",
                ApiKey = "test-key",
                MaximumRetryDelaySeconds = 30
            });

        TimeSpan delay = policy.GetRetryDelay(response, retryNumber: 1);

        Assert.AreEqual(TimeSpan.FromSeconds(30), delay);
    }

    [TestMethod]
    public async Task GetAsync_OversizedSuccessBody_FailsBeforeReturningContent()
    {
        Queue<HttpResponseMessage> responses = new(
        [
            CreateResponse(HttpStatusCode.OK, "12345", null)
        ]);
        using HttpClient client = new(new QueueHttpMessageHandler(responses));
        HttpWebClient<object> subject = new(
            "https://vendor.example/",
            "test-key",
            client,
            new MyAtmRequestPolicy(),
            maxResponseBytes: 4);

        AdapterException exception = await Assert.ThrowsAsync<AdapterException>(() => subject.GetAsync("devices", TestContext.CancellationToken));

        Assert.DoesNotContain("12345", exception.ToString());
    }

    [TestMethod]
    public async Task GetAsync_PermanentFailure_DoesNotReadOrExposeVendorBody()
    {
        const string sentinel = "sensitive-vendor-body-sentinel";
        TrackingStringContent content = new(sentinel);
        Queue<HttpResponseMessage> responses = new(
        [
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }
        ]);
        using HttpClient client = new(new QueueHttpMessageHandler(responses));
        HttpWebClient<object> subject = new(
            "https://vendor.example/",
            "test-key",
            client,
            new MyAtmRequestPolicy());

        AdapterException exception = await Assert.ThrowsAsync<AdapterException>(() => subject.GetAsync("devices", TestContext.CancellationToken));

        Assert.IsFalse(content.WasSerialized);
        Assert.DoesNotContain(sentinel, exception.ToString());
    }

    [TestMethod]
    public async Task GetAsync_RetriesTooManyRequests_UsingRetryAfter()
    {
        Queue<HttpResponseMessage> responses = new(
        [
            CreateResponse(HttpStatusCode.TooManyRequests, "slow down", TimeSpan.FromSeconds(3)),
            CreateResponse(HttpStatusCode.OK, "[]", null)
        ]);
        List<TimeSpan> delays = [];
        QueueHttpMessageHandler handler = new(responses);
        MyAtmRequestPolicy policy = new(
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        using HttpClient client = new(handler);
        HttpWebClient<object> subject = new("https://vendor.example/", "test-key", client, policy);

        string result = await subject.GetAsync("devices", TestContext.CancellationToken);

        Assert.AreEqual("[]", result);
        Assert.AreEqual(2, handler.RequestCount);
        Assert.IsTrue(delays.Any(delay => delay == TimeSpan.FromSeconds(3)));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.BadGateway)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.GatewayTimeout)]
    public async Task GetAsync_RetriesTransientVendorFailures(HttpStatusCode transientStatus)
    {
        Queue<HttpResponseMessage> responses = new(
        [
            CreateResponse(transientStatus, "temporary", null),
            CreateResponse(HttpStatusCode.OK, "[]", null)
        ]);
        List<TimeSpan> delays = [];
        MyAtmRequestPolicy policy = new(delayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        using HttpClient client = new(new QueueHttpMessageHandler(responses));
        HttpWebClient<object> subject = new("https://vendor.example/", "test-key", client, policy);

        string result = await subject.GetAsync("devices", TestContext.CancellationToken);

        Assert.AreEqual("[]", result);
        Assert.IsGreaterThanOrEqualTo(1, delays.Count, "A transient response must schedule a retry delay.");
    }

    [TestMethod]
    public async Task GetAsync_PropagatesCallerCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        MyAtmRequestPolicy policy = new();
        using HttpClient client = new(new QueueHttpMessageHandler(new Queue<HttpResponseMessage>()));
        HttpWebClient<object> subject = new("https://vendor.example/", "test-key", client, policy);

        await Assert.ThrowsAsync<OperationCanceledException>(() => subject.GetAsync("devices", cancellation.Token));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, TimeSpan? retryAfter)
    {
        HttpResponseMessage response = new(statusCode)
        {
            Content = new StringContent(content)
        };
        if (retryAfter != null)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        }

        return response;
    }

    private sealed class QueueHttpMessageHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = responses;

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class TrackingStringContent(string content) : StringContent(content)
    {
        public bool WasSerialized { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasSerialized = true;
            return base.SerializeToStreamAsync(stream, context);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}

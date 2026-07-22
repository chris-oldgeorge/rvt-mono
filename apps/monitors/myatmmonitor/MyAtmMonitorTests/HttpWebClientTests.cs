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
        using var response = CreateResponse(
            HttpStatusCode.TooManyRequests,
            "ignored",
            TimeSpan.FromMinutes(20));
        var policy = new MyAtmRequestPolicy(
            new MyAtmVendorOptions
            {
                BaseUrl = "https://vendor.example/",
                ApiKey = "test-key",
                MaximumRetryDelaySeconds = 30
            });

        var delay = policy.GetRetryDelay(response, retryNumber: 1);

        Assert.AreEqual(TimeSpan.FromSeconds(30), delay);
    }

    [TestMethod]
    public async Task GetAsync_OversizedSuccessBody_FailsBeforeReturningContent()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateResponse(HttpStatusCode.OK, "12345", null)
        });
        using var client = new HttpClient(new QueueHttpMessageHandler(responses));
        var subject = new HttpWebClient<object>(
            "https://vendor.example/",
            "test-key",
            client,
            new MyAtmRequestPolicy(),
            maxResponseBytes: 4);

        var exception = await Assert.ThrowsAsync<AdapterException>(() => subject.GetAsync("devices"));

        Assert.DoesNotContain("12345", exception.ToString());
    }

    [TestMethod]
    public async Task GetAsync_PermanentFailure_DoesNotReadOrExposeVendorBody()
    {
        const string sentinel = "sensitive-vendor-body-sentinel";
        var content = new TrackingStringContent(sentinel);
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }
        });
        using var client = new HttpClient(new QueueHttpMessageHandler(responses));
        var subject = new HttpWebClient<object>(
            "https://vendor.example/",
            "test-key",
            client,
            new MyAtmRequestPolicy());

        var exception = await Assert.ThrowsAsync<AdapterException>(() => subject.GetAsync("devices"));

        Assert.IsFalse(content.WasSerialized);
        Assert.DoesNotContain(sentinel, exception.ToString());
    }

    [TestMethod]
    public async Task GetAsync_RetriesTooManyRequests_UsingRetryAfter()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateResponse(HttpStatusCode.TooManyRequests, "slow down", TimeSpan.FromSeconds(3)),
            CreateResponse(HttpStatusCode.OK, "[]", null)
        });
        var delays = new List<TimeSpan>();
        var handler = new QueueHttpMessageHandler(responses);
        var policy = new MyAtmRequestPolicy(
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        using var client = new HttpClient(handler);
        var subject = new HttpWebClient<object>("https://vendor.example/", "test-key", client, policy);

        var result = await subject.GetAsync("devices");

        Assert.AreEqual("[]", result);
        Assert.AreEqual(2, handler.RequestCount);
        Assert.IsTrue(delays.Any(delay => delay == TimeSpan.FromSeconds(3)));
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.BadGateway)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.GatewayTimeout)]
    public async Task GetAsync_RetriesTransientVendorFailures(HttpStatusCode transientStatus)
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateResponse(transientStatus, "temporary", null),
            CreateResponse(HttpStatusCode.OK, "[]", null)
        });
        var delays = new List<TimeSpan>();
        var policy = new MyAtmRequestPolicy(delayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        using var client = new HttpClient(new QueueHttpMessageHandler(responses));
        var subject = new HttpWebClient<object>("https://vendor.example/", "test-key", client, policy);

        var result = await subject.GetAsync("devices");

        Assert.AreEqual("[]", result);
        Assert.IsTrue(delays.Count >= 1, "A transient response must schedule a retry delay.");
    }

    [TestMethod]
    public async Task GetAsync_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var policy = new MyAtmRequestPolicy();
        using var client = new HttpClient(new QueueHttpMessageHandler(new Queue<HttpResponseMessage>()));
        var subject = new HttpWebClient<object>("https://vendor.example/", "test-key", client, policy);

        await Assert.ThrowsAsync<OperationCanceledException>(() => subject.GetAsync("devices", cancellation.Token));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, TimeSpan? retryAfter)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
        if (retryAfter != null)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        }

        return response;
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public QueueHttpMessageHandler(Queue<HttpResponseMessage> responses) => this.responses = responses;

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class TrackingStringContent : StringContent
    {
        public TrackingStringContent(string content)
            : base(content)
        {
        }

        public bool WasSerialized { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasSerialized = true;
            return base.SerializeToStreamAsync(stream, context);
        }
    }
}

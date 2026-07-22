using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Http;

namespace SvantekMonitorTests;

[TestClass]
public sealed class HttpWebClientCancellationTests
{
    [TestInitialize]
    public void InitializeLogger()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(HttpWebClientCancellationTests));
    }

    [TestMethod]
    public async Task PostAsync_DisposesRequestAndResponse()
    {
        using var cancellation = new CancellationTokenSource();
        var requestContent = new TrackingContent("request");
        var responseContent = new TrackingContent("response");
        using var client = new HttpClient(new ResponseHandler(responseContent))
        {
            BaseAddress = new Uri("https://vendor.example/")
        };
        var subject = new HttpWebClient<object>("https://vendor.example/", client);

        var result = await subject.PostAsync("stations-get-list.php", requestContent, cancellation.Token);

        Assert.AreEqual("response", result);
        Assert.IsTrue(requestContent.IsDisposed);
        Assert.IsTrue(responseContent.IsDisposed);
    }

    [TestMethod]
    public async Task GetByteArrayAsync_DisposesRequestAndResponse()
    {
        using var cancellation = new CancellationTokenSource();
        var requestContent = new TrackingMultipartFormDataContent();
        var responseContent = new TrackingContent([82, 73, 70, 70]);
        using var client = new HttpClient(new ResponseHandler(responseContent))
        {
            BaseAddress = new Uri("https://vendor.example/")
        };
        var subject = new HttpWebClient<object>("https://vendor.example/", client);

        var result = await subject.GetByteArrayAsync(
            "projects-get-data.php",
            requestContent,
            cancellation.Token);

        CollectionAssert.AreEqual(new byte[] { 82, 73, 70, 70 }, result);
        Assert.IsTrue(requestContent.IsDisposed);
        Assert.IsTrue(responseContent.IsDisposed);
    }

    [TestMethod]
    public async Task GetAsync_CallerCancellationStopsResponseRead_AndDisposesResponse()
    {
        using var cancellation = new CancellationTokenSource();
        var responseContent = new BlockingContent();
        using var client = new HttpClient(new ResponseHandler(responseContent))
        {
            BaseAddress = new Uri("https://vendor.example/")
        };
        var subject = new HttpWebClient<object>("https://vendor.example/", client);

        var operation = subject.GetAsync("projects-get-data.php", cancellation.Token);
        await responseContent.ReadStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsTrue(responseContent.IsDisposed);
    }

    [TestMethod]
    public async Task GetAsync_CallerCancellationStopsSend_AndDisposesRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var requestDisposalMarker = new TrackingContent("unused");
        var handler = new CancellationHandler(requestDisposalMarker);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://vendor.example/")
        };
        var subject = new HttpWebClient<object>("https://vendor.example/", client);

        var operation = subject.GetAsync("projects-get-data.php", cancellation.Token);
        await handler.RequestStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        Assert.IsTrue(requestDisposalMarker.IsDisposed);
    }

    private sealed class ResponseHandler(HttpContent responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent
            });
        }
    }

    private sealed class CancellationHandler(TrackingContent requestDisposalMarker) : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Content = requestDisposalMarker;
            RequestStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait unexpectedly completed.");
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        private readonly byte[] value;

        public TrackingContent(string value)
            : this(Encoding.UTF8.GetBytes(value))
        {
        }

        public TrackingContent(byte[] value)
        {
            this.value = value;
        }

        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(value).AsTask();
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            return stream.WriteAsync(value, cancellationToken).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = value.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource ReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDisposed { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(1));
            await stream.WriteAsync("late"u8.ToArray());
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 4;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingMultipartFormDataContent : MultipartFormDataContent
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}

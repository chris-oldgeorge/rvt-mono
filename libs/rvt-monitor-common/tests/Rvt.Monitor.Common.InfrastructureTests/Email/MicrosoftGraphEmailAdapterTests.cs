using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;

namespace Rvt.Monitor.Common.InfrastructureTests.Email;

[TestClass]
public sealed class MicrosoftGraphEmailAdapterTests
{
    [TestMethod]
    public async Task SendAsync_PostsAuthenticatedSmallMessageWithAttachment()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var tokens = new RecordingTokenProvider("token-value");
        var adapter = new MicrosoftGraphEmailAdapter(httpClient, tokens, Options());
        var attachment = new EmailAttachment("report.pdf", "application/pdf", new byte[] { 1, 2, 3 });

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        var request = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/sendMail",
            request.Uri.ToString());
        Assert.AreEqual("Bearer token-value", request.Authorization);
        using var json = JsonDocument.Parse(request.Body!);
        var root = json.RootElement;
        Assert.IsTrue(root.GetProperty("saveToSentItems").GetBoolean());
        var message = root.GetProperty("message");
        Assert.AreEqual("subject", message.GetProperty("subject").GetString());
        Assert.AreEqual("HTML", message.GetProperty("body").GetProperty("contentType").GetString());
        Assert.AreEqual("<p>html</p>", message.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual(
            "ops@example.test",
            message.GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        var file = message.GetProperty("attachments")[0];
        Assert.AreEqual("#microsoft.graph.fileAttachment", file.GetProperty("@odata.type").GetString());
        Assert.AreEqual("report.pdf", file.GetProperty("name").GetString());
        Assert.AreEqual(Convert.ToBase64String(new byte[] { 1, 2, 3 }), file.GetProperty("contentBytes").GetString());
        Assert.AreEqual(1, tokens.Calls);
    }

    [TestMethod]
    public async Task SendAsync_NoAttachmentsOmitsAttachmentArray()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(httpClient, new RecordingTokenProvider("token"), Options());

        await adapter.SendAsync(Request());

        using var json = JsonDocument.Parse(handler.Requests.Single().Body!);
        Assert.IsFalse(json.RootElement.GetProperty("message").TryGetProperty("attachments", out _));
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.RequestTimeout, DeliveryFailureKind.Transient)]
    [DataRow((HttpStatusCode)429, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.InternalServerError, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.BadRequest, DeliveryFailureKind.Permanent)]
    public async Task SendAsync_ClassifiesStatusWithoutReadingRawBody(
        HttpStatusCode status,
        DeliveryFailureKind expectedKind)
    {
        using var handler = new RecordingHandler(status, "raw provider secret");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(httpClient, new RecordingTokenProvider("token"), Options());

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(expectedKind, exception.FailureKind);
        Assert.AreEqual(((int)status).ToString(), exception.Code);
        Assert.DoesNotContain("raw provider secret", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_CallerCancellationPropagatesBeforeTokenOrNetwork()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var tokens = new RecordingTokenProvider("token");
        var adapter = new MicrosoftGraphEmailAdapter(httpClient, tokens, Options());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(Request(), source.Token));

        Assert.AreEqual(0, tokens.Calls);
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_TextOnlyUsesTextBodyAndMultipleSmallAttachments()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain only",
            string.Empty,
            [
                new EmailAttachment("first.txt", "text/plain", new byte[] { 1 }),
                new EmailAttachment("second.pdf", "application/pdf", new byte[] { 2, 3 })
            ]));

        using var json = JsonDocument.Parse(handler.Requests.Single().Body!);
        var message = json.RootElement.GetProperty("message");
        Assert.AreEqual("Text", message.GetProperty("body").GetProperty("contentType").GetString());
        Assert.AreEqual("plain only", message.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual(2, message.GetProperty("attachments").GetArrayLength());
        Assert.AreEqual(
            "text/plain",
            message.GetProperty("attachments")[0].GetProperty("contentType").GetString());
    }

    [TestMethod]
    public async Task SendAsync_ThrottleResponseCarriesRetryAfter()
    {
        using var handler = new RecordingHandler(
            (HttpStatusCode)429,
            configureHeaders: headers => headers.RetryAfter = new RetryConditionHeaderValue(
                TimeSpan.FromSeconds(90)));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(TimeSpan.FromSeconds(90), exception.RetryAfter);
    }

    [TestMethod]
    public async Task SendAsync_NetworkFailureIsTransientAndSafe()
    {
        using var httpClient = new HttpClient(new ThrowingHandler(
            new HttpRequestException("raw network secret")))
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.DoesNotContain("raw network secret", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_TokenFailureIsPermanentAndSafe()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new ThrowingTokenProvider(new InvalidOperationException("raw credential secret")),
            Options());

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.DoesNotContain("raw credential secret", exception.ToString());
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_MissingConfigurationFailsBeforeTokenOrNetwork()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var tokens = new RecordingTokenProvider("token");
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            tokens,
            new CommunicationsOptions { EmailProvider = EmailProvider.MicrosoftGraph });

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(DeliveryFailureKind.Configuration, exception.FailureKind);
        Assert.AreEqual(0, tokens.Calls);
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_DisposesProviderResponse()
    {
        var content = new TrackingContent();
        using var handler = new RecordingHandler(HttpStatusCode.Accepted, content: content);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(Request());

        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_AttachmentBelowThreeMiBUsesSingleSendMailRequest()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        var attachment = new EmailAttachment(
            "small.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit - 1]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        Assert.HasCount(1, handler.Requests);
        Assert.EndsWith("/sendMail", handler.Requests.Single().Uri.AbsolutePath);
    }

    [TestMethod]
    public async Task SendAsync_ExactlyThreeMiBUsesDraftUploadAndSendWithoutUploadAuthorization()
    {
        using var handler = new LargeFlowHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        var attachment = new EmailAttachment(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        CollectionAssert.AreEqual(
            new[]
            {
                "/v1.0/users/sender%40example.test/messages",
                "/v1.0/users/sender%40example.test/messages/draft-id/attachments/createUploadSession",
                "/upload/session-secret",
                "/v1.0/users/sender%40example.test/messages/draft-id/send"
            },
            handler.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        var upload = handler.Requests.Single(request => request.Method == HttpMethod.Put);
        Assert.IsNull(upload.Authorization);
        Assert.AreEqual("bytes 0-3145727/3145728", upload.ContentRange);
        Assert.AreEqual(3L * 1024 * 1024, upload.ContentLength);
    }

    [TestMethod]
    public async Task SendAsync_MixedSmallAndLargeAttachmentsUsesBothAttachmentPaths()
    {
        using var handler = new LargeFlowHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain",
            "<p>html</p>",
            [
                new EmailAttachment("small.txt", "text/plain", new byte[] { 1, 2 }),
                new EmailAttachment(
                    "large.bin",
                    "application/octet-stream",
                    new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit])
            ]));

        Assert.IsTrue(handler.Requests.Any(request =>
            request.Method == HttpMethod.Post &&
            request.Uri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal)));
        Assert.IsTrue(handler.Requests.Any(request =>
            request.Uri.AbsolutePath.EndsWith("/createUploadSession", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SendAsync_SevenMiBAttachmentUsesOrderedBoundedInclusiveChunks()
    {
        using var handler = new LargeFlowHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        var attachment = new EmailAttachment(
            "seven.bin",
            "application/octet-stream",
            new byte[7 * 1024 * 1024]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        var chunks = handler.Requests.Where(request => request.Method == HttpMethod.Put).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "bytes 0-3145727/7340032",
                "bytes 3145728-6291455/7340032",
                "bytes 6291456-7340031/7340032"
            },
            chunks.Select(chunk => chunk.ContentRange).ToArray());
        Assert.IsTrue(chunks.All(chunk => chunk.ContentLength <= 3L * 1024 * 1024));
        Assert.IsTrue(chunks.All(chunk => chunk.Authorization is null));
    }

    [TestMethod]
    public async Task SendAsync_InvalidUploadUrlIsPermanentAndNeverExposed()
    {
        const string invalidUploadUrl = "http://upload.example/session-secret";
        using var handler = new LargeFlowHandler(invalidUploadUrl);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        var attachment = new EmailAttachment(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        var exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(new EmailDeliveryRequest(
                "ops@example.test", "subject", "plain", "<p>html</p>", [attachment])));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.DoesNotContain(invalidUploadUrl, exception.ToString());
        Assert.IsFalse(handler.Requests.Any(request => request.Method == HttpMethod.Put));
    }

    [TestMethod]
    public void AttachmentSizeBoundaries_AllowExactlyOneHundredFiftyMiB()
    {
        Assert.IsTrue(MicrosoftGraphEmailAdapter.IsAttachmentSizeSupported(150L * 1024 * 1024));
        Assert.IsFalse(MicrosoftGraphEmailAdapter.IsAttachmentSizeSupported((150L * 1024 * 1024) + 1));
    }

    [TestMethod]
    public async Task SendAsync_SevenMiBAttachmentUsesOrderedUnauthenticatedThreeMiBChunks()
    {
        const int total = 7 * 1024 * 1024;
        using var handler = new SequenceHandler(
            (HttpStatusCode.Created, "{\"id\":\"draft-id\"}"),
            (HttpStatusCode.OK, "{\"uploadUrl\":\"https://upload.example/session-token\"}"),
            (HttpStatusCode.Accepted, string.Empty),
            (HttpStatusCode.Accepted, string.Empty),
            (HttpStatusCode.Created, string.Empty),
            (HttpStatusCode.Accepted, string.Empty));
        using var httpClient = new HttpClient(handler);
        var tokens = new RecordingTokenProvider("token");
        var adapter = new MicrosoftGraphEmailAdapter(httpClient, tokens, Options());
        var attachment = new EmailAttachment(
            "large.pdf",
            "application/pdf",
            new byte[total]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/messages",
            handler.Requests[0].Uri.ToString());
        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/messages/draft-id/attachments/createUploadSession",
            handler.Requests[1].Uri.ToString());
        var uploads = handler.Requests.Where(request => request.Method == HttpMethod.Put).ToArray();
        Assert.HasCount(3, uploads);
        Assert.AreEqual($"bytes 0-3145727/{total}", uploads[0].ContentRange);
        Assert.AreEqual($"bytes 3145728-6291455/{total}", uploads[1].ContentRange);
        Assert.AreEqual($"bytes 6291456-7340031/{total}", uploads[2].ContentRange);
        Assert.IsTrue(uploads.All(request => request.Authorization is null));
        Assert.IsTrue(uploads.All(request => request.ContentLength <= 3 * 1024 * 1024));
        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/messages/draft-id/send",
            handler.Requests[^1].Uri.ToString());
        Assert.AreEqual(3, tokens.Calls);
    }

    [TestMethod]
    public async Task SendAsync_ExactlyThreeMiBUsesDraftUploadFlow()
    {
        const int total = 3 * 1024 * 1024;
        using var handler = new SequenceHandler(
            (HttpStatusCode.Created, "{\"id\":\"draft-id\"}"),
            (HttpStatusCode.OK, "{\"uploadUrl\":\"https://upload.example/session-token\"}"),
            (HttpStatusCode.Created, string.Empty),
            (HttpStatusCode.Accepted, string.Empty));
        using var httpClient = new HttpClient(handler);
        var adapter = new MicrosoftGraphEmailAdapter(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain",
            "<p>html</p>",
            [new EmailAttachment("boundary.pdf", "application/pdf", new byte[total])]));

        Assert.IsTrue(handler.Requests.Any(request => request.Method == HttpMethod.Put));
        Assert.IsFalse(handler.Requests.Any(request => request.Uri.AbsolutePath.EndsWith("/sendMail")));
    }

    private static EmailDeliveryRequest Request() =>
        new("ops@example.test", "subject", "plain", "<p>html</p>", []);

    private static CommunicationsOptions Options() => new()
    {
        EmailProvider = EmailProvider.MicrosoftGraph,
        EmailEnabled = true,
        MicrosoftTenantId = "tenant",
        MicrosoftClientId = "client",
        MicrosoftClientSecret = "secret",
        MicrosoftSenderAddress = "sender@example.test"
    };

    private sealed class RecordingTokenProvider(string token) : IMicrosoftGraphAccessTokenProvider
    {
        internal int Calls { get; private set; }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(token);
        }
    }

    private sealed class ThrowingTokenProvider(Exception exception) : IMicrosoftGraphAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromException<string>(exception);
    }

    private sealed class RecordingHandler(
        HttpStatusCode status,
        string responseBody = "",
        Action<HttpResponseHeaders>? configureHeaders = null,
        HttpContent? content = null) : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            var response = new HttpResponseMessage(status)
            {
                Content = content ?? new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
            configureHeaders?.Invoke(response.Headers);
            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class TrackingContent : ByteArrayContent
    {
        internal TrackingContent()
            : base([])
        {
        }

        internal bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class LargeFlowHandler(
        string uploadUrl = "https://upload.example/upload/session-secret") : HttpMessageHandler
    {
        internal List<FlowRecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var contentLength = request.Content is null
                ? null
                : (long?)(await request.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength;
            Requests.Add(new FlowRecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentRange?.ToString(),
                request.Content?.Headers.ContentLength ?? contentLength));

            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.Created, """{"id":"draft-id"}""");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, $"{{\"uploadUrl\":\"{uploadUrl}\"}}");
            }

            if (request.Method == HttpMethod.Put)
            {
                var range = request.Content!.Headers.ContentRange!;
                var isFinal = range.To!.Value + 1 == range.Length!.Value;
                return Json(isFinal ? HttpStatusCode.Created : HttpStatusCode.Accepted, "{}");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.Created, "{}");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/send", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.Accepted, "{}");
            }

            throw new InvalidOperationException("Unexpected Graph test request.");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceHandler(params (HttpStatusCode Status, string Body)[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> pending = new(responses);

        internal List<LargeRecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var bytes = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new LargeRecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentRange?.ToString(),
                bytes.LongLength));
            var response = pending.Dequeue();
            return new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record LargeRecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? ContentRange,
        long ContentLength);

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? Body);

    private sealed record FlowRecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? ContentRange,
        long? ContentLength);
}

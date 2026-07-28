using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;

namespace Rvt.Communication.MicrosoftGraphMailTests;

[TestClass]
public sealed class MicrosoftGraphEmailAdapterTests
{
    private static readonly string[] ExpectedUploadRanges =
    [
        "bytes 0-3145727/7340032",
        "bytes 3145728-6291455/7340032",
        "bytes 6291456-7340031/7340032"
    ];

    [TestMethod]
    public async Task SendAsync_PostsAuthenticatedSmallMessageWithAttachment()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        RecordingTokenProvider tokens = new("token-value");
        MicrosoftGraphEmailAdapter adapter = new(httpClient, tokens, Options());
        EmailAttachment attachment = new("report.pdf", "application/pdf", [1, 2, 3]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken);

        RecordedRequest request = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/sendMail",
            request.Uri.ToString());
        Assert.AreEqual("Bearer token-value", request.Authorization);
        using JsonDocument json = JsonDocument.Parse(request.Body!);
        JsonElement root = json.RootElement;
        Assert.IsTrue(root.GetProperty("saveToSentItems").GetBoolean());
        JsonElement message = root.GetProperty("message");
        Assert.AreEqual("subject", message.GetProperty("subject").GetString());
        Assert.AreEqual("HTML", message.GetProperty("body").GetProperty("contentType").GetString());
        Assert.AreEqual("<p>html</p>", message.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual(
            "ops@example.test",
            message.GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        JsonElement file = message.GetProperty("attachments")[0];
        Assert.AreEqual("#microsoft.graph.fileAttachment", file.GetProperty("@odata.type").GetString());
        Assert.AreEqual("report.pdf", file.GetProperty("name").GetString());
        Assert.AreEqual(Convert.ToBase64String(new byte[] { 1, 2, 3 }), file.GetProperty("contentBytes").GetString());
        Assert.AreEqual(1, tokens.Calls);
    }

    [TestMethod]
    public async Task SendAsync_NoAttachmentsOmitsAttachmentArray()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(httpClient, new RecordingTokenProvider("token"), Options());

        await adapter.SendAsync(Request(), TestContext.CancellationToken);

        using JsonDocument json = JsonDocument.Parse(handler.Requests.Single().Body!);
        Assert.IsFalse(json.RootElement.GetProperty("message").TryGetProperty("attachments", out _));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.RequestTimeout, DeliveryFailureKind.Transient)]
    [DataRow((HttpStatusCode)429, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.InternalServerError, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.BadRequest, DeliveryFailureKind.Permanent)]
    public async Task SendAsync_ClassifiesStatusWithoutReadingRawBody(
        HttpStatusCode status,
        DeliveryFailureKind expectedKind)
    {
        using RecordingHandler handler = new(status, "raw provider secret");
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(httpClient, new RecordingTokenProvider("token"), Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(expectedKind, exception.FailureKind);
        Assert.AreEqual(((int)status).ToString(), exception.Code);
        Assert.DoesNotContain("raw provider secret", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_CallerCancellationPropagatesBeforeTokenOrNetwork()
    {
        using CancellationTokenSource source = new();
        source.Cancel();
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        RecordingTokenProvider tokens = new("token");
        MicrosoftGraphEmailAdapter adapter = new(httpClient, tokens, Options());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(Request(), source.Token));

        Assert.AreEqual(0, tokens.Calls);
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_TextOnlyUsesTextBodyAndMultipleSmallAttachments()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain only",
            string.Empty,
            [
                new EmailAttachment("first.txt", "text/plain", [1]),
                new EmailAttachment("second.pdf", "application/pdf", [2, 3])
            ]), TestContext.CancellationToken);

        using JsonDocument json = JsonDocument.Parse(handler.Requests.Single().Body!);
        JsonElement message = json.RootElement.GetProperty("message");
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
        using RecordingHandler handler = new(
            (HttpStatusCode)429,
            configureHeaders: headers => headers.RetryAfter = new RetryConditionHeaderValue(
                TimeSpan.FromSeconds(90)));
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(TimeSpan.FromSeconds(90), exception.RetryAfter);
    }

    [TestMethod]
    public async Task SendAsync_NetworkFailureIsTransientAndSafe()
    {
        using HttpClient httpClient = new(new ThrowingHandler(
            new HttpRequestException("raw network secret")))
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.DoesNotContain("raw network secret", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_HttpTimeoutIsTransientAndSafe()
    {
        using HttpClient httpClient = new(new ThrowingHandler(
            new OperationCanceledException("raw timeout secret")))
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("Timeout", exception.Code);
        Assert.DoesNotContain("raw timeout secret", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_TokenFailureIsPermanentAndSafe()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new ThrowingTokenProvider(new InvalidOperationException("raw credential secret")),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.DoesNotContain("raw credential secret", exception.ToString());
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_MissingConfigurationFailsBeforeTokenOrNetwork()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        RecordingTokenProvider tokens = new("token");
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            tokens,
            new MicrosoftGraphMailOptions());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request(), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Configuration, exception.FailureKind);
        Assert.AreEqual(0, tokens.Calls);
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task SendAsync_DisposesProviderResponse()
    {
        TrackingContent content = new();
        using RecordingHandler handler = new(HttpStatusCode.Accepted, content: content);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(Request(), TestContext.CancellationToken);

        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_AttachmentBelowThreeMiBUsesSingleSendMailRequest()
    {
        using RecordingHandler handler = new(HttpStatusCode.Accepted);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "small.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit - 1]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken);

        Assert.HasCount(1, handler.Requests);
        Assert.EndsWith("/sendMail", handler.Requests.Single().Uri.AbsolutePath);
    }

    [TestMethod]
    public async Task SendAsync_ExactlyThreeMiBUsesDraftUploadAndSendWithoutUploadAuthorization()
    {
        using LargeFlowHandler handler = new();
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            expected,
            handler.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        FlowRecordedRequest upload = handler.Requests.Single(request => request.Method == HttpMethod.Put);
        Assert.IsNull(upload.Authorization);
        Assert.AreEqual("bytes 0-3145727/3145728", upload.ContentRange);
        Assert.AreEqual(3L * 1024 * 1024, upload.ContentLength);
    }

    [TestMethod]
    public async Task SendAsync_UploadChunkTimeoutIsTransientAndSafe()
    {
        const string providerMessage = "raw upload timeout secret";
        using CancellationTokenSource cancellation = new();
        using UploadChunkCancellationHandler handler = new(
            _ => new OperationCanceledException(providerMessage));
        using HttpClient httpClient = new(handler);
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(
                new EmailDeliveryRequest(
                    "ops@example.test",
                    "subject",
                    "plain",
                    "<p>html</p>",
                    [attachment]),
                cancellation.Token));

        Assert.AreEqual("MicrosoftGraph", exception.Provider);
        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("Timeout", exception.Code);
        Assert.DoesNotContain(providerMessage, exception.ToString());
        Assert.IsFalse(cancellation.IsCancellationRequested);
        Assert.AreEqual(1, handler.UploadChunkCalls);
    }

    [TestMethod]
    public async Task SendAsync_UploadChunkCallerCancellationPropagates()
    {
        using CancellationTokenSource cancellation = new();
        using UploadChunkCancellationHandler handler = new(token =>
        {
            cancellation.Cancel();
            return new OperationCanceledException(token);
        });
        using HttpClient httpClient = new(handler);
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            adapter.SendAsync(
                new EmailDeliveryRequest(
                    "ops@example.test",
                    "subject",
                    "plain",
                    "<p>html</p>",
                    [attachment]),
                cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.AreEqual(1, handler.UploadChunkCalls);
    }

    [TestMethod]
    public async Task SendAsync_MixedSmallAndLargeAttachmentsUsesBothAttachmentPaths()
    {
        using LargeFlowHandler handler = new();
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain",
            "<p>html</p>",
            [
                new EmailAttachment("small.txt", "text/plain", [1, 2]),
                new EmailAttachment(
                    "large.bin",
                    "application/octet-stream",
                    new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit])
            ]), TestContext.CancellationToken);

        Assert.IsTrue(handler.Requests.Any(request =>
            request.Method == HttpMethod.Post &&
            request.Uri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal)));
        Assert.IsTrue(handler.Requests.Any(request =>
            request.Uri.AbsolutePath.EndsWith("/createUploadSession", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SendAsync_SevenMiBAttachmentUsesOrderedBoundedInclusiveChunks()
    {
        using LargeFlowHandler handler = new();
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "seven.bin",
            "application/octet-stream",
            new byte[7 * 1024 * 1024]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken);

        FlowRecordedRequest[] chunks = [.. handler.Requests.Where(request => request.Method == HttpMethod.Put)];
        CollectionAssert.AreEqual(
            ExpectedUploadRanges,
            chunks.Select(chunk => chunk.ContentRange).ToArray());
        Assert.IsTrue(chunks.All(chunk => chunk.ContentLength <= 3L * 1024 * 1024));
        Assert.IsTrue(chunks.All(chunk => chunk.Authorization is null));
    }

    [TestMethod]
    public async Task SendAsync_InvalidUploadUrlIsPermanentAndNeverExposed()
    {
        const string invalidUploadUrl = "http://upload.example/session-secret";
        using LargeFlowHandler handler = new(invalidUploadUrl);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());
        EmailAttachment attachment = new(
            "large.bin",
            "application/octet-stream",
            new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit]);

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(new EmailDeliveryRequest(
                "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.DoesNotContain(invalidUploadUrl, exception.ToString());
        Assert.IsFalse(handler.Requests.Any(request => request.Method == HttpMethod.Put));
    }

    [TestMethod]
    public async Task SendAsync_MalformedDraftResponseIsSafeTypedFailure()
    {
        const string malformedResponse = "{\"id\":\"raw-draft-secret\"";
        using SequenceHandler handler = new((HttpStatusCode.Created, malformedResponse));
        using HttpClient httpClient = new(handler);
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(new EmailDeliveryRequest(
                "ops@example.test",
                "subject",
                "plain",
                "<p>html</p>",
                [new EmailAttachment(
                    "large.bin",
                    "application/octet-stream",
                    new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit])]), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.AreEqual("InvalidDraftResponse", exception.Code);
        Assert.DoesNotContain(malformedResponse, exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_MalformedUploadSessionResponseIsSafeTypedFailure()
    {
        const string malformedResponse = "{\"uploadUrl\":\"raw-session-secret\"";
        using SequenceHandler handler = new(
            (HttpStatusCode.Created, """{"id":"draft-id"}"""),
            (HttpStatusCode.OK, malformedResponse));
        using HttpClient httpClient = new(handler);
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(new EmailDeliveryRequest(
                "ops@example.test",
                "subject",
                "plain",
                "<p>html</p>",
                [new EmailAttachment(
                    "large.bin",
                    "application/octet-stream",
                    new byte[MicrosoftGraphEmailAdapter.SmallAttachmentLimit])]), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.AreEqual("InvalidUploadSession", exception.Code);
        Assert.DoesNotContain(malformedResponse, exception.ToString());
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
        using SequenceHandler handler = new(
            (HttpStatusCode.Created, "{\"id\":\"draft-id\"}"),
            (HttpStatusCode.OK, "{\"uploadUrl\":\"https://upload.example/session-token\"}"),
            (HttpStatusCode.Accepted, string.Empty),
            (HttpStatusCode.Accepted, string.Empty),
            (HttpStatusCode.Created, string.Empty),
            (HttpStatusCode.Accepted, string.Empty));
        using HttpClient httpClient = new(handler);
        RecordingTokenProvider tokens = new("token");
        MicrosoftGraphEmailAdapter adapter = new(httpClient, tokens, Options());
        EmailAttachment attachment = new(
            "large.pdf",
            "application/pdf",
            new byte[total]);

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]), TestContext.CancellationToken);

        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/messages",
            handler.Requests[0].Uri.ToString());
        Assert.AreEqual(
            "https://graph.microsoft.com/v1.0/users/sender%40example.test/messages/draft-id/attachments/createUploadSession",
            handler.Requests[1].Uri.ToString());
        LargeRecordedRequest[] uploads = [.. handler.Requests.Where(request => request.Method == HttpMethod.Put)];
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
        using SequenceHandler handler = new(
            (HttpStatusCode.Created, "{\"id\":\"draft-id\"}"),
            (HttpStatusCode.OK, "{\"uploadUrl\":\"https://upload.example/session-token\"}"),
            (HttpStatusCode.Created, string.Empty),
            (HttpStatusCode.Accepted, string.Empty));
        using HttpClient httpClient = new(handler);
        MicrosoftGraphEmailAdapter adapter = new(
            httpClient,
            new RecordingTokenProvider("token"),
            Options());

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test",
            "subject",
            "plain",
            "<p>html</p>",
            [new EmailAttachment("boundary.pdf", "application/pdf", new byte[total])]), TestContext.CancellationToken);

        Assert.IsTrue(handler.Requests.Any(request => request.Method == HttpMethod.Put));
        Assert.IsFalse(handler.Requests.Any(request => request.Uri.AbsolutePath.EndsWith("/sendMail")));
    }

    private static EmailDeliveryRequest Request() =>
        new("ops@example.test", "subject", "plain", "<p>html</p>", []);

    private static MicrosoftGraphMailOptions Options() => new()
    {
        Enabled = true,
        TenantId = "tenant",
        ClientId = "client",
        ClientSecret = "secret",
        SenderAddress = "sender@example.test"
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
            HttpResponseMessage response = new(status)
            {
                Content = content ?? new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
            configureHeaders?.Invoke(response.Headers);
            return response;
        }
    }

    private sealed class UploadChunkCancellationHandler(
        Func<CancellationToken, OperationCanceledException> cancellationFactory) : HttpMessageHandler
    {
        internal int UploadChunkCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(HttpStatusCode.Created, """{"id":"draft-id"}"""));
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(
                    HttpStatusCode.OK,
                    """{"uploadUrl":"https://upload.example/session-token"}"""));
            }

            if (request.Method == HttpMethod.Put)
            {
                UploadChunkCalls++;
                return Task.FromException<HttpResponseMessage>(
                    cancellationFactory(cancellationToken));
            }

            throw new InvalidOperationException("Unexpected Graph test request.");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
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
            long? contentLength = request.Content is null
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
                ContentRangeHeaderValue range = request.Content!.Headers.ContentRange!;
                bool isFinal = range.To!.Value + 1 == range.Length!.Value;
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
            byte[] bytes = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new LargeRecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentRange?.ToString(),
                bytes.LongLength));
            (HttpStatusCode Status, string Body) = pending.Dequeue();
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
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

    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] expected =
            [
                "/v1.0/users/sender%40example.test/messages",
                "/v1.0/users/sender%40example.test/messages/draft-id/attachments/createUploadSession",
                "/upload/session-secret",
                "/v1.0/users/sender%40example.test/messages/draft-id/send"
            ];
}

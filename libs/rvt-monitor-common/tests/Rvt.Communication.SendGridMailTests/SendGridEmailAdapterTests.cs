using System.Net;
using System.Net.Http.Headers;
using Moq;
using Rvt.Communication.Abstractions;
using Rvt.Communication.SendGridMail;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Rvt.Communication.SendGridMailTests;

[TestClass]
public sealed class SendGridEmailAdapterTests
{
    [TestMethod]
    public async Task SendAsync_MapsSenderBodiesRecipientAndAttachments()
    {
        SendGridMessage? captured = null;
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback((SendGridMessage message, CancellationToken _) => captured = message)
            .ReturnsAsync(Response(HttpStatusCode.Accepted));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);
        EmailAttachment attachment = new EmailAttachment("report.pdf", "application/pdf", new byte[] { 1, 2, 3 });

        await adapter.SendAsync(new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", [attachment]));

        Assert.AreEqual("sender@example.test", captured!.From.Email);
        Assert.AreEqual("RVT Cloud", captured.From.Name);
        Assert.AreEqual("ops@example.test", captured.Personalizations.Single().Tos.Single().Email);
        Assert.AreEqual("subject", captured.Subject);
        Assert.IsTrue(captured.Contents.Any(content => content.Type == "text/plain" && content.Value == "plain"));
        Assert.IsTrue(captured.Contents.Any(content => content.Type == "text/html" && content.Value == "<p>html</p>"));
        Attachment sentAttachment = captured.Attachments.Single();
        Assert.AreEqual("report.pdf", sentAttachment.Filename);
        Assert.AreEqual("application/pdf", sentAttachment.Type);
        Assert.AreEqual(Convert.ToBase64String(new byte[] { 1, 2, 3 }), sentAttachment.Content);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.RequestTimeout, DeliveryFailureKind.Transient)]
    [DataRow((HttpStatusCode)429, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.InternalServerError, DeliveryFailureKind.Transient)]
    [DataRow(HttpStatusCode.BadRequest, DeliveryFailureKind.Permanent)]
    public async Task SendAsync_ClassifiesStatusWithoutRawResponse(
        HttpStatusCode status,
        DeliveryFailureKind expectedKind)
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(status, "raw provider secret"));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(expectedKind, exception.FailureKind);
        Assert.AreEqual(((int)status).ToString(), exception.Code);
        Assert.DoesNotContain("raw provider secret", exception.ToString());
        Assert.DoesNotContain("ops@example.test", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_PropagatesCallerCancellation()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), source.Token))
            .ThrowsAsync(new OperationCanceledException(source.Token));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(Request(), source.Token));
    }

    [TestMethod]
    public async Task SendAsync_NetworkFailureIsTransientAndSafe()
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("raw network secret"));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.DoesNotContain("raw network secret", exception.Message);
    }

    [TestMethod]
    public async Task SendAsync_AcceptsAnySuccessfulStatus()
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(HttpStatusCode.OK));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        await adapter.SendAsync(Request());
    }

    [TestMethod]
    public async Task SendAsync_ThrottleResponseCarriesRetryAfter()
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(
                (HttpStatusCode)429,
                configureHeaders: headers => headers.RetryAfter = new RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(75))));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(TimeSpan.FromSeconds(75), exception.RetryAfter);
    }

    [TestMethod]
    public async Task SendAsync_MissingConfigurationFailsBeforeProviderCall()
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>(MockBehavior.Strict);
        Mock<ISendGridClientFactory> factory = new Mock<ISendGridClientFactory>();
        factory.Setup(x => x.Create(string.Empty)).Returns(client.Object);
        SendGridEmailAdapter adapter = new SendGridEmailAdapter(factory.Object, new SendGridMailOptions());

        EmailDeliveryException exception = await Assert.ThrowsExactlyAsync<EmailDeliveryException>(() =>
            adapter.SendAsync(Request()));

        Assert.AreEqual(DeliveryFailureKind.Configuration, exception.FailureKind);
        client.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Constructor_CreatesAndReusesOneClient()
    {
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(HttpStatusCode.Accepted));
        Mock<ISendGridClientFactory> factory = new Mock<ISendGridClientFactory>();
        factory.Setup(x => x.Create("api-key")).Returns(client.Object);
        SendGridEmailAdapter adapter = new SendGridEmailAdapter(factory.Object, Options());

        await adapter.SendAsync(Request());
        await adapter.SendAsync(Request());

        factory.Verify(x => x.Create("api-key"), Times.Once);
        client.Verify(
            x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task SendAsync_DisposesProviderResponseBody()
    {
        TrackingContent content = new TrackingContent();
        Mock<ISendGridClient> client = new Mock<ISendGridClient>();
        client.Setup(x => x.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(HttpStatusCode.Accepted, content: content));
        SendGridEmailAdapter adapter = CreateAdapter(client.Object);

        await adapter.SendAsync(Request());

        Assert.IsTrue(content.IsDisposed);
    }

    private static SendGridEmailAdapter CreateAdapter(ISendGridClient client)
    {
        Mock<ISendGridClientFactory> factory = new Mock<ISendGridClientFactory>();
        factory.Setup(x => x.Create("api-key")).Returns(client);
        return new SendGridEmailAdapter(factory.Object, Options());
    }

    private static EmailDeliveryRequest Request() =>
        new("ops@example.test", "subject", "plain", "<p>html</p>", []);

    private static SendGridMailOptions Options() => new()
    {
        Enabled = true,
        ApiKey = "api-key",
        FromEmail = "sender@example.test",
        FromName = "RVT Cloud"
    };

    private static Response Response(
        HttpStatusCode status,
        string body = "",
        Action<HttpResponseHeaders>? configureHeaders = null,
        HttpContent? content = null)
    {
        HttpResponseMessage message = new HttpResponseMessage(status);
        HttpResponseHeaders headers = message.Headers;
        configureHeaders?.Invoke(headers);
        return new Response(status, content ?? new StringContent(body), headers);
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
}

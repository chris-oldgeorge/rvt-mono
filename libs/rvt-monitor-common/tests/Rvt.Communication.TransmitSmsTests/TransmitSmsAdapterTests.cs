using System.Net;
using System.Net.Http.Headers;
using Rvt.Communication.Abstractions;
using Rvt.Communication.TransmitSms;
using static Rvt.Communication.TransmitSmsTests.TransmitSmsClientTests;

namespace Rvt.Communication.TransmitSmsTests;

[TestClass]
public sealed class TransmitSmsAdapterTests
{
    [TestMethod]
    public async Task SendAsync_MapsPortRequestAndConfiguredCredentials()
    {
        using CapturingHandler handler = SuccessHandler();
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        await adapter.SendAsync(
            new SmsDeliveryRequest("447700900123", "Threshold breached"),
            CancellationToken.None);

        Assert.AreEqual(
            "message=Threshold+breached&to=447700900123&from=KrakenAlert",
            handler.RequestBody);
    }

    [TestMethod]
    public async Task SendAsync_DisabledSmsIsConfigurationFailureBeforeNetworkCall()
    {
        using CapturingHandler handler = SuccessHandler();
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, new TransmitSmsOptions
        {
            Enabled = false
        });

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Configuration, exception.FailureKind);
        Assert.IsNull(handler.Request);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow((HttpStatusCode)429)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_TransientHttpStatusIsClassified(HttpStatusCode statusCode)
    {
        using CapturingHandler handler = new(statusCode, "raw-private-response");
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual(((int)statusCode).ToString(), exception.Code);
        Assert.DoesNotContain("raw-private-response", exception.ToString());
        Assert.DoesNotContain("private-body", exception.ToString());
        Assert.DoesNotContain("447700900123", exception.ToString());
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    public async Task SendAsync_OtherHttpClientFailuresArePermanent(HttpStatusCode statusCode)
    {
        using CapturingHandler handler = new(statusCode, "raw-private-response");
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
    }

    [TestMethod]
    public async Task SendAsync_ApiLevelFailureIsPermanentAndKeepsOnlyCode()
    {
        using CapturingHandler handler = new(
            HttpStatusCode.OK,
            """{"error":{"code":"FIELD_INVALID","description":"raw private recipient"}}""");
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.AreEqual("FIELD_INVALID", exception.Code);
        Assert.DoesNotContain("raw private recipient", exception.ToString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("""{"error":{}}""")]
    [DataRow("""{"result":"queued"}""")]
    public async Task SendAsync_SuccessfulResponseWithNoRecognisableErrorCodeIsTransient(string body)
    {
        using CapturingHandler handler = new(HttpStatusCode.OK, body);
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        // An empty or blank body fails to parse at all and already routed
        // through the JsonException path; the rest used to dead-letter.
        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
    }

    [TestMethod]
    public async Task SendAsync_SuccessfulResponseWithNoErrorCode_CarriesTheUnknownSentinel()
    {
        using CapturingHandler handler = new(HttpStatusCode.OK, """{"error":{}}""");
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual("UNKNOWN", exception.Code);
        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
    }

    [TestMethod]
    public async Task SendAsync_NetworkFailureIsTransient()
    {
        using ThrowingHandler handler = new(new HttpRequestException("raw network secret"));
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.DoesNotContain("raw network secret", exception.Message);
    }

    [TestMethod]
    public async Task SendAsync_TransientHttpFailureRetainsRetryAfter()
    {
        using CapturingHandler handler = new(
            (HttpStatusCode)429,
            "raw-private-response",
            TimeSpan.FromSeconds(30));
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        SmsDeliveryException exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body"), TestContext.CancellationToken));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("429", exception.Code);
        Assert.AreEqual(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [TestMethod]
    public async Task SendAsync_CallerCancellationPropagates()
    {
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        using ThrowingHandler handler = new(
            new OperationCanceledException(cancellationSource.Token));
        using HttpClient httpClient = new(handler);
        TransmitSmsAdapter adapter = new(httpClient, EnabledOptions());

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(
                new SmsDeliveryRequest("447700900123", "private-body"),
                cancellationSource.Token));

        Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
    }

    private static CapturingHandler SuccessHandler() => new(
        HttpStatusCode.OK,
        """{"error":{"code":"SUCCESS","description":"OK"}}""");

    private static TransmitSmsOptions EnabledOptions() => new()
    {
        Enabled = true,
        ApiKey = "api-key",
        ApiSecret = "api-secret",
        Sender = "KrakenAlert"
    };

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    public TestContext TestContext { get; set; } = null!;
}

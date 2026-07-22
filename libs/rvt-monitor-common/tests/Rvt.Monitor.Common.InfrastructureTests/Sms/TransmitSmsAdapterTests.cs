using System.Net;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;
using Rvt.Monitor.Common.Infrastructure.Sms;
using static Rvt.Monitor.Common.InfrastructureTests.Sms.TransmitSmsClientTests;

namespace Rvt.Monitor.Common.InfrastructureTests.Sms;

[TestClass]
public sealed class TransmitSmsAdapterTests
{
    [TestMethod]
    public async Task SendAsync_MapsPortRequestAndConfiguredCredentials()
    {
        using var handler = SuccessHandler();
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

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
        using var handler = SuccessHandler();
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, new CommunicationsOptions
        {
            EmailEnabled = false
        });

        var exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "body")));

        Assert.AreEqual(DeliveryFailureKind.Configuration, exception.FailureKind);
        Assert.IsNull(handler.Request);
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow((HttpStatusCode)429)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_TransientHttpStatusIsClassified(HttpStatusCode statusCode)
    {
        using var handler = new CapturingHandler(statusCode, "raw-private-response");
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

        var exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body")));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual(((int)statusCode).ToString(), exception.Code);
        Assert.DoesNotContain("raw-private-response", exception.ToString());
        Assert.DoesNotContain("private-body", exception.ToString());
        Assert.DoesNotContain("447700900123", exception.ToString());
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    public async Task SendAsync_OtherHttpClientFailuresArePermanent(HttpStatusCode statusCode)
    {
        using var handler = new CapturingHandler(statusCode, "raw-private-response");
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

        var exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body")));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
    }

    [TestMethod]
    public async Task SendAsync_ApiLevelFailureIsPermanentAndKeepsOnlyCode()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"error":{"code":"FIELD_INVALID","description":"raw private recipient"}}""");
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

        var exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body")));

        Assert.AreEqual(DeliveryFailureKind.Permanent, exception.FailureKind);
        Assert.AreEqual("FIELD_INVALID", exception.Code);
        Assert.DoesNotContain("raw private recipient", exception.ToString());
    }

    [TestMethod]
    public async Task SendAsync_NetworkFailureIsTransient()
    {
        using var handler = new ThrowingHandler(new HttpRequestException("raw network secret"));
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

        var exception = await Assert.ThrowsExactlyAsync<SmsDeliveryException>(() =>
            adapter.SendAsync(new SmsDeliveryRequest("447700900123", "private-body")));

        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.DoesNotContain("raw network secret", exception.Message);
    }

    [TestMethod]
    public async Task SendAsync_CallerCancellationPropagates()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        using var handler = new ThrowingHandler(
            new OperationCanceledException(cancellationSource.Token));
        using var httpClient = new HttpClient(handler);
        var adapter = new TransmitSmsAdapter(httpClient, EnabledOptions());

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(
                new SmsDeliveryRequest("447700900123", "private-body"),
                cancellationSource.Token));

        Assert.AreEqual(cancellationSource.Token, exception.CancellationToken);
    }

    private static CapturingHandler SuccessHandler() => new(
        HttpStatusCode.OK,
        """{"error":{"code":"SUCCESS","description":"OK"}}""");

    private static CommunicationsOptions EnabledOptions() => new()
    {
        EmailEnabled = false,
        SmsEnabled = true,
        SmsApiKey = "api-key",
        SmsApiSecret = "api-secret",
        SmsSender = "KrakenAlert"
    };

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }
}

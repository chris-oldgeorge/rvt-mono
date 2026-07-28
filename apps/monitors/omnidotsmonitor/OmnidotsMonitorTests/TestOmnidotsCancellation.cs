// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api.Http;
using Omnidots.Api.Ports;
using Omnidots.Api.UseCases;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;

namespace OmnidotsAdapterTests;

/// <summary>
/// The Omnidots import chain previously blocked on <c>.Result</c> for every
/// vendor call and dropped the scheduler's token for most jobs, so a
/// container stop could not interrupt a vibration import. Two async methods
/// used <c>WaitAsync</c>, which abandons the wait but leaves the vendor
/// request running. These tests pin the token's journey to the request.
/// </summary>
[TestClass]
public class TestOmnidotsCancellation
{
    [TestInitialize]
    public void InitializeLogger()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(TestOmnidotsCancellation));
    }

    [TestMethod]
    public async Task HttpWebClient_PassesTheCallerTokenToTheVendorGet()
    {
        TokenCapturingHandler handler = new TokenCapturingHandler();
        using HttpClient inner = new HttpClient(handler);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        HttpWebClient subject = new HttpWebClient("https://omnidots.example.test/", inner);

        await subject.GetAsync("/api/v1/list_measuring_points", cancellation.Token);

        Assert.IsTrue(handler.ObservedToken.CanBeCanceled);
    }

    [TestMethod]
    public async Task HttpWebClient_WhenTheCallerCancels_TheVendorGetIsCancelled()
    {
        using HttpClient inner = new HttpClient(new TokenCapturingHandler());
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        HttpWebClient subject = new HttpWebClient("https://omnidots.example.test/", inner);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => subject.GetAsync("/api/v1/list_measuring_points", cancellation.Token));
    }

    [TestMethod]
    public async Task GatewayPeakRecords_PropagatesTheTokenToTheHttpPort()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Mock<IHttpClient> httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"samples\":[]}");
        OmnidotsHttpGateway gateway = new OmnidotsHttpGateway(httpClient.Object, "user", "auth");

        await gateway.GetPeakRecordsAsync("token", DateTime.UtcNow, null, "serial", cancellation.Token);

        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task GatewayAuthenticate_PropagatesTheTokenToTheHttpPort()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Mock<IHttpClient> httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        httpClient
            .Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        OmnidotsHttpGateway gateway = new OmnidotsHttpGateway(httpClient.Object, "user", "auth");

        TokenResponse response = await gateway.AuthenticateAsync(cancellation.Token);

        Assert.AreEqual("vendor-token", response.Token);
        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task GatewayListMeasuringPoints_AuthenticatesAndReadsUnderTheSameToken()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Mock<IHttpClient> httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
        httpClient
            .Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        httpClient
            .Setup(client => client.GetAsync(
                "/api/v1/list_measuring_points?token=vendor-token",
                cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"measuring_points\":[]}");
        OmnidotsHttpGateway gateway = new OmnidotsHttpGateway(httpClient.Object, "user", "auth");

        await gateway.ListMeasuringPointsAsync(cancellation.Token);

        httpClient.VerifyAll();
    }

    [TestMethod]
    public void UseCasesDependOnThePortNotTheHttpAdapter()
    {
        // Hexagonal boundary: the import use cases must be constructible
        // against the port alone, with no reference to the HTTP adapter.
        foreach (Type? handler in new[]
                 {
                     typeof(StoreMonitorsHandler),
                     typeof(StorePeakRecordsHandler),
                     typeof(StoreVeffRecordsHandler),
                     typeof(StoreVdvRecordsHandler),
                     typeof(StoreTracesHandler),
                     typeof(ConfigureMeasuringPointHandler)
                 })
        {
            ParameterInfo[] adapterParameters = [.. handler
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType == typeof(OmnidotsHttpGateway))];

            Assert.IsEmpty(
                adapterParameters,
                $"{handler.Name} must depend on {nameof(IOmnidotsVendorGateway)}, not the concrete adapter.");
        }
    }

    [TestMethod]
    public void TheVendorGatewayNoLongerBlocksOnSynchronousResult()
    {
        // The port exists so the import chain stays asynchronous end to end;
        // every member must be awaitable.
        string[] nonAsync =
        [
            .. typeof(IOmnidotsVendorGateway)
                .GetMethods()
                .Where(method => !typeof(Task).IsAssignableFrom(method.ReturnType))
                .Select(method => method.Name),
        ];

        Assert.IsEmpty(nonAsync, "Every vendor port member must return a Task.");
    }

    private sealed class TokenCapturingHandler : HttpMessageHandler
    {
        public CancellationToken ObservedToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}

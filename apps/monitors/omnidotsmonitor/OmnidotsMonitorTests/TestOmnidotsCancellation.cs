// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.Ports;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Omnidots.Model.Json;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

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
        TokenCapturingHandler handler = new();
        using HttpClient inner = new(handler);
        using CancellationTokenSource cancellation = new();
        HttpWebClient subject = new("https://omnidots.example.test/", inner);

        await subject.GetAsync("/api/v1/list_measuring_points", cancellation.Token);

        Assert.IsTrue(handler.ObservedToken.CanBeCanceled);
    }

    [TestMethod]
    public async Task HttpWebClient_WhenTheCallerCancels_TheVendorGetIsCancelled()
    {
        using HttpClient inner = new(new TokenCapturingHandler());
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        HttpWebClient subject = new("https://omnidots.example.test/", inner);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => subject.GetAsync("/api/v1/list_measuring_points", cancellation.Token));
    }

    [TestMethod]
    public async Task GatewayPeakRecords_PropagatesTheTokenToTheHttpPort()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"samples\":[]}");
        OmnidotsHttpGateway gateway = new(httpClient.Object, "user", "auth");

        await gateway.GetPeakRecordsAsync("token", DateTime.UtcNow, null, "serial", cancellation.Token);

        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task GatewayAuthenticate_PropagatesTheTokenToTheHttpPort()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .ReturnsAsync("{\"ok\":true,\"token\":\"vendor-token\"}");
        OmnidotsHttpGateway gateway = new(httpClient.Object, "user", "auth");

        TokenResponse response = await gateway.AuthenticateAsync(cancellation.Token);

        Assert.AreEqual("vendor-token", response.Token);
        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task GatewayListMeasuringPoints_AuthenticatesAndReadsUnderTheSameToken()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
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
        OmnidotsHttpGateway gateway = new(httpClient.Object, "user", "auth");

        await gateway.ListMeasuringPointsAsync(cancellation.Token);

        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task StoreTraces_WhenVendorReadIsCancelled_PropagatesWithoutRecordingFailure()
    {
        using CancellationTokenSource cancellation = new();
        OmnidotsApi subject = TestUtil.CreateApiAndMocks(
            out Mock<IHttpClient> httpClient,
            out Mock<IDBClient> dbClient,
            out Mock<IMqttClient> mqttClient,
            out Mock<IMessageService> messageClient,
            traceCollectionOptions: new OmnidotsTraceCollectionOptions
            {
                AllowedSerialIds = [],
                MaxMonitorsPerRun = 1
            });
        dbClient
            .Setup(client => client.ReadMonitorList(It.IsAny<DateTime?>()))
            .Returns(OmnidotsFixture.MonitorsList(1));
        httpClient
            .Setup(client => client.PostAsync(
                "/api/v1/user/authenticate",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .Returns(OmnidotsFixture.AuthenticateTask("trace-token"));
        httpClient
            .Setup(client => client.GetAsync(
                It.Is<string>(url => url.StartsWith(
                    "/api/v1/get_traces_list",
                    StringComparison.Ordinal)),
                cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<string>(cancellation.Token);
            });

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => subject.StoreTracesAsync(
                DateTime.UtcNow.AddMinutes(-5),
                cancellation.Token));

        dbClient.Verify(
            client => client.HandleException(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Never);
        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
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

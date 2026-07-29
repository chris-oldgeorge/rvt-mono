using System.Reflection;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace AirQMonitorTests;

/// <summary>
/// The AirQ import chain previously blocked on <c>.Result</c> and dropped the
/// scheduler's token at the job boundary, so a container stop could not
/// interrupt an in-flight fleet import. These tests pin the token's journey
/// from the job entry point down to the vendor request.
/// </summary>
[TestClass]
public class TestAirQCancellation
{
    [TestInitialize]
    public void InitializeLogger()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(TestAirQCancellation));
    }

    [TestMethod]
    public async Task HttpWebClient_PassesTheCallerTokenToTheVendorRequest()
    {
        using CancellationTokenSource cancellation = new();
        TokenCapturingHandler handler = new();
        using HttpClient inner = new(handler);
        HttpWebClient subject = new("https://airq.example.test/", inner);

        await subject.GetAsync("/latestData", cancellation.Token);

        Assert.IsTrue(handler.ObservedToken.CanBeCanceled);
    }

    [TestMethod]
    public async Task HttpWebClient_WhenTheCallerCancels_TheVendorRequestIsCancelled()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using HttpClient inner = new(new TokenCapturingHandler());
        HttpWebClient subject = new("https://airq.example.test/", inner);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => subject.GetAsync("/latestData", cancellation.Token));
    }

    [TestMethod]
    public async Task GatewayLatestSamples_PropagatesTheTokenToTheHttpPort()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), cancellation.Token))
            .ReturnsAsync("[]");
        AirQHttpGateway gateway = new(httpClient.Object);

        await gateway.GetLatestSamplesAsync("user", "auth", "Device1", DateTime.UtcNow, cancellation.Token);

        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task GatewayLatestSamples_ReturnsTheAdvancedWatermarkInsteadOfMutatingAnArgument()
    {
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");
        AirQHttpGateway gateway = new(httpClient.Object);
        DateTime watermark = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        LatestSamplesResult result = await gateway.GetLatestSamplesAsync("user", "auth", "Device1", watermark, TestContext.CancellationToken);

        Assert.IsNotNull(result.Samples);
        Assert.AreEqual(watermark, result.LatestDateTime);
    }

    [TestMethod]
    public async Task GatewayCancellation_SurfacesAsCancellationNotAsAnAdapterFailure()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Mock<IHttpClient> httpClient = new();
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        AirQHttpGateway gateway = new(httpClient.Object);

        // A shutdown must not be recorded as a vendor adapter fault.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => gateway.GetMonitorsAsync("user", "auth", cancellation.Token));
    }

    [TestMethod]
    public async Task StoreMonitorsHandler_StopsBeforeTheVendorCallWhenAlreadyCancelled()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Mock<IAirQVendorGateway> gateway = new(MockBehavior.Strict);
        gateway
            .Setup(port => port.GetMonitorsAsync("user", "auth", cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        Mock<IAirQMonitorCommands> monitorCommands = new(MockBehavior.Strict);
        Mock<IAirQOperationalCommands> operationalCommands = new(MockBehavior.Strict);
        StoreMonitorsHandler handler = new(
            gateway.Object,
            monitorCommands.Object,
            operationalCommands.Object,
            AirQTestLocalMonitorFilter.Create(false, null));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.RunAsync("user", "auth", cancellation.Token));

        // Cancellation is not an import failure, so nothing is written and
        // no error row is recorded.
        operationalCommands.Verify(
            commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Never);
        monitorCommands.Verify(
            commands => commands.WriteMonitorList(It.IsAny<List<NoiseMonitorDto>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task StoreNoiseLevelsHandler_MissingWatermark_UsesUtcFallback()
    {
        DateTimeOffset now = new(2026, 7, 29, 10, 15, 0, TimeSpan.Zero);
        DateTime? observedWatermark = null;
        Mock<IAirQVendorGateway> gateway = new();
        gateway
            .Setup(port => port.GetLatestSamplesAsync(
                "user",
                "auth",
                "Device1",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTime, CancellationToken>(
                (_, _, _, watermark, _) => observedWatermark = watermark)
            .ReturnsAsync((
                string _,
                string _,
                string _,
                DateTime watermark,
                CancellationToken _) =>
                new LatestSamplesResult([], watermark));
        Mock<IAirQMonitorQueries> monitorQueries = new();
        monitorQueries
            .Setup(queries => queries.ReadMonitorList(null))
            .Returns(
            [
                new NoiseMonitorDto(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    lastDataTime: null,
                    "Device1",
                    "Model",
                    "Firmware",
                    "Turnkey",
                    "Fleet-1",
                    0,
                    0,
                    null,
                    "UTC",
                    null,
                    offline: false,
                    new NoiseMonitorStatus(
                        DateTime.UtcNow,
                        NoiseMonitorStatus.ACTIVE,
                        0,
                        null,
                        null,
                        null,
                        null))
            ]);
        AirQMonitorReader monitorReader = new(
            monitorQueries.Object,
            AirQTestLocalMonitorFilter.Create(false, null));
        Mock<IAirQRuleQueries> ruleQueries = new();
        Mock<IAirQMonitorCommands> monitorCommands = new();
        Mock<IAirQMeasurementCommands> measurementCommands = new();
        Mock<IAirQOperationalCommands> operationalCommands = new();
        Mock<IMonitorEventPublisher> eventPublisher = new();
        AirQRuleProcessor ruleProcessor = new(
            ruleQueries.Object,
            operationalCommands.Object,
            Mock.Of<IAlertIngressPort>());
        StoreNoiseLevelsHandler subject = new(
            gateway.Object,
            monitorReader,
            ruleQueries.Object,
            monitorCommands.Object,
            measurementCommands.Object,
            operationalCommands.Object,
            eventPublisher.Object,
            ruleProcessor,
            new FixedTimeProvider(now));
        DateTime expectedWatermark = now.UtcDateTime.AddYears(-1);

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);

        Assert.IsTrue(observedWatermark.HasValue);
        Assert.AreEqual(DateTimeKind.Utc, observedWatermark.Value.Kind);
        Assert.AreEqual(expectedWatermark, observedWatermark.Value);
    }

    [TestMethod]
    public void UseCasesDependOnThePortNotTheHttpAdapter()
    {
        // Hexagonal boundary: the import use cases must be constructible
        // against the port alone, with no reference to the HTTP adapter.
        foreach (Type? handler in new[]
                 {
                     typeof(StoreMonitorsHandler),
                     typeof(StoreNoiseLevelsHandler),
                     typeof(StoreNoiseLevelsForDateHandler)
                 })
        {
            ParameterInfo[] gatewayParameters = [.. handler
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType == typeof(AirQHttpGateway))];

            Assert.IsEmpty(
                gatewayParameters,
                $"{handler.Name} must depend on {nameof(IAirQVendorGateway)}, not the concrete adapter.");
        }
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
                Content = new StringContent("[]")
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public TestContext TestContext { get; set; } = null!;
}

using System.Reflection;
using AirQ.Api;
using AirQ.Api.Http;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQMonitorTests
{
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
            using var cancellation = new CancellationTokenSource();
            var handler = new TokenCapturingHandler();
            using var inner = new HttpClient(handler);
            var subject = new HttpWebClient<object>("https://airq.example.test/", inner);

            await subject.GetAsync("/latestData", cancellation.Token);

            Assert.IsTrue(handler.ObservedToken.CanBeCanceled);
        }

        [TestMethod]
        public async Task HttpWebClient_WhenTheCallerCancels_TheVendorRequestIsCancelled()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            using var inner = new HttpClient(new TokenCapturingHandler());
            var subject = new HttpWebClient<object>("https://airq.example.test/", inner);

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => subject.GetAsync("/latestData", cancellation.Token));
        }

        [TestMethod]
        public async Task GatewayLatestSamples_PropagatesTheTokenToTheHttpPort()
        {
            using var cancellation = new CancellationTokenSource();
            var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
            httpClient
                .Setup(client => client.GetAsync(It.IsAny<string>(), cancellation.Token))
                .ReturnsAsync("[]");
            var gateway = new AirQHttpGateway(httpClient.Object);

            await gateway.GetLatestSamplesAsync("user", "auth", "Device1", DateTime.UtcNow, cancellation.Token);

            httpClient.VerifyAll();
        }

        [TestMethod]
        public async Task GatewayLatestSamples_ReturnsTheAdvancedWatermarkInsteadOfMutatingAnArgument()
        {
            var httpClient = new Mock<IHttpClient>(MockBehavior.Strict);
            httpClient
                .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("[]");
            var gateway = new AirQHttpGateway(httpClient.Object);
            var watermark = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            LatestSamplesResult result = await gateway.GetLatestSamplesAsync("user", "auth", "Device1", watermark);

            Assert.IsNotNull(result.Samples);
            Assert.AreEqual(watermark, result.LatestDateTime);
        }

        [TestMethod]
        public async Task GatewayCancellation_SurfacesAsCancellationNotAsAnAdapterFailure()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var httpClient = new Mock<IHttpClient>();
            httpClient
                .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException(cancellation.Token));
            var gateway = new AirQHttpGateway(httpClient.Object);

            // A shutdown must not be recorded as a vendor adapter fault.
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => gateway.GetMonitorsAsync("user", "auth", cancellation.Token));
        }

        [TestMethod]
        public async Task StoreMonitorsHandler_StopsBeforeTheVendorCallWhenAlreadyCancelled()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var gateway = new Mock<IAirQVendorGateway>(MockBehavior.Strict);
            gateway
                .Setup(port => port.GetMonitorsAsync("user", "auth", cancellation.Token))
                .ThrowsAsync(new OperationCanceledException(cancellation.Token));
            var monitorCommands = new Mock<AirQ.Api.Db.IAirQMonitorCommands>(MockBehavior.Strict);
            var operationalCommands = new Mock<AirQ.Api.Db.IAirQOperationalCommands>(MockBehavior.Strict);
            var handler = new StoreMonitorsHandler(
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
                ParameterInfo[] gatewayParameters = handler
                    .GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Where(parameter => parameter.ParameterType == typeof(AirQHttpGateway))
                    .ToArray();

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
    }
}

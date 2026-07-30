using System.Globalization;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Http;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using AirQ.Common;
using AirQ.Model.Dto;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Mqtt;

namespace AirQMonitorTests;

/// <summary>
/// A single future-dated vendor sample used to be persisted as the monitor's
/// watermark, after which every real sample compared as older and was silently
/// discarded for good. These tests pin both halves of the fix: the sample is
/// dropped instead of raising the watermark, and a monitor already poisoned
/// with a future watermark recovers without a manual database edit.
/// </summary>
[TestClass]
public sealed class AirQFutureSampleWatermarkTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task GatewayLatestSamples_DropsFutureDatedSamplesAndLeavesTheWatermarkInThePast()
    {
        DateTime realSample = _now.UtcDateTime.AddMinutes(-15);
        DateTime futureSample = _now.UtcDateTime.AddYears(4);
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplesJson(realSample, futureSample));
        AirQHttpGateway gateway = new(httpClient.Object, new FixedTimeProvider(_now));

        LatestSamplesResult result = await gateway.GetLatestSamplesAsync(
            "user",
            "auth",
            "Device1",
            _now.UtcDateTime.AddHours(-1),
            TestContext.CancellationToken);

        Assert.HasCount(1, result.Samples);
        Assert.AreEqual(realSample, DateTimeUtil.ToUtc((DateTime)result.Samples[0].Utc!));
        Assert.AreEqual(realSample, result.LatestDateTime);
    }

    [TestMethod]
    public async Task StoreNoiseLevels_DoesNotPersistAWatermarkAheadOfTheClock()
    {
        DateTime futureSample = _now.UtcDateTime.AddYears(4);
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplesJson(futureSample));
        AirQHttpGateway gateway = new(httpClient.Object, new FixedTimeProvider(_now));
        Mock<IAirQMonitorCommands> monitorCommands = new();
        Mock<IAirQMeasurementCommands> measurementCommands = new();
        StoreNoiseLevelsHandler subject = CreateHandler(
            gateway,
            lastDataTime: _now.UtcDateTime.AddHours(-1),
            monitorCommands,
            measurementCommands);

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);

        // Nothing was written and the watermark was left alone, so the next run
        // still asks for - and keeps - the real samples.
        measurementCommands.Verify(
            commands => commands.InsertNoiseDtos(It.IsAny<string>(), It.IsAny<List<NoiseDto>>()),
            Times.Never);
        monitorCommands.Verify(
            commands => commands.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [TestMethod]
    public async Task StoreNoiseLevels_RecoversAMonitorAlreadyPoisonedWithAFutureWatermark()
    {
        DateTime realSample = _now.UtcDateTime.AddMinutes(-15);
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplesJson(realSample));
        AirQHttpGateway gateway = new(httpClient.Object, new FixedTimeProvider(_now));
        Mock<IAirQMonitorCommands> monitorCommands = new();
        Mock<IAirQMeasurementCommands> measurementCommands = new();
        StoreNoiseLevelsHandler subject = CreateHandler(
            gateway,
            lastDataTime: _now.UtcDateTime.AddYears(4),
            monitorCommands,
            measurementCommands);

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);

        // The stored 2030 watermark is discarded as unusable, so the backlog is
        // imported and the watermark returns to the newest real sample.
        measurementCommands.Verify(
            commands => commands.InsertNoiseDtos("Device1", It.Is<List<NoiseDto>>(dtos => dtos.Count == 1)),
            Times.Once);
        monitorCommands.Verify(
            commands => commands.WriteLatestTimestamp("Device1", realSample),
            Times.Once);
    }

    private static StoreNoiseLevelsHandler CreateHandler(
        IAirQVendorGateway gateway,
        DateTime? lastDataTime,
        Mock<IAirQMonitorCommands> monitorCommands,
        Mock<IAirQMeasurementCommands> measurementCommands)
    {
        Mock<IAirQMonitorQueries> monitorQueries = new();
        monitorQueries
            .Setup(queries => queries.ReadMonitorList(null))
            .Returns([Monitor(lastDataTime)]);
        AirQMonitorReader monitorReader = new(
            monitorQueries.Object,
            AirQTestLocalMonitorFilter.Create(false, null));
        Mock<IAirQRuleQueries> ruleQueries = new();
        ruleQueries.Setup(queries => queries.ReadRules(It.IsAny<string>())).Returns([]);
        Mock<IAirQOperationalCommands> operationalCommands = new();
        AirQRuleProcessor ruleProcessor = new(
            ruleQueries.Object,
            operationalCommands.Object,
            Mock.Of<IAlertIngressPort>());

        return new StoreNoiseLevelsHandler(
            gateway,
            monitorReader,
            ruleQueries.Object,
            monitorCommands.Object,
            measurementCommands.Object,
            operationalCommands.Object,
            Mock.Of<IMonitorEventPublisher>(),
            ruleProcessor,
            new FixedTimeProvider(_now));
    }

    private static NoiseMonitorDto Monitor(DateTime? lastDataTime) =>
        new(
            Guid.NewGuid(),
            _now.UtcDateTime,
            lastDataTime,
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
                _now.UtcDateTime,
                NoiseMonitorStatus.ACTIVE,
                0,
                null,
                null,
                null,
                null));

    private static string SamplesJson(params DateTime[] sampleTimesUtc) =>
        "[" + string.Join(",", sampleTimesUtc.Select(sampleTime => string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            {
              "utc": "{{sampleTime.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00", CultureInfo.InvariantCulture)}}",
              "timestamp": "{{sampleTime.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00", CultureInfo.InvariantCulture)}}",
              "instrumentID": "Device1",
              "location": "Initial Configuration",
              "gpsCoordinates": "51.2500,0.75000",
              "data": [
                { "unit": "dB", "name": "LAeq(T)", "value": "44.75" },
                { "unit": "dB", "name": "LAmax(T)", "value": "61.28" },
                { "unit": "dB", "name": "LA90(T)", "value": "43.00" },
                { "unit": "dB", "name": "LA10(T)", "value": "44.47" },
                { "unit": "dB", "name": "LCeq(T)", "value": "54.19" },
                { "unit": "dB", "name": "LCmax(T)", "value": "82.81" },
                { "unit": "dB", "name": "LC90(T)", "value": "47.56" },
                { "unit": "dB", "name": "LC10(T)", "value": "51.22" }
              ]
            }
            """))) + "]";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public TestContext TestContext { get; set; } = null!;
}

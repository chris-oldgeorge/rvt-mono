using System.Globalization;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.Ports;
using AirQ.Api.UseCases;
using AirQ.Model.Config;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace AirQMonitorTests;

/// <summary>
/// An unwatermarked AirQ monitor is seeded a year back, and that seed used to
/// drive the averaging loop and the rule windows as well as the truncation
/// threshold: roughly 1,095 Create8hourAverage calls plus 8,760 hour windows,
/// each its own context and aggregate query, inside the fleet loop. The
/// processing start is now clamped to AirQImportOptions.MaximumInitialBackfill.
/// </summary>
[TestClass]
public sealed class AirQImportBackfillBoundTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTime _sampleTime = new(2026, 1, 7, 23, 45, 0, DateTimeKind.Utc);
    private static readonly string[] _sampleFields =
        ["LAeq(T)", "LAmax(T)", "LA90(T)", "LA10(T)", "LCeq(T)", "LCmax(T)", "LC90(T)", "LC10(T)"];

    public AirQImportBackfillBoundTests() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(AirQImportBackfillBoundTests));

    [TestMethod]
    public async Task StoreNoiseLevels_LongPastDeployment_BoundsAveragingAndRuleWindowsByTheBackfill()
    {
        Mock<IAirQVendorGateway> gateway = new();
        gateway
            .Setup(port => port.GetLatestSamplesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestSamplesResult([Sample(_sampleTime)], _sampleTime));
        Mock<IAirQMonitorQueries> monitorQueries = new();
        monitorQueries
            .Setup(queries => queries.ReadMonitorList(null))
            .Returns([UnwatermarkedMonitor()]);
        Mock<IAirQRuleQueries> ruleQueries = new();
        ruleQueries.Setup(queries => queries.ReadRules("Device1")).Returns([HourlyRule()]);
        Mock<IAirQMeasurementCommands> measurementCommands = new();
        Mock<IAirQOperationalCommands> operationalCommands = new();
        StoreNoiseLevelsHandler subject = new(
            gateway.Object,
            new AirQMonitorReader(monitorQueries.Object, AirQTestLocalMonitorFilter.Create(false, null)),
            ruleQueries.Object,
            Mock.Of<IAirQMonitorCommands>(),
            measurementCommands.Object,
            operationalCommands.Object,
            Mock.Of<IMonitorEventPublisher>(),
            new AirQRuleProcessor(ruleQueries.Object, operationalCommands.Object, Mock.Of<IAlertIngressPort>()),
            new FixedTimeProvider(_now),
            new AirQImportOptions { MaximumInitialBackfill = TimeSpan.FromDays(7) });

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);

        // Seven days of eight-hour periods ending at or before the sample, and
        // seven days of complete hours. Unclamped these were 1,095 and 8,760.
        measurementCommands.Verify(
            commands => commands.Create8hourAverage("Device1", It.IsAny<DateTime>()),
            Times.Exactly(21));
        ruleQueries.Verify(
            queries => queries.GetAverageNoiseLevel("Device1", "LAeq", It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Exactly(168));
    }

    private static NoiseMonitorDto UnwatermarkedMonitor() =>
        new(
            Guid.NewGuid(),
            _now.UtcDateTime.AddYears(-1),
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
                _now.UtcDateTime,
                NoiseMonitorStatus.ACTIVE,
                0,
                null,
                null,
                null,
                null));

    private static RvtAlertRuleDto HourlyRule() =>
        new(
            Guid.NewGuid(),
            "Device1",
            "LAeq",
            limitOn: 70.0,
            limitOff: 60.0,
            averagingPeriod: 3600,
            AirQFixture.CreateActiveRuleActivity(null, null),
            AlertType.Alert,
            isActive: false,
            isDeleted: false,
            _now.UtcDateTime.AddYears(-1),
            null);

    private static SampleResponse Sample(DateTime sampleTimeUtc) =>
        new()
        {
            Utc = sampleTimeUtc,
            Timestamp = sampleTimeUtc,
            InstrumentID = "Device1",
            Location = "Initial Configuration",
            GpsCoordinates = "51.2500,0.75000",
            Data = [.. _sampleFields
                .Select(name => new SampleData
                {
                    Unit = "dB",
                    Name = name,
                    Value = 44.75.ToString(CultureInfo.InvariantCulture)
                })]
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public TestContext TestContext { get; set; } = null!;
}

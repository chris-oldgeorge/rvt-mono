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
/// The watermark used to be advanced before rules were evaluated, so a
/// rule-processing failure moved the start of the next run past samples that
/// were never evaluated - those alerts were lost permanently and silently.
/// The watermark is now written only after rule evaluation succeeds; the
/// re-read is safe because the alert source event key is derived from the
/// window boundaries, which the unchanged watermark reproduces exactly.
/// </summary>
[TestClass]
public sealed class AirQWatermarkOrderingTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTime _sampleTime = new(2026, 1, 7, 23, 45, 0, DateTimeKind.Utc);
    private static readonly string[] _sampleFields =
        ["LAeq(T)", "LAmax(T)", "LA90(T)", "LA10(T)", "LCeq(T)", "LCmax(T)", "LC90(T)", "LC10(T)"];

    public AirQWatermarkOrderingTests() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(AirQWatermarkOrderingTests));

    [TestMethod]
    public async Task StoreNoiseLevels_RuleEvaluationFailure_LeavesTheWatermarkUnadvanced()
    {
        Mock<IAirQMonitorCommands> monitorCommands = new();
        Mock<IAirQOperationalCommands> operationalCommands = new();

        await Assert.ThrowsAsync<AggregateException>(
            () => RunAsync(monitorCommands, operationalCommands, ruleEvaluationFails: true));

        // Samples were stored, but the next run must still start from the old
        // watermark so the unevaluated samples are re-read and re-alerted.
        monitorCommands.Verify(
            commands => commands.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
        operationalCommands.Verify(
            commands => commands.HandleException("StoreNoiseLevels SerialId=Device1", It.IsAny<Exception>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StoreNoiseLevels_RuleEvaluationSucceeds_AdvancesTheWatermark()
    {
        Mock<IAirQMonitorCommands> monitorCommands = new();
        Mock<IAirQOperationalCommands> operationalCommands = new();

        await RunAsync(monitorCommands, operationalCommands, ruleEvaluationFails: false);

        monitorCommands.Verify(
            commands => commands.WriteLatestTimestamp("Device1", _sampleTime),
            Times.Once);
    }

    private async Task RunAsync(
        Mock<IAirQMonitorCommands> monitorCommands,
        Mock<IAirQOperationalCommands> operationalCommands,
        bool ruleEvaluationFails)
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
        monitorQueries.Setup(queries => queries.ReadMonitorList(null)).Returns([Monitor()]);
        Mock<IAirQRuleQueries> ruleQueries = new();
        ruleQueries.Setup(queries => queries.ReadRules("Device1")).Returns([HourlyRule()]);
        Moq.Language.Flow.ISetup<IAirQRuleQueries, double?> averageSetup = ruleQueries.Setup(
            queries => queries.GetAverageNoiseLevel(
                "Device1",
                "LAeq",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()));
        if (ruleEvaluationFails)
        {
            averageSetup.Throws(new IOException("aggregate query failed"));
        }
        else
        {
            averageSetup.Returns((double?)null);
        }

        StoreNoiseLevelsHandler subject = new(
            gateway.Object,
            new AirQMonitorReader(monitorQueries.Object, AirQTestLocalMonitorFilter.Create(false, null)),
            ruleQueries.Object,
            monitorCommands.Object,
            Mock.Of<IAirQMeasurementCommands>(),
            operationalCommands.Object,
            Mock.Of<IMonitorEventPublisher>(),
            new AirQRuleProcessor(ruleQueries.Object, operationalCommands.Object, Mock.Of<IAlertIngressPort>()),
            new FixedTimeProvider(_now),
            new AirQImportOptions { MaximumInitialBackfill = TimeSpan.FromDays(7) });

        await subject.RunAsync("user", "auth", TestContext.CancellationToken);
    }

    private static NoiseMonitorDto Monitor() =>
        new(
            Guid.NewGuid(),
            _now.UtcDateTime.AddYears(-1),
            lastDataTime: _now.UtcDateTime.AddHours(-6),
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

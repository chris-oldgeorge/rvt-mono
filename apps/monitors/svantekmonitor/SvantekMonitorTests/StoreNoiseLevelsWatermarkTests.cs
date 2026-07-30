using System.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;

namespace SvantekMonitorTests;

/// <summary>
/// A future-dated vendor timestamp persisted as <c>LastDataTime15Min</c> pushes
/// every subsequent request window past the wall clock, so the monitor is never
/// asked for data again. The watermark is therefore clamped to
/// <c>min(sampleTime, utcNow)</c> on the way to the database.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class StoreNoiseLevelsWatermarkTests
{
    private static readonly DateTime _utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(StoreNoiseLevelsWatermarkTests));

    [TestMethod]
    public async Task RunAsync_ClampsAFutureVendorTimestampToTheWallClock()
    {
        List<DateTime> writtenWatermarks = await RunHandlerAsync("2030-01-01 00:00:00");

        Assert.HasCount(1, writtenWatermarks);
        Assert.AreEqual(_utcNow, writtenWatermarks[0]);
    }

    [TestMethod]
    public async Task RunAsync_KeepsAPastVendorTimestampUnchanged()
    {
        List<DateTime> writtenWatermarks = await RunHandlerAsync("2025-12-31 23:59:00");

        Assert.HasCount(1, writtenWatermarks);
        Assert.AreEqual(new DateTime(2025, 12, 31, 23, 59, 0), writtenWatermarks[0]);
    }

    private async Task<List<DateTime>> RunHandlerAsync(string vendorTimestamp)
    {
        NoiseMonitorReadDto monitor = new(
            Guid.NewGuid(),
            "fleet-1",
            "1001",
            7,
            3,
            _utcNow,
            null,
            null,
            _utcNow.AddMinutes(-10),
            false,
            BatteryAlertType.Off,
            100);
        Mock<ISvantekMonitorQueries> monitorQueries = new(MockBehavior.Strict);
        monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([monitor]);
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""
                {
                  "status":"ok",
                  "data":[{
                    "point":3,
                    "data":{
                      "status":"ok",
                      "results":[{
                        "keys":[],
                        "data":[{
                          "timestamp":"{{vendorTimestamp}}",
                          "values":["12.5","13.5","14.5","15.5","16.5","17.5","18.5","19.5"]
                        }]
                      }]
                    }
                  }]
                }
                """);
        Mock<ISvantekMeasurementCommands> measurementCommands = new(MockBehavior.Strict);
        measurementCommands.Setup(commands => commands.InsertNoiseRecordsTableAsync(
                It.IsAny<DataTable>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        measurementCommands.Setup(commands => commands.Create8hourAverageAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        List<DateTime> writtenWatermarks = [];
        Mock<ISvantekMonitorCommands> monitorCommands = new(MockBehavior.Strict);
        monitorCommands.Setup(commands => commands.WriteLatestTimestampAsync(
                "1001",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, DateTime watermark, CancellationToken _) => writtenWatermarks.Add(watermark))
            .Returns(Task.CompletedTask);
        Mock<ISvantekRuleQueries> ruleQueries = new(MockBehavior.Strict);
        ruleQueries.Setup(queries => queries.ReadRules("1001")).Returns([]);
        Mock<ISvantekOperationalCommands> operational = new(MockBehavior.Strict);
        StoreNoiseLevelsHandler handler = new(
            new SvantekHttpGateway(http.Object, "key"),
            new SvantekMonitorReader(monitorQueries.Object, testLocal: false),
            ruleQueries.Object,
            monitorCommands.Object,
            measurementCommands.Object,
            operational.Object,
            new SvantekRuleProcessor(
                ruleQueries.Object,
                operational.Object,
                Mock.Of<IAlertIngressPort>()),
            new NoiseRequestWindowCalculator(new SvantekImportOptions()),
            new FixedTimeProvider(_utcNow));

        await handler.RunAsync(TestContext.CancellationToken);

        return writtenWatermarks;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    public TestContext TestContext { get; set; } = null!;
}

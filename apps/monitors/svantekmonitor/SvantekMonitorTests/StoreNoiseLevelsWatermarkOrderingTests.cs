using System.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;
using Svantek.Model.Dto;

namespace SvantekMonitorTests;

/// <summary>
/// The watermark used to be advanced before rules were evaluated, so a
/// rule-processing failure moved the start of the next run past samples that
/// were never evaluated - those alerts were lost permanently and silently.
/// The watermark is now written only after rule evaluation succeeds.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class StoreNoiseLevelsWatermarkOrderingTests
{
    private static readonly DateTime _utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(StoreNoiseLevelsWatermarkOrderingTests));

    [TestMethod]
    public async Task RunAsync_RuleEvaluationFailure_LeavesTheWatermarkUnadvanced()
    {
        List<DateTime> writtenWatermarks = [];
        Mock<ISvantekOperationalCommands> operational = new();

        await Assert.ThrowsAsync<SvantekJobAggregateException>(
            () => RunHandlerAsync(writtenWatermarks, operational, ruleEvaluationFails: true));

        // Samples were stored, but the next run must still start from the old
        // watermark so the unevaluated samples are re-read and re-alerted.
        Assert.IsEmpty(writtenWatermarks);
        operational.Verify(
            commands => commands.HandleException(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_RuleEvaluationSucceeds_AdvancesTheWatermark()
    {
        List<DateTime> writtenWatermarks = [];
        Mock<ISvantekOperationalCommands> operational = new();

        await RunHandlerAsync(writtenWatermarks, operational, ruleEvaluationFails: false);

        Assert.HasCount(1, writtenWatermarks);
        Assert.AreEqual(new DateTime(2025, 12, 31, 23, 59, 0), writtenWatermarks[0]);
    }

    private async Task RunHandlerAsync(
        List<DateTime> writtenWatermarks,
        Mock<ISvantekOperationalCommands> operational,
        bool ruleEvaluationFails)
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
            _utcNow.AddHours(-2),
            false,
            BatteryAlertType.Off,
            100);
        Mock<ISvantekMonitorQueries> monitorQueries = new();
        monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([monitor]);
        Mock<IHttpClient> http = new();
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "status":"ok",
                  "data":[{
                    "point":3,
                    "data":{
                      "status":"ok",
                      "results":[{
                        "keys":[],
                        "data":[{
                          "timestamp":"2025-12-31 23:59:00",
                          "values":["12.5","13.5","14.5","15.5","16.5","17.5","18.5","19.5"]
                        }]
                      }]
                    }
                  }]
                }
                """);
        Mock<ISvantekMeasurementCommands> measurementCommands = new();
        measurementCommands.Setup(commands => commands.InsertNoiseRecordsTableAsync(
                It.IsAny<DataTable>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        measurementCommands.Setup(commands => commands.Create8hourAverageAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<ISvantekMonitorCommands> monitorCommands = new();
        monitorCommands.Setup(commands => commands.WriteLatestTimestampAsync(
                "1001",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, DateTime watermark, CancellationToken _) => writtenWatermarks.Add(watermark))
            .Returns(Task.CompletedTask);
        Mock<ISvantekRuleQueries> ruleQueries = new();
        ruleQueries.Setup(queries => queries.ReadRules("1001")).Returns([Rule()]);
        Moq.Language.Flow.ISetup<ISvantekRuleQueries, double?> averageSetup = ruleQueries.Setup(queries => queries.GetAverageNoiseLevel(
            "1001",
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
    }

    private static RvtAlertRuleDto Rule() =>
        new(
            Guid.NewGuid(),
            "1001",
            "LAeq",
            limitOn: 70.0,
            limitOff: 60.0,
            averagingPeriod: 900,
            SvantekFixture.CreateActiveRuleActivity(null, null),
            AlertType.Alert,
            isActive: false,
            isDeleted: false,
            DateTime.UtcNow,
            null);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    public TestContext TestContext { get; set; } = null!;
}

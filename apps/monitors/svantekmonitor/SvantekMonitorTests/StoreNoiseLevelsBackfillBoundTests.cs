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

namespace SvantekMonitorTests;

/// <summary>
/// The vendor request is capped at MaximumInitialBackfill but the
/// rule-evaluation start was not: it came from LastDataTime ?? DeployedStart, so
/// a monitor deployed a year ago whose first sample arrives today drove roughly
/// 35,000 single-window aggregate queries for one 15-minute rule, inside the
/// per-project loop that blocks the rest of the fleet.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class StoreNoiseLevelsBackfillBoundTests
{
    private const string _serialId = "1001";

    private static readonly DateTime _utcNow = new(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(StoreNoiseLevelsBackfillBoundTests));

    [TestMethod]
    public async Task RunAsync_LongPastDeployment_BoundsAveragingAndRuleWindowsByTheBackfill()
    {
        NoiseMonitorReadDto monitor = new(
            Guid.NewGuid(),
            "fleet-1",
            _serialId,
            7,
            3,
            _utcNow,
            null,
            null,
            _utcNow.AddYears(-1),
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
                          "timestamp":"2026-01-07 23:45:00",
                          "values":["12.5","13.5","14.5","15.5","16.5","17.5","18.5","19.5"]
                        }]
                      }]
                    }
                  }]
                }
                """);
        Mock<ISvantekMeasurementCommands> measurementCommands = new();
        Mock<ISvantekMonitorCommands> monitorCommands = new();
        Mock<ISvantekRuleQueries> ruleQueries = new();
        ruleQueries.Setup(queries => queries.ReadRules(_serialId)).Returns([QuarterHourRule()]);
        Mock<ISvantekOperationalCommands> operational = new();
        StoreNoiseLevelsHandler handler = new(
            new SvantekHttpGateway(http.Object, "key"),
            new SvantekMonitorReader(monitorQueries.Object, testLocal: false),
            ruleQueries.Object,
            monitorCommands.Object,
            measurementCommands.Object,
            operational.Object,
            new SvantekRuleProcessor(ruleQueries.Object, operational.Object, Mock.Of<IAlertIngressPort>()),
            // One request window keeps the vendor call count out of the picture;
            // the seven-day cap is what the assertions are about.
            new NoiseRequestWindowCalculator(new SvantekImportOptions
            {
                MaximumInitialBackfill = TimeSpan.FromDays(7),
                MaximumRequestWindow = TimeSpan.FromDays(7)
            }),
            new FixedTimeProvider(_utcNow));

        await handler.RunAsync(TestContext.CancellationToken);

        // Seven days of eight-hour periods ending at or before the sample, and
        // seven days of quarter hours. Unclamped these were 1,095 and 35,040.
        measurementCommands.Verify(
            commands => commands.Create8hourAverageAsync(
                _serialId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(21));
        ruleQueries.Verify(
            queries => queries.GetAverageNoiseLevel(_serialId, "LAeq", It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Exactly(672));
    }

    private static RvtAlertRuleDto QuarterHourRule() =>
        new(
            Guid.NewGuid(),
            _serialId,
            "LAeq",
            limitOn: 70.0,
            limitOff: 60.0,
            averagingPeriod: 15 * 60,
            SvantekFixture.CreateActiveRuleActivity(null, null),
            AlertType.Alert,
            isActive: false,
            isDeleted: false,
            _utcNow.AddYears(-1),
            null);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    public TestContext TestContext { get; set; } = null!;
}

using System.Data;
using System.Globalization;
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

[TestClass]
[DoNotParallelize]
public sealed class StoreNoiseLevelsParsingTests
{
    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(StoreNoiseLevelsParsingTests));

    [TestMethod]
    public async Task RunAsync_ParsesSampleTimestampAndLevelsInvariantOfCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            DateTime expectedSampleTime = new(2025, 12, 31, 23, 59, 0);

            DataTable writtenTable = await RunHandlerAsync("""
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
                """, expectedSampleTime);

            Assert.HasCount(1, writtenTable.Rows);
            Assert.AreEqual(expectedSampleTime, writtenTable.Rows[0].Field<DateTime>("SampleTime"));
            Assert.AreEqual(12.5, writtenTable.Rows[0].Field<double>("LAeq"));
            Assert.AreEqual(13.5, writtenTable.Rows[0].Field<double>("LAmax"));
            Assert.AreEqual(19.5, writtenTable.Rows[0].Field<double>("LC10"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestMethod]
    public async Task RunAsync_StoresUnparseableLevelsAsDbNull_NeverAsZero()
    {
        DateTime expectedSampleTime = new(2025, 12, 31, 23, 59, 0);

        // "-.-" and "" are real vendor artefacts; "12,5" is not invariant and
        // must not silently parse to a wrong magnitude either.
        DataTable writtenTable = await RunHandlerAsync("""
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
                      "values":["-.-","","garbage","12,5","16.5","17.5","18.5","19.5"]
                    }]
                  }]
                }
              }]
            }
            """, expectedSampleTime);

        Assert.HasCount(1, writtenTable.Rows);
        DataRow row = writtenTable.Rows[0];
        Assert.AreEqual(DBNull.Value, row["LAeq"]);
        Assert.AreEqual(DBNull.Value, row["LAmax"]);
        Assert.AreEqual(DBNull.Value, row["LA90"]);
        Assert.AreEqual(DBNull.Value, row["LA10"]);
        Assert.AreEqual(16.5, row.Field<double>("LCeq"));
        Assert.AreEqual(17.5, row.Field<double>("LCmax"));
        Assert.AreEqual(18.5, row.Field<double>("LC90"));
        Assert.AreEqual(19.5, row.Field<double>("LC10"));
    }

    private async Task<DataTable> RunHandlerAsync(string vendorJson, DateTime expectedSampleTime)
    {
        DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        NoiseMonitorReadDto monitor = new(
            Guid.NewGuid(),
            "fleet-1",
            "1001",
            7,
            3,
            utcNow,
            null,
            null,
            utcNow.AddMinutes(-10),
            false,
            SvantekApi.BatteryAlertType.Off,
            100);
        Mock<ISvantekMonitorQueries> monitorQueries = new(MockBehavior.Strict);
        monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([monitor]);
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendorJson);
        DataTable? writtenTable = null;
        Mock<ISvantekMeasurementCommands> measurementCommands = new(MockBehavior.Strict);
        measurementCommands.Setup(commands => commands.InsertNoiseRecordsTableAsync(
                It.IsAny<DataTable>(),
                It.IsAny<CancellationToken>()))
            .Callback((DataTable table, CancellationToken _) => writtenTable = table)
            .Returns(Task.CompletedTask);
        Mock<ISvantekMonitorCommands> monitorCommands = new(MockBehavior.Strict);
        monitorCommands.Setup(commands => commands.WriteLatestTimestampAsync(
                "1001",
                expectedSampleTime,
                It.IsAny<CancellationToken>()))
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
            new FixedTimeProvider(utcNow));

        await handler.RunAsync(TestContext.CancellationToken);

        Assert.IsNotNull(writtenTable);
        monitorQueries.VerifyAll();
        http.VerifyAll();
        measurementCommands.VerifyAll();
        monitorCommands.VerifyAll();
        ruleQueries.VerifyAll();
        operational.VerifyAll();
        return writtenTable;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    public TestContext TestContext { get; set; } = null!;
}

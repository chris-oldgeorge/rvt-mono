using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;
using SvantekMonitor.model.dto;

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
    public async Task RunAsync_ParsesSampleTimestampAndLevelsUsingCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var utcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var expectedSampleTime = new DateTime(2025, 12, 31, 23, 59, 0);
            var monitor = new NoiseMonitorReadDto(
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
            var monitorQueries = new Mock<ISvantekMonitorQueries>(MockBehavior.Strict);
            monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, CancellationToken.None))
                .ReturnsAsync([monitor]);
            var http = new Mock<IHttpClient>(MockBehavior.Strict);
            http.Setup(client => client.PostAsync(
                    "projects-get-result-data-multi-point.php",
                    It.IsAny<HttpContent>(),
                    CancellationToken.None))
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
                              "timestamp":"31/12/2025 23:59:00",
                              "values":["12,5","13,5","14,5","15,5","16,5","17,5","18,5","19,5"]
                            }]
                          }]
                        }
                      }]
                    }
                    """);
            DataTable? writtenTable = null;
            var measurementCommands = new Mock<ISvantekMeasurementCommands>(MockBehavior.Strict);
            measurementCommands.Setup(commands => commands.InsertNoiseRecordsTableAsync(
                    It.IsAny<DataTable>(),
                    CancellationToken.None))
                .Callback((DataTable table, CancellationToken _) => writtenTable = table)
                .Returns(Task.CompletedTask);
            var monitorCommands = new Mock<ISvantekMonitorCommands>(MockBehavior.Strict);
            monitorCommands.Setup(commands => commands.WriteLatestTimestampAsync(
                    "1001",
                    expectedSampleTime,
                    CancellationToken.None))
                .Returns(Task.CompletedTask);
            var ruleQueries = new Mock<ISvantekRuleQueries>(MockBehavior.Strict);
            ruleQueries.Setup(queries => queries.ReadRules("1001")).Returns([]);
            var operational = new Mock<ISvantekOperationalCommands>(MockBehavior.Strict);
            var handler = new StoreNoiseLevelsHandler(
                new SvantekHttpGateway(http.Object, "key"),
                new SvantekMonitorReader(monitorQueries.Object, testLocal: false),
                ruleQueries.Object,
                monitorCommands.Object,
                measurementCommands.Object,
                operational.Object,
                new SvantekRuleProcessor(
                    ruleQueries.Object,
                    operational.Object,
                    Mock.Of<IMessageService>(),
                    Mock.Of<IMonitorEventPublisher>()),
                new NoiseRequestWindowCalculator(new SvantekImportOptions()),
                new FixedTimeProvider(utcNow));

            await handler.RunAsync();

            Assert.IsNotNull(writtenTable);
            Assert.HasCount(1, writtenTable.Rows);
            Assert.AreEqual(expectedSampleTime, writtenTable.Rows[0].Field<DateTime>("SampleTime"));
            Assert.AreEqual(12.5, writtenTable.Rows[0].Field<double>("LAeq"));
            monitorQueries.VerifyAll();
            http.VerifyAll();
            measurementCommands.VerifyAll();
            monitorCommands.VerifyAll();
            ruleQueries.VerifyAll();
            operational.VerifyAll();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

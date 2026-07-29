using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using SvantekMonitor.model.dto;
namespace SvantekMonitorTests;

// Summary: Facade-level StoreNoiseLevels tests: samples are persisted and rule breaches
// emit exactly one durable AlertSignal per breach through IAlertIngressPort.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: rewritten from the retired synchronous
//   StoreNoiseLevels(string,string) / for-date entry points onto the async multi-point
//   vendor pipeline and the durable alert contract.
[TestClass]
public class TestSvantekApiNoiseLevels
{
    public TestSvantekApiNoiseLevels()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        RvtLogger.CreateLogger(factory, "TestSvantekApiNoiseLevels");
    }

    private static string MultiPointResponse(int pointId, DateTime sampleTime, int level)
    {
        string timestamp = sampleTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        string value = level.ToString(CultureInfo.InvariantCulture);
        string values = string.Join(",", Enumerable.Repeat($"\"{value}\"", 8));
        return $$$"""
            {"status":"ok","data":[{"point":{{{pointId}}},"data":{"status":"ok","results":[{"keys":[],"data":[{"timestamp":"{{{timestamp}}}","values":[{{{values}}}]}]}]}}]}
            """;
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_EmptyRules_StoresSamplesWithoutSignals()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        DateTime sampleTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddMinutes(-5));
        NoiseMonitorReadDto monitor = SvantekFixture.ReadMonitorDto(
            "Device1",
            pointId: 3,
            lastDataTime: DateTime.UtcNow.AddMinutes(-40));
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
                ReturnsAsync([monitor]);
        dbClient.Setup(c => c.ReadRules("Device1")).
            Returns([]);
        httpClient.Setup(c => c.PostAsync("projects-get-result-data-multi-point.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).
                ReturnsAsync(MultiPointResponse(3, sampleTime, 55));

        await testObj.StoreNoiseLevelsAsync(TestContext.CancellationToken);

        httpClient.Verify(c => c.PostAsync("projects-get-result-data-multi-point.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.InsertNoiseRecordsTableAsync(
            It.Is<DataTable>(table => table.Rows.Count == 1), It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.WriteLatestTimestampAsync("Device1", sampleTime, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.ReadRules("Device1"), Times.Exactly(1));
        dbClient.Verify(c => c.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()), Times.Never);

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_RuleBreach_EmitsOneDurableSignalAndLatchesRule()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient, out Mock<IAlertIngressPort> messageClient);

        DateTime sampleTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddMinutes(-5));
        NoiseMonitorReadDto monitor = SvantekFixture.ReadMonitorDto(
            "Device1",
            pointId: 3,
            lastDataTime: DateTime.UtcNow.AddMinutes(-40));
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
                ReturnsAsync([monitor]);

        int durationSeconds = 15 * 60;
        double limitOn = 10.0;
        RvtAlertRuleDto rule = new(Guid.NewGuid(), "Device1", "LAeq", limitOn, 1.0, durationSeconds,
                                   SvantekFixture.CreateActiveRuleActivity(null, null),
                                   AlertType.Alert, false, false, DateTime.UtcNow, null);
        dbClient.Setup(c => c.ReadRules("Device1")).
            Returns([rule]);
        dbClient.Setup(c => c.GetAverageNoiseLevel("Device1", "LAeq", It.IsAny<DateTime>(), It.IsAny<DateTime>())).
            Returns(limitOn + 1);
        httpClient.Setup(c => c.PostAsync("projects-get-result-data-multi-point.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).
                ReturnsAsync(MultiPointResponse(3, sampleTime, 55));

        await testObj.StoreNoiseLevelsAsync(TestContext.CancellationToken);

        httpClient.Verify(c => c.PostAsync("projects-get-result-data-multi-point.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.WriteLatestTimestampAsync("Device1", sampleTime, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.ReadRules("Device1"), Times.Exactly(1));

        // One breach, one durable signal; the rule latch keeps later periods quiet and the
        // durable stack owns notification writes, contact fan-out, and MQTT/email/SMS sends.
        messageClient.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.Source == "svantek.rules" &&
                signal.SerialId == "Device1" &&
                signal.AlertType == AlertType.Alert &&
                signal.Field == "LAeq" &&
                signal.Level == limitOn + 1 &&
                signal.Limit == limitOn &&
                signal.AveragingPeriod == durationSeconds &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                signal.SuppressionWindow == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        messageClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, "Device1", "LAeq", true))),
            Times.Exactly(1));

        mqttClient.VerifyNoOtherCalls();
    }

    public TestContext TestContext { get; set; } = null!;
}

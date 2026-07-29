using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
namespace SvantekMonitorTests;

// Summary: Facade-level failure-semantics tests: setup failures escape, per-unit failures
// are recorded via HandleException and re-thrown as SvantekJobAggregateException.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: rewritten from the swallow-and-HandleException
//   suite of the retired synchronous entry points (StoreNoiseLevels(string,string),
//   StoreAllNoiseLevelsForYesterday, StoreNoiseLevelsForDate).
[TestClass]
public class TestSvantekApiException
{
    public TestSvantekApiException()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        RvtLogger.CreateLogger(factory, "TestSvantekApiException");
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_MonitorReadFailure_EscapesAsAdapterException()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);

        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
                ThrowsAsync(new IOException());

        AdapterException exception =
            await Assert.ThrowsExactlyAsync<AdapterException>(() => testObj.StoreNoiseLevelsAsync(TestContext.CancellationToken));

        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.HandleException(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        dbClient.VerifyNoOtherCalls();

        httpClient.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreNoiseLevels_VendorFailure_RecordsProjectAndThrowsAggregate()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);

        List<NoiseMonitorReadDto> monitors = SvantekFixture.ReadMonitorDtos(DateTime.UtcNow.AddMinutes(-40));
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
                ReturnsAsync(monitors);
        httpClient.Setup(c => c.PostAsync("projects-get-result-data-multi-point.php", It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).
                ThrowsAsync(new IOException());

        SvantekJobAggregateException aggregate =
            await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(() => testObj.StoreNoiseLevelsAsync(TestContext.CancellationToken));

        Assert.AreEqual("StoreNoiseLevels", aggregate.JobName);
        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual("StoreNoiseLevels project 7", aggregate.Failures[0].Message);
        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.HandleException("StoreNoiseLevels project 7", It.IsAny<Exception>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestCheckForOfflineMonitors_CommandFailure_ContinuesSignalingAndThrowsAggregate()
    {
        SvantekApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                                 out Mock<IDBClient> dbClient,
                                                 out Mock<IMqttClient> mqttClient,
                                                 out Mock<IAlertIngressPort> messageClient);

        List<RvtAlertRuleDto> rules = SvantekFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
        List<NoiseMonitorReadDto> monitors = SvantekFixture.ReadMonitorDtos(DateTime.UtcNow.AddHours(-25));
        dbClient.Setup(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>())).
                ReturnsAsync(monitors);
        dbClient.Setup(c => c.SetMonitorOfflineAsync(monitors[0].Id, true, It.IsAny<CancellationToken>())).
                ThrowsAsync(new IOException());

        SvantekJobAggregateException aggregate =
            await Assert.ThrowsExactlyAsync<SvantekJobAggregateException>(() => testObj.CheckForOfflineMonitorsAsync(TestContext.CancellationToken));

        Assert.AreEqual("CheckForOfflineMonitors", aggregate.JobName);
        Assert.HasCount(1, aggregate.Failures);
        Assert.AreEqual("CheckForOfflineMonitors monitor Device1", aggregate.Failures[0].Message);
        Assert.IsInstanceOfType<IOException>(aggregate.Failures[0].InnerException);

        // The independent monitors keep signaling and latching after the first failure.
        foreach (NoiseMonitorReadDto m in monitors)
        {
            messageClient.Verify(c => c.AcceptAsync(
                It.Is<AlertSignal>(signal =>
                    signal.SerialId == m.SerialId &&
                    signal.AlertType == AlertType.Offline),
                It.IsAny<CancellationToken>()), Times.Exactly(1));
            dbClient.Verify(c => c.SetMonitorOfflineAsync(m.Id, true, It.IsAny<CancellationToken>()), Times.Exactly(1));
        }
        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorListAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(1));
        dbClient.Verify(c => c.HandleException("CheckForOfflineMonitors monitor Device1", It.IsAny<Exception>()), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        httpClient.VerifyNoOtherCalls();
        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    public TestContext TestContext { get; set; } = null!;
}

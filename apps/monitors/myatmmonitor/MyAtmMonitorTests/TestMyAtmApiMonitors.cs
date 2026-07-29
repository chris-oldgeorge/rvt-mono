// File summary: Tests MyAtm monitor discovery, accessory ingestion, and offline monitor notification flows.
// Major updates:
// - 2026-06-18: Realigned monitor discovery expectations with paged MyAtmosphere device listing.
using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests;


[TestClass]
public class TestMyAtmApiDevices
{

    public TestMyAtmApiDevices()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        RvtLogger.CreateLogger(factory, "TestMyAtmApiDevices");
    }

    private static Rvt.Monitor.Common.Notifications.RvtContactDto ContactEquivalentTo(RvtContactDto expected) =>
        It.Is<Rvt.Monitor.Common.Notifications.RvtContactDto>(actual =>
            actual.ContactMethod == (Rvt.Monitor.Common.Notifications.ContactMethod)(int)expected.ContactMethod &&
            actual.EmailAddress == expected.EmailAddress &&
            actual.PhoneNumber == expected.PhoneNumber &&
            actual.Email == expected.Email &&
            actual.SMS == expected.SMS &&
            actual.SendStartTime == expected.SendStartTime &&
            actual.SendEndTime == expected.SendEndTime);

    [TestMethod]
    public async Task TestStoreMonitors_Success()
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                            out Mock<IDBClient> dbClient,
                                            out Mock<IMqttClient> mqttClient,
                                            out Mock<INotificationDeliveryService> messageClient);

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=100", TestContext.CancellationToken)).
                Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.DevicesResponseJson(), TestContext.CancellationToken));

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/11111", TestContext.CancellationToken)).
            Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.DeviceInfoResponseJson("11111"), TestContext.CancellationToken));

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/22222", TestContext.CancellationToken)).
            Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.DeviceInfoResponseJson("22222"), TestContext.CancellationToken));

        await testObj.StoreMonitorsAsync(123, TestContext.CancellationToken);

        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=100", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices/11111", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices/22222", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.WriteMonitorList(
                        It.Is<List<DustMonitorDto>>(
                            l => TestUtil.AreEqual(MyAtmFixture.CustomerDeviceDtos(It.IsAny<DateTime?>(), false), l))), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestStoreMonitors_UsesConfiguredPageSizeForPaging()
    {
        Mock<IHttpClient> httpClient = new();
        Mock<IDBClient> dbClient = new();
        Mock<IMqttClient> mqttClient = new();
        Mock<INotificationDeliveryService> messageClient = new();
        MyAtmMonitorOptions options = new()
        {
            CustomerId = 123,
            DevicePageSize = 2,
            PortalBaseUrl = "https://portal.example/"
        };
        MyAtmApi testObj = new(httpClient.Object, dbClient.Object, mqttClient.Object, messageClient.Object, false, options);

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=2", TestContext.CancellationToken))
            .ReturnsAsync(MyAtmFixture.DevicesResponseJson());
        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices?$skip=2&$top=2", TestContext.CancellationToken))
            .ReturnsAsync("""
                [
                  {
                    "serialNumber": "33333",
                    "model": "AQ Guard",
                    "displayName": "Dust - Test - 33333",
                    "sharedPublicly": false,
                    "includeInMonitoring": true,
                    "currentLocation": {
                      "id": 3,
                      "deviceSerialNumber": "33333",
                      "customerId": 123,
                      "latitude": 12.345678,
                      "longitude": 87.654321,
                      "address": "3 The Street",
                      "timeZone": "Europe/London",
                      "effectiveSince": "2023-09-06T12:19:44.682+00:00",
                      "effectiveTill": null,
                      "current": true
                    },
                    "currentCustomerAssignment": {
                      "customerDisplayName": "RVT-Test",
                      "customerId": 123,
                      "effectiveSince": "2023-09-06T12:19:30.496431+00:00"
                    }
                  }
                ]
                """);
        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/11111", TestContext.CancellationToken))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("11111"));
        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/22222", TestContext.CancellationToken))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("22222"));
        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/33333", TestContext.CancellationToken))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("33333"));

        await testObj.StoreMonitorsAsync(123, TestContext.CancellationToken);

        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=2", TestContext.CancellationToken), Times.Once);
        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices?$skip=2&$top=2", TestContext.CancellationToken), Times.Once);
        dbClient.Verify(c => c.WriteMonitorList(It.IsAny<List<DustMonitorDto>>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task TestStoreMonitors_TestLocal_WritesOnlyDemoDustMonitor()
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient,
                                            out Mock<IDBClient> dbClient,
                                            out Mock<IMqttClient> mqttClient,
                                            out Mock<INotificationDeliveryService> messageClient,
                                            testLocal: true);

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=100", TestContext.CancellationToken)).
                Returns(Task<string>.Factory.StartNew(() => TestLocalDevicesJson(), TestContext.CancellationToken));

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/21972", TestContext.CancellationToken)).
            Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.DeviceInfoResponseJson("21972"), TestContext.CancellationToken));

        httpClient.Setup(c => c.GetAsync("/api/customers/123/devices/99999", TestContext.CancellationToken)).
            Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.DeviceInfoResponseJson("99999"), TestContext.CancellationToken));

        await testObj.StoreMonitorsAsync(123, TestContext.CancellationToken);

        dbClient.Verify(c => c.WriteMonitorList(It.Is<List<DustMonitorDto>>(
            monitors => monitors.Count == 1 && monitors[0].SerialId == "21972")), Times.Exactly(1));
        dbClient.VerifyNoOtherCalls();

        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices?$skip=0&$top=100", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices/21972", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.Verify(c => c.GetAsync("/api/customers/123/devices/99999", TestContext.CancellationToken), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    private static string TestLocalDevicesJson() =>
        """
        [
          {
            "serialNumber": "99999",
            "model": "AQ Guard",
            "displayName": "Dust - Other - 99999",
            "sharedPublicly": false,
            "includeInMonitoring": false,
            "currentLocation": {
              "id": 1,
              "deviceSerialNumber": "99999",
              "customerId": 123,
              "latitude": 12.345678,
              "longitude": 87.654321,
              "address": "1 The Street",
              "timeZone": "Europe/London",
              "effectiveSince": "2023-09-06T12:19:44.682+00:00",
              "effectiveTill": null,
              "current": true
            },
            "currentCustomerAssignment": {
              "customerDisplayName": "RVT-Test",
              "customerId": 123,
              "effectiveSince": "2023-09-06T12:19:30.496431+00:00"
            }
          },
          {
            "serialNumber": "21972",
            "model": "AQ Guard",
            "displayName": "Dust - R6025V - 21972",
            "sharedPublicly": false,
            "includeInMonitoring": true,
            "currentLocation": {
              "id": 2,
              "deviceSerialNumber": "21972",
              "customerId": 123,
              "latitude": 12.345678,
              "longitude": 87.654321,
              "address": "2 The Street",
              "timeZone": "Europe/London",
              "effectiveSince": "2023-09-06T12:19:44.682+00:00",
              "effectiveTill": null,
              "current": true
            },
            "currentCustomerAssignment": {
              "customerDisplayName": "RVT-Test",
              "customerId": 123,
              "effectiveSince": "2023-09-06T12:19:30.496431+00:00"
            }
          }
        ]
        """;

    [TestMethod]
    public async Task TestStoreAccessoryInfo_Success()
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                     out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

        int customerId = 656;
        httpClient.Setup(c => c.GetAsync(It.IsRegex("\\/api\\/customers\\/" + customerId + "\\/devices\\/.*\\/measurements/accessory"), TestContext.CancellationToken)).
                             Returns(Task<string>.Factory.StartNew(() => MyAtmFixture.AccessoryResponseJson(), TestContext.CancellationToken));

        dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), null)).
                Returns(MyAtmFixture.CustomerDeviceDtos(null));
        dbClient.Setup(c => c.ReadLatestAccessoryTimestamp(It.IsAny<string>())).Returns((DateTime?)null);
        dbClient.Setup(c => c.InsertAccessoryPageAsync(
                It.Is<IReadOnlyList<AccessoryInfoDto>>(page => page.Count == 50),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await testObj.StoreAccessoryInfoAsync(customerId, TestContext.CancellationToken);

        httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.AccessoryPageRequestPattern(656, "11111")), TestContext.CancellationToken), Times.Exactly(1));
        httpClient.Verify(c => c.GetAsync(It.IsRegex(TestUtil.AccessoryPageRequestPattern(656, "22222")), TestContext.CancellationToken), Times.Exactly(1));
        httpClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadLatestAccessoryTimestamp("11111"), Times.Exactly(1));
        dbClient.Verify(c => c.ReadLatestAccessoryTimestamp("22222"), Times.Exactly(1));
        dbClient.Verify(c => c.InsertAccessoryPageAsync(It.IsAny<IReadOnlyList<AccessoryInfoDto>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        dbClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();

    }

    [TestMethod]
    public async Task TestCheckForOfflineMonitors_MonitorsOfflineFor23Hours_Success()
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

        int customerId = 765;
        List<RvtAlertRuleDto> rules = MyAtmFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);
        dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>())).
            Returns([]);

        await testObj.CheckForOfflineMonitorsAsync(customerId, TestContext.CancellationToken);

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Exactly(1));

        dbClient.VerifyNoOtherCalls();

        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    [DataRow(25 * 60, 3600)]
    [DataRow(24 * 60, 0)]
    [DataRow((24 * 60) + 1, 60)]
    [TestMethod]
    public async Task TestCheckForOfflineMonitors_NotificationWrittenOk_Success(int minutesOffline, int offlineForSeconds)
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

        int customerId = 765;
        List<RvtAlertRuleDto> rules = MyAtmFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

        List<DustMonitorDto> monitors = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow.AddMinutes(-minutesOffline));
        dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>())).
            Returns(monitors);
        dbClient.Setup(c => c.ReadSiteSchedule(It.IsAny<Guid>())).Returns(AlwaysOpenSiteSchedule());
        List<RvtContactDto> contacts = MyAtmFixture.AlertContacts();
        dbClient.Setup(c => c.ReadAlertContacts(It.IsAny<Guid>())).Returns(contacts);
        List<MyAtmAlertCommit> commits = [];
        dbClient.Setup(c => c.CommitAlertAsync(It.IsAny<MyAtmAlertCommit>(), It.IsAny<CancellationToken>()))
            .Callback<MyAtmAlertCommit, CancellationToken>((commit, _) => commits.Add(commit))
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));

        await testObj.CheckForOfflineMonitorsAsync(customerId, TestContext.CancellationToken);

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Exactly(1));

        Assert.HasCount(monitors.Count, commits);
        Assert.IsTrue(commits.All(commit =>
            commit.MonitorStateMutation!.ExpectedOffline == false &&
            commit.MonitorStateMutation.Offline == true &&
            commit.Occurrences.Single().AlertType == AlertType.Offline &&
            commit.Occurrences.Single().Level == offlineForSeconds &&
            commit.Occurrences.Single().DeliveryPlan!.Deliveries.All(
                delivery => delivery.Kind != MonitorDeliveryKind.MqttAlert)));
        dbClient.Verify(c => c.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
        dbClient.Verify(c => c.SetMonitorOffline(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        dbClient.Verify(c => c.ClaimNextDueAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    private static MyAtmSiteSchedule AlwaysOpenSiteSchedule() => new()
    {
        WeekdayStart = TimeSpan.Zero,
        WeekdayEnd = TimeSpan.FromHours(24),
        SaturdayStart = TimeSpan.Zero,
        SaturdayEnd = TimeSpan.FromHours(24),
        SundayStart = TimeSpan.Zero,
        SundayEnd = TimeSpan.FromHours(24)
    };

    [TestMethod]
    public async Task TestCheckForOfflineMonitors_OfflineMonitorWithRecentData_MarkedOnline()
    {
        MyAtmApi testObj = TestUtil.CreateApiAndMocks(out Mock<IHttpClient> httpClient, out Mock<IDBClient> dbClient,
                                 out Mock<IMqttClient> mqttClient, out Mock<INotificationDeliveryService> messageClient);

        int customerId = 765;
        List<RvtAlertRuleDto> rules = MyAtmFixture.OfflineRules();
        dbClient.Setup(c => c.ReadRules(null)).Returns(rules);

        // Monitors flagged offline in the DB but with fresh data - the check should mark them back online.
        List<DustMonitorDto> monitors = MyAtmFixture.CustomerDeviceDtos(DateTime.UtcNow);
        foreach (DustMonitorDto monitor in monitors)
        {
            monitor.Offline = true;
        }
        dbClient.Setup(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(monitors);

        await testObj.CheckForOfflineMonitorsAsync(customerId, TestContext.CancellationToken);

        httpClient.VerifyNoOtherCalls();

        dbClient.Verify(c => c.ReadRules(null), Times.Exactly(1));
        dbClient.Verify(c => c.ReadMonitorList(It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Exactly(1));

        foreach (DustMonitorDto monitor in monitors)
        {
            Assert.IsFalse(monitor.Offline);
        }
        dbClient.Verify(c => c.CommitAlertAsync(It.Is<MyAtmAlertCommit>(commit =>
            commit.MonitorStateMutation!.ExpectedOffline == true &&
            commit.MonitorStateMutation.Offline == false &&
            commit.Occurrences.Count == 0), It.IsAny<CancellationToken>()), Times.Exactly(monitors.Count));
        dbClient.Verify(c => c.SetMonitorOffline(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        dbClient.Verify(c => c.WriteNotification(It.IsAny<NotificationDto>()), Times.Never);
        dbClient.Verify(c => c.ClaimNextDueAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Coming back online must not raise notifications or messages.
        mqttClient.VerifyNoOtherCalls();
        messageClient.VerifyNoOtherCalls();
    }

    public TestContext TestContext { get; set; }
}

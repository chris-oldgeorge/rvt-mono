using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.UseCases;
using MyAtm.Model.Dto;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class StoreMonitorsHandlerTests
{
    [TestMethod]
    public async Task RunAsync_DeviceDetailFails_PersistsSuccessfulDeviceAndContinuesToNextPage()
    {
        Mock<IHttpClient> http = new Mock<IHttpClient>(MockBehavior.Strict);
        Mock<IMyAtmMonitorCommands> monitorCommands = new Mock<IMyAtmMonitorCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices?$skip=0&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyAtmFixture.DevicesResponseJson());
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices/11111",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("detail unavailable"));
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices/22222",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("22222"));
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices?$skip=2&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");
        operational.Setup(commands => commands.HandleException(
            "StoreMonitors serialId=11111",
            It.IsAny<Exception>()));
        monitorCommands.Setup(commands => commands.WriteMonitorList(
            It.Is<List<DustMonitorDto>>(monitors =>
                monitors.Count == 1 && monitors[0].SerialId == "22222")));
        StoreMonitorsHandler handler = CreateHandler(http, monitorCommands, operational, maxPages: 5);

        MyAtmJobAggregateException exception = await Assert.ThrowsAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync(123));

        Assert.HasCount(1, exception.Failures);
        Assert.AreEqual("StoreMonitors serialId=11111", exception.Failures[0].Identifier);
        http.VerifyAll();
        monitorCommands.VerifyAll();
        operational.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_RepeatedFullPage_StopsWithoutFetchingDetailsTwice()
    {
        Mock<IHttpClient> http = new Mock<IHttpClient>(MockBehavior.Strict);
        Mock<IMyAtmMonitorCommands> monitorCommands = new Mock<IMyAtmMonitorCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        string page = MyAtmFixture.DevicesResponseJson();
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices?$skip=0&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices?$skip=2&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices/11111",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("11111"));
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices/22222",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson("22222"));
        monitorCommands.Setup(commands => commands.WriteMonitorList(
            It.Is<List<DustMonitorDto>>(monitors => monitors.Count == 2)));
        operational.Setup(commands => commands.HandleException(
            "StoreMonitors page=2",
            It.Is<InvalidOperationException>(exception =>
                exception.Message.Contains("repeated", StringComparison.OrdinalIgnoreCase))));
        StoreMonitorsHandler handler = CreateHandler(http, monitorCommands, operational, maxPages: 5);

        MyAtmJobAggregateException exception = await Assert.ThrowsAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync(123));

        Assert.HasCount(1, exception.Failures);
        http.Verify(client => client.GetAsync(
            "/api/customers/123/devices/11111",
            It.IsAny<CancellationToken>()), Times.Once);
        http.Verify(client => client.GetAsync(
            "/api/customers/123/devices/22222",
            It.IsAny<CancellationToken>()), Times.Once);
        http.VerifyAll();
        monitorCommands.VerifyAll();
        operational.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_LastAllowedPageIsFull_FailsAsIncompleteAfterPersistingPage()
    {
        Mock<IHttpClient> http = new Mock<IHttpClient>(MockBehavior.Strict);
        Mock<IMyAtmMonitorCommands> monitorCommands = new Mock<IMyAtmMonitorCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        http.Setup(client => client.GetAsync(
                "/api/customers/123/devices?$skip=0&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyAtmFixture.DevicesResponseJson());
        foreach (string? serialId in new[] { "11111", "22222" })
        {
            http.Setup(client => client.GetAsync(
                    $"/api/customers/123/devices/{serialId}",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MyAtmFixture.DeviceInfoResponseJson(serialId));
        }
        monitorCommands.Setup(commands => commands.WriteMonitorList(
            It.Is<List<DustMonitorDto>>(monitors => monitors.Count == 2)));
        operational.Setup(commands => commands.HandleException(
            "StoreMonitors page=1",
            It.Is<InvalidOperationException>(exception =>
                exception.Message.Contains("page limit", StringComparison.OrdinalIgnoreCase))));
        StoreMonitorsHandler handler = CreateHandler(http, monitorCommands, operational, maxPages: 1);

        MyAtmJobAggregateException exception = await Assert.ThrowsAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync(123));

        Assert.HasCount(1, exception.Failures);
        http.VerifyAll();
        monitorCommands.VerifyAll();
        operational.VerifyAll();
    }

    private static MyAtm.Api.UseCases.StoreMonitorsHandler CreateHandler(
        Mock<IHttpClient> http,
        Mock<IMyAtmMonitorCommands> monitorCommands,
        Mock<IMyAtmOperationalCommands> operational,
        int maxPages)
    {
        MyAtmHttpGateway gateway = new MyAtmHttpGateway(
            http.Object,
            devicePageSize: 2,
            measurementPageSize: 2,
            accessoryPageSize: 2);
        return new MyAtm.Api.UseCases.StoreMonitorsHandler(
            gateway,
            monitorCommands.Object,
            operational.Object,
            testLocal: false,
            devicePageSize: 2,
            maxDevicePagesPerRun: maxPages);
    }
}

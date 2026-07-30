using System.Text.Json;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Http;
using MyAtm.Api.Ports;
using MyAtm.Model;
using MyAtm.Model.Json;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmHttpGatewayTests
{
    [TestMethod]
    public void MeasurementPage_IsOwnedByPortsNamespace()
    {
        Assert.AreEqual("MyAtm.Api.Ports", typeof(MyAtmMeasurementPage<>).Namespace);
    }

    [TestMethod]
    public async Task HttpGetDeviceMeasurementPageAsync_UnspecifiedDatabaseAndVendorTimes_PreserveTicksAsUtc()
    {
        DateTime cursor = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Unspecified);
        DateTime sampleTime = cursor.AddMinutes(1);
        DeviceMeasurement measurement = MyAtmFixture.CreateDeviceMeasurement(sampleTime, 1, 2, 3);
        measurement.Timestamp = DateTime.SpecifyKind(measurement.Timestamp, DateTimeKind.Unspecified);
        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(
                "/api/customers/9/devices/11111/measurements?$select=avrg,timestamp,pm1,pm2_5,pm10,pm_total,weather_t,weather_p,weather_rh&$filter=timestamp gt 2026-07-16T08:00:00.0000000Z&$orderby=timestamp asc&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new[] { measurement }));
        MyAtmHttpGateway gateway = new(
            httpClient.Object,
            devicePageSize: 100,
            measurementPageSize: 2,
            accessoryPageSize: 2);

        MyAtmMeasurementPage<DeviceMeasurement> page = await gateway.HttpGetDeviceMeasurementPageAsync<DeviceMeasurement>(
            9,
            "11111",
            cursor,
            Period.Minutes1, TestContext.CancellationToken);

        Assert.AreEqual(sampleTime.Ticks, page.Measurements.Single().Timestamp.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, page.Measurements.Single().Timestamp.Kind);
        Assert.AreEqual(sampleTime.Ticks, page.NextCursor!.Value.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, page.NextCursor.Value.Kind);
        httpClient.VerifyAll();
    }

    [TestMethod]
    public async Task HttpGetDeviceMeasurementPageAsync_UsesKeysetPagingAndNormalizesAFullPage()
    {
        const int customerId = 9;
        const string serialId = "11111";
        DateTime cursor = new(2023, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        DeviceMeasurement newer = MyAtmFixture.CreateDeviceMeasurement(cursor.AddMinutes(2), 2, 2, 2);
        DeviceMeasurement older = MyAtmFixture.CreateDeviceMeasurement(cursor.AddMinutes(1), 1, 1, 1);
        DeviceMeasurement duplicate = MyAtmFixture.CreateDeviceMeasurement(cursor.AddMinutes(2), 3, 3, 3);

        Mock<IHttpClient> httpClient = new(MockBehavior.Strict);
        httpClient
            .Setup(client => client.GetAsync(
                "/api/customers/9/devices/11111/measurements?$select=avrg,timestamp,pm1,pm2_5,pm10,pm_total,weather_t,weather_p,weather_rh&$filter=timestamp gt 2023-09-25T10:00:00.0000000Z&$orderby=timestamp asc&$top=2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new[] { newer, older, duplicate }));

        MyAtmHttpGateway gateway = new(httpClient.Object, devicePageSize: 100, measurementPageSize: 2, accessoryPageSize: 2);

        MyAtmMeasurementPage<DeviceMeasurement> page = await gateway.HttpGetDeviceMeasurementPageAsync<DeviceMeasurement>(customerId, serialId, cursor, Period.Minutes1, TestContext.CancellationToken);

        Assert.HasCount(2, page.Measurements);
        Assert.AreEqual(cursor.AddMinutes(1), page.Measurements[0].Timestamp);
        Assert.AreEqual(cursor.AddMinutes(2), page.Measurements[1].Timestamp);
        Assert.AreEqual(cursor.AddMinutes(2), page.NextCursor);
        Assert.IsTrue(page.HasMore);
        httpClient.VerifyAll();
    }

    public TestContext TestContext { get; set; } = null!;
}

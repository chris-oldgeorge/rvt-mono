using AirQ.Api;
using AirQ.Model.Dto;
using AirQ.Model.Http;

namespace AirQMonitorTests;

[TestClass]
public class TestLocalMonitorFilterTests
{
    [TestMethod]
    public void Apply_WhenDisabled_ReturnsAllMonitors()
    {
        AirQTestLocalMonitorFilter filter = AirQTestLocalMonitorFilter.Create(enabled: false, targetSerialId: null);
        List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);

        List<NoiseMonitorDto> filtered = filter.Apply(monitors);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void Apply_WhenEnabled_ReturnsOnlyConfiguredMonitor()
    {
        AirQTestLocalMonitorFilter filter = AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: "Device2");
        List<NoiseMonitorDto> monitors = AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);

        List<NoiseMonitorDto> filtered = filter.Apply(monitors);

        Assert.HasCount(1, filtered);
        Assert.AreEqual("Device2", filtered[0].SerialId);
    }

    [TestMethod]
    public void ApplyCatalog_WhenEnabled_ReturnsOnlyConfiguredInstrument()
    {
        AirQTestLocalMonitorFilter filter = AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: "Device2");
        List<InstrumentResponse> instruments =
        [
            new() { InstrumentID = "Device1" },
            new() { InstrumentID = "Device2" }
        ];

        List<InstrumentResponse> filtered = filter.ApplyCatalog(instruments);

        Assert.HasCount(1, filtered);
        Assert.AreEqual("Device2", filtered[0].InstrumentID);
    }

    [TestMethod]
    public void Create_WhenEnabledWithoutTargetSerial_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: " "));
    }
}

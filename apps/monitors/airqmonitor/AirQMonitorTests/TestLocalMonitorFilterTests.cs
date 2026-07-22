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
        var filter = AirQTestLocalMonitorFilter.Create(enabled: false, targetSerialId: null);
        var monitors = AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);

        var filtered = filter.Apply(monitors);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void Apply_WhenEnabled_ReturnsOnlyConfiguredMonitor()
    {
        var filter = AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: "Device2");
        var monitors = AirQFixture.MonitorDtos(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE);

        var filtered = filter.Apply(monitors);

        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Device2", filtered[0].SerialId);
    }

    [TestMethod]
    public void ApplyCatalog_WhenEnabled_ReturnsOnlyConfiguredInstrument()
    {
        var filter = AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: "Device2");
        var instruments = new List<InstrumentResponse>
        {
            new() { InstrumentID = "Device1" },
            new() { InstrumentID = "Device2" }
        };

        var filtered = filter.ApplyCatalog(instruments);

        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Device2", filtered[0].InstrumentID);
    }

    [TestMethod]
    public void Create_WhenEnabledWithoutTargetSerial_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AirQTestLocalMonitorFilter.Create(enabled: true, targetSerialId: " "));
    }
}

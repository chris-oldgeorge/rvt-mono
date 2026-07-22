using MyAtm.Api;
using MyAtm.Model.Dto;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests;

[TestClass]
public class TestLocalMonitorFilterTests
{
    [TestMethod]
    public void ApplyReadMonitorFilter_WhenDisabled_ReturnsAllMonitors()
    {
        var monitors = new List<DustMonitorDto>
        {
            Monitor("R6025V", "21972"),
            Monitor("Other", "99999")
        };

        var filtered = MyAtmTestLocalMonitorFilter.Apply(monitors, enabled: false);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void ApplyReadMonitorFilter_WhenEnabled_ReturnsOnlyDemoDustMonitor()
    {
        var target = Monitor("R6025V", "21972");
        var sameSerialWrongFleet = Monitor("Other", "21972");
        var sameFleetWrongSerial = Monitor("R6025V", "99999");
        var monitors = new List<DustMonitorDto>
        {
            sameSerialWrongFleet,
            target,
            sameFleetWrongSerial
        };

        var filtered = MyAtmTestLocalMonitorFilter.Apply(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    [TestMethod]
    public void ApplyCatalogMonitorFilter_WhenEnabled_ReturnsOnlyDemoSerial()
    {
        var target = Monitor(null, "21972");
        var other = Monitor("R6025V", "99999");
        var monitors = new List<DustMonitorDto> { other, target };

        var filtered = MyAtmTestLocalMonitorFilter.ApplyCatalog(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    private static DustMonitorDto Monitor(string? fleetNr, string serialId) =>
        new(
            id: Guid.NewGuid(),
            customerId: 9,
            listedAtTime: DateTime.UtcNow,
            serialId: serialId,
            model: "AQ Guard",
            locationId: 123,
            latitude: 0,
            longitude: 0,
            address: null,
            timeZone: "Europe/London",
            customerDisplayName: fleetNr,
            lastDataTime1Min: null,
            lastDataTime15Min: null,
            lastDataTime1Hour: null,
            lastDataTime24Hour: null,
            manufacturer: "Palas GmbH",
            firmwareVersion: "1.0",
            fleetNr: fleetNr,
            offline: false);
}

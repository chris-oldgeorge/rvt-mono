
using Svantek.Api;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests;

[TestClass]
public class TestLocalMonitorFilterTests
{
    [TestMethod]
    public void ApplyReadMonitorFilter_WhenDisabled_ReturnsAllMonitors()
    {
        var monitors = new List<NoiseMonitorReadDto>
        {
            ReadMonitor("E125V", "157206"),
            ReadMonitor("Other", "999999")
        };

        var filtered = SvantekTestLocalMonitorFilter.Apply(monitors, enabled: false);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void ApplyReadMonitorFilter_WhenEnabled_ReturnsOnlyDemoNoiseMonitor()
    {
        var target = ReadMonitor("E125V", "157206");
        var sameSerialWrongFleet = ReadMonitor("Other", "157206");
        var sameFleetWrongSerial = ReadMonitor("E125V", "999999");
        var monitors = new List<NoiseMonitorReadDto>
        {
            sameSerialWrongFleet,
            target,
            sameFleetWrongSerial
        };

        var filtered = SvantekTestLocalMonitorFilter.Apply(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    [TestMethod]
    public void ApplyCatalogMonitorFilter_WhenEnabled_ReturnsOnlyDemoSerial()
    {
        var target = CatalogMonitor("157206");
        var other = CatalogMonitor("999999");
        var monitors = new List<NoiseMonitorDto> { other, target };

        var filtered = SvantekTestLocalMonitorFilter.Apply(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    private static NoiseMonitorReadDto ReadMonitor(string fleetNr, string serialId) =>
        new(
            Id: Guid.NewGuid(),
            FleetNr: fleetNr,
            SerialId: serialId,
            ProjectId: 123,
            PointId: 456,
            ListedAtTime: DateTime.UtcNow,
            LastDataTime: DateTime.UtcNow.AddHours(-1),
            LastStatusTimestamp: DateTime.UtcNow,
            DeployedStart: DateTime.UtcNow.AddDays(-1),
            Offline: false,
            BatteryStatus: SvantekApi.BatteryAlertType.Off,
            BatteryCharge: 100);

    private static NoiseMonitorDto CatalogMonitor(string serialId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SerialId = serialId,
            FleetNr = string.Empty,
            ProjectId = 123,
            PointId = 456,
            ListedAtTime = DateTime.UtcNow,
            Model = "SV 307A",
            CustomerDisplayName = "E125V",
            FirmwareVersion = "1.0"
        };
}

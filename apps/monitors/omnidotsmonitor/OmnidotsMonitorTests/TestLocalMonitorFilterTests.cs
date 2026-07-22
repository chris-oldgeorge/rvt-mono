using Omnidots.Api;
using Omnidots.Model.Dto;

using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests;

[TestClass]
public class TestLocalMonitorFilterTests
{
    [TestMethod]
    public void ApplyReadMonitorFilter_WhenDisabled_ReturnsAllMonitors()
    {
        var monitors = new List<VibrationMonitorDto>
        {
            Monitor("R17222V-QUCILO", "14768"),
            Monitor("Other", "99999")
        };

        var filtered = OmnidotsTestLocalMonitorFilter.Apply(monitors, enabled: false);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void ApplyReadMonitorFilter_WhenEnabled_ReturnsOnlyDemoVibrationMonitor()
    {
        var target = Monitor("R17222V-QUCILO", "14768");
        var sameSerialWrongFleet = Monitor("Other", "14768");
        var sameFleetWrongSerial = Monitor("R17222V-QUCILO", "99999");
        var monitors = new List<VibrationMonitorDto>
        {
            sameSerialWrongFleet,
            target,
            sameFleetWrongSerial
        };

        var filtered = OmnidotsTestLocalMonitorFilter.Apply(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    [TestMethod]
    public void ApplyCatalogMonitorFilter_WhenEnabled_ReturnsOnlyDemoSerial()
    {
        var target = Monitor("unknown", "14768");
        var other = Monitor("R17222V-QUCILO", "99999");
        var monitors = new List<VibrationMonitorDto> { other, target };

        var filtered = OmnidotsTestLocalMonitorFilter.ApplyCatalog(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    private static VibrationMonitorDto Monitor(string fleetNr, string serialId)
    {
        var status = new VibrationMonitorStatusDto(
            serialId: serialId,
            measurementDuration: 60,
            dataSaveLevel: 1,
            vdvEnabled: false,
            vdvX: null,
            vdvY: null,
            vdvZ: null,
            vdvPeriod: 0,
            traceSaveLevel: 1,
            tracePreTrigger: 1,
            tracePostTrigger: 1,
            alarmValue: 1,
            flatLevel: null,
            disableLed: false,
            logFlushInterval: 5,
            guideLine: null,
            buildingLevel: "unspecified",
            vectorEnabled: false,
            atopEnabled: false,
            vtopEnabled: false);

        return new VibrationMonitorDto(
            id: Guid.NewGuid(),
            listedAtTime: DateTime.UtcNow,
            lastDataTime: null,
            serialId: serialId,
            model: "SWARM",
            firmwareVersion: "1.0",
            manufacturer: "Omnidots",
            fleetNr: fleetNr,
            latitude: 0,
            longitude: 0,
            address: null,
            timeZone: "Europe/London",
            customerDisplayName: fleetNr,
            monitorStatus: status,
            sensor: null,
            offline: false,
            batteryStatus: OmnidotsApi.BatteryAlertType.Off,
            lastSeen: null,
            deployDate: DateTime.UtcNow);
    }
}

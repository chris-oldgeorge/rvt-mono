using Omnidots.Api;
using Omnidots.Model.Dto;
namespace OmnidotsAdapterTests;

[TestClass]
public class TestLocalMonitorFilterTests
{
    [TestMethod]
    public void ApplyReadMonitorFilter_WhenDisabled_ReturnsAllMonitors()
    {
        List<VibrationMonitorDto> monitors =
        [
            Monitor("R17222V-QUCILO", "14768"),
            Monitor("Other", "99999")
        ];

        List<VibrationMonitorDto> filtered = OmnidotsTestLocalMonitorFilter.Apply(monitors, enabled: false);

        CollectionAssert.AreEqual(monitors, filtered);
    }

    [TestMethod]
    public void ApplyReadMonitorFilter_WhenEnabled_ReturnsOnlyDemoVibrationMonitor()
    {
        VibrationMonitorDto target = Monitor("R17222V-QUCILO", "14768");
        VibrationMonitorDto sameSerialWrongFleet = Monitor("Other", "14768");
        VibrationMonitorDto sameFleetWrongSerial = Monitor("R17222V-QUCILO", "99999");
        List<VibrationMonitorDto> monitors =
        [
            sameSerialWrongFleet,
            target,
            sameFleetWrongSerial
        ];

        List<VibrationMonitorDto> filtered = OmnidotsTestLocalMonitorFilter.Apply(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    [TestMethod]
    public void ApplyCatalogMonitorFilter_WhenEnabled_ReturnsOnlyDemoSerial()
    {
        VibrationMonitorDto target = Monitor("unknown", "14768");
        VibrationMonitorDto other = Monitor("R17222V-QUCILO", "99999");
        List<VibrationMonitorDto> monitors = [other, target];

        List<VibrationMonitorDto> filtered = OmnidotsTestLocalMonitorFilter.ApplyCatalog(monitors, enabled: true);

        CollectionAssert.AreEqual(new[] { target }, filtered);
    }

    private static VibrationMonitorDto Monitor(string fleetNr, string serialId)
    {
        VibrationMonitorStatusDto status = new(
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

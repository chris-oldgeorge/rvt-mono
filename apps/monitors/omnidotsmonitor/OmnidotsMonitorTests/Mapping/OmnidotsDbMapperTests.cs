using Omnidots.Api;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Api.Db.Mapping;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Data.Entities;

namespace OmnidotsMonitorTests.Mapping;

[TestClass]
public sealed class OmnidotsDbMapperTests
{
    [TestMethod]
    public void ToVibrationMonitorDto_MapsMonitorStatusAndSensor()
    {
        var id = Guid.NewGuid();
        var monitor = new MonitorEntity
        {
            Id = id,
            ListedAtTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            LastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime(),
            SerialId = "14768",
            Model = "Vibration",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationAddress = "Test Road",
            TimeZone = "Europe/London",
            CustomerDisplayName = "RVT Test",
            Manufacturer = "Omnidots",
            FirmwareVersion = "1.2.3",
            FleetNr = "R17222V-QUCILO",
            Offline = true,
            BatteryStatus = 2,
            TypeOfMonitor = VibrationMonitorDto.MONITOR_TYPE_VIBRATION
        };
        var status = new OmnidotsMonitorStatusEntity
        {
            SerialId = "14768",
            MeasurementDuration = 60,
            DataSaveLevel = 1.1,
            VdvEnabled = true,
            VdvPeriod = 900,
            BuildingLevel = "DIN",
            LogFlushInterval = 10
        };
        var sensor = new OmnidotsSensorEntity
        {
            SerialId = "14768",
            Name = "S1",
            Lastseen = DateTime.Parse("2026-07-06T08:02:00Z").ToUniversalTime(),
            BatteryCharge = 88,
            ConnectedUsing = "lte",
            Online = true
        };

        var dto = OmnidotsDbMapper.ToVibrationMonitorDto(
            monitor,
            status,
            sensor,
            lastSeen: sensor.Lastseen,
            deployDate: DateTime.Parse("2026-07-01T00:00:00Z").ToUniversalTime());

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("14768", dto.SerialId);
        Assert.AreEqual("R17222V-QUCILO", dto.FleetNr);
        Assert.IsTrue(dto.Offline);
        Assert.AreEqual(OmnidotsApi.BatteryAlertType.BatteryCaution, dto.BatteryStatus);
        Assert.AreEqual(60, dto.MonitorStatus.MeasurementDuration);
        Assert.AreEqual("S1", dto.Sensor!.Name);
    }

    [TestMethod]
    public void UpdateMonitorEntity_DoesNotOverwriteLatestTimestampOrBatteryStatus()
    {
        var lastDataTime = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime();
        var entity = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            SerialId = "14768",
            LastDataTime1Min = lastDataTime,
            BatteryStatus = 2
        };
        var dto = new VibrationMonitorDto(
            id: entity.Id,
            listedAtTime: DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            lastDataTime: DateTime.Parse("2030-01-01T00:00:00Z").ToUniversalTime(),
            serialId: "14768",
            model: "Updated",
            firmwareVersion: "9.9.9",
            manufacturer: "Omnidots",
            fleetNr: "SHOULD-NOT-BE-COPIED",
            latitude: 10.5f,
            longitude: 20.5f,
            address: "New address",
            timeZone: "Europe/Athens",
            customerDisplayName: new string('x', 70),
            monitorStatus: MinimalStatus("14768"),
            sensor: null,
            offline: false,
            batteryStatus: OmnidotsApi.BatteryAlertType.BatteryAlert,
            lastSeen: null,
            deployDate: null);

        OmnidotsDbMapper.UpdateMonitorEntity(entity, dto);

        Assert.AreEqual(lastDataTime, entity.LastDataTime1Min);
        Assert.AreEqual((byte)2, entity.BatteryStatus);
        Assert.AreEqual("Updated", entity.Model);
        Assert.AreEqual(64, entity.CustomerDisplayName!.Length);
    }

    [TestMethod]
    public void ToPeakLevelEntity_MapsPeakRecordDto()
    {
        var dto = new PeakRecordDto(
            new FDomVtopOverflow(1.1, 1.2, 1.3),
            new FDomVtopOverflow(2.1, 2.2, 2.3),
            new FDomVtopOverflow(3.1, 3.2, 3.3),
            DateTimeOffset.Parse("2026-07-06T08:00:00Z").ToUnixTimeMilliseconds());

        var entity = OmnidotsDbMapper.ToPeakLevelEntity("14768", dto);

        Assert.AreEqual("14768", entity.SerialId);
        Assert.AreEqual(1.1, entity.XFdom);
        Assert.AreEqual(2.2, entity.YVtop);
        Assert.AreEqual(3.3, entity.ZVtopOverflow);
    }

    private static VibrationMonitorStatusDto MinimalStatus(string serialId)
    {
        return new VibrationMonitorStatusDto(serialId, 60, 1, false, null, null, null, 0, 0, 0, 0, 0, null, false, 0, null, "DIN", false, false, false);
    }
}

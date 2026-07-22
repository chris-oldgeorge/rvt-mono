using AirQ.Api.Db.EntityFramework;
using AirQ.Api.Db.Mapping;
using AirQ.Model.Dto;
using Rvt.Monitor.Common.Data.Entities;

namespace AirQMonitorTests.Mapping;

[TestClass]
public sealed class AirQDbMapperTests
{
    [TestMethod]
    public void ToNoiseMonitorDto_MapsMonitorAndStatusDefaults()
    {
        var id = Guid.NewGuid();
        var entity = new MonitorEntity
        {
            Id = id,
            ListedAtTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            LastDataTime15Min = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime(),
            SerialId = "AIRQ-1",
            Model = "Turnkey monitor",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationAddress = "Test Road",
            TimeZone = "Europe/London",
            CustomerDisplayName = "RVT Test",
            Manufacturer = "Turnkey",
            FirmwareVersion = "1.2.3",
            FleetNr = "FLEET-1",
            Offline = true,
            TypeOfMonitor = NoiseMonitorDto.MONITOR_TYPE_NOISE
        };
        var status = new AirQMonitorStatusEntity
        {
            SerialId = "AIRQ-1",
            UpdateTime = DateTime.Parse("2026-07-06T08:05:00Z").ToUniversalTime(),
            Status = NoiseMonitorStatus.ACTIVE,
            ErrorCount = 2,
            BatteryVoltage = "12.3",
            CalibrationDate = DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(),
            FilterChangeDate = DateTime.Parse("2026-02-01T00:00:00Z").ToUniversalTime(),
            PumpHours = "42"
        };

        var dto = AirQDbMapper.ToNoiseMonitorDto(entity, new Dictionary<string, AirQMonitorStatusEntity>
        {
            ["AIRQ-1"] = status
        });

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("AIRQ-1", dto.SerialId);
        Assert.AreEqual("Turnkey monitor", dto.Model);
        Assert.AreEqual(51.5f, dto.Latitude);
        Assert.AreEqual(-0.1f, dto.Longitude);
        Assert.AreEqual("Test Road", dto.Address);
        Assert.AreEqual("FLEET-1", dto.FleetNr);
        Assert.IsTrue(dto.Offline);
        Assert.AreEqual(NoiseMonitorStatus.ACTIVE, dto.MonitorStatus.Status);
        Assert.AreEqual(2, dto.MonitorStatus.ErrorCount);
        Assert.AreEqual("12.3", dto.MonitorStatus.BatteryVoltage);
    }

    [TestMethod]
    public void UpdateMonitorEntity_DoesNotOverwriteLatestTimestamp()
    {
        var lastDataTime = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime();
        var entity = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            SerialId = "AIRQ-1",
            LastDataTime15Min = lastDataTime
        };
        var dto = new NoiseMonitorDto(
            id: entity.Id,
            listedAtTime: DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            lastDataTime: DateTime.Parse("2030-01-01T00:00:00Z").ToUniversalTime(),
            serialId: "AIRQ-1",
            model: "Updated model",
            firmwareVersion: "9.9.9",
            manufacturer: "Turnkey",
            fleetNr: "FLEET-2",
            latitude: 10.5f,
            longitude: 20.5f,
            address: "New address",
            timeZone: "Europe/Athens",
            customerDisplayName: "Updated customer",
            offline: false,
            monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE, 0, null, null, null, null));

        AirQDbMapper.UpdateMonitorEntity(entity, dto);

        Assert.AreEqual(lastDataTime, entity.LastDataTime15Min);
        Assert.AreEqual("Updated model", entity.Model);
        Assert.AreEqual("FLEET-2", entity.FleetNr);
        Assert.AreEqual(NoiseMonitorDto.MONITOR_TYPE_NOISE, entity.TypeOfMonitor);
    }

    [TestMethod]
    public void ToNoiseLevelEntity_MapsNoiseDtoWithSerialId()
    {
        var sampleTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime();
        var dto = new NoiseDto(sampleTime, 1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8);

        var entity = AirQDbMapper.ToNoiseLevelEntity("AIRQ-1", dto);

        Assert.AreEqual("AIRQ-1", entity.SerialId);
        Assert.AreEqual(sampleTime, entity.SampleTime);
        Assert.AreEqual(1.1, entity.LAeq);
        Assert.AreEqual(2.2, entity.LAmax);
        Assert.AreEqual(3.3, entity.LA90);
        Assert.AreEqual(4.4, entity.LA10);
        Assert.AreEqual(5.5, entity.LCeq);
        Assert.AreEqual(6.6, entity.LCmax);
        Assert.AreEqual(7.7, entity.LC90);
        Assert.AreEqual(8.8, entity.LC10);
    }

    [TestMethod]
    public void UpdateMonitorStatusEntity_MapsStatusFields()
    {
        var entity = new AirQMonitorStatusEntity { Id = "AIRQ-1", SerialId = "AIRQ-1" };
        var updateTime = DateTime.Parse("2026-07-06T08:05:00Z").ToUniversalTime();
        var dto = new NoiseMonitorStatus(updateTime, NoiseMonitorStatus.ACTIVE, 3, "12.4", null, null, "84");

        AirQDbMapper.UpdateMonitorStatusEntity(entity, dto);

        Assert.AreEqual(updateTime, entity.UpdateTime);
        Assert.AreEqual(NoiseMonitorStatus.ACTIVE, entity.Status);
        Assert.AreEqual(3, entity.ErrorCount);
        Assert.AreEqual("12.4", entity.BatteryVoltage);
        Assert.AreEqual("84", entity.PumpHours);
    }
}

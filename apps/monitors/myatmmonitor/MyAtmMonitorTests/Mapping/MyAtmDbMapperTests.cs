using MyAtm.Api.Db.EntityFramework;
using MyAtm.Api.Db.Mapping;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Data.Entities;

namespace MyAtmMonitorTests.Mapping;

[TestClass]
public sealed class MyAtmDbMapperTests
{
    [TestMethod]
    public void ToDustMonitorDto_MapsMonitorEntityDefaults()
    {
        var id = Guid.NewGuid();
        var entity = new MonitorEntity
        {
            Id = id,
            CustomerId = 42,
            ListedAtTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            SerialId = "21972",
            Model = "Fidas Frog",
            LocationId = 7,
            Latitude = 51.5,
            Longitude = -0.1,
            LocationAddress = "Test Road",
            TimeZone = "Europe/London",
            CustomerDisplayName = "RVT Test",
            LastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime(),
            LastDataTime15Min = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime(),
            LastDataTime1Hour = DateTime.Parse("2026-07-06T09:00:00Z").ToUniversalTime(),
            LastDataTime24Hour = DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime(),
            Manufacturer = "Palas GmbH",
            FirmwareVersion = "1.2.3",
            FleetNr = "R6025V",
            Offline = true,
            TypeOfMonitor = DustMonitorDto.MONITOR_TYPE_DUST
        };

        var dto = MyAtmDbMapper.ToDustMonitorDto(entity);

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual(42, dto.CustomerId);
        Assert.AreEqual("21972", dto.SerialId);
        Assert.AreEqual("Fidas Frog", dto.Model);
        Assert.AreEqual(7, dto.LocationId);
        Assert.AreEqual(51.5f, dto.Latitude);
        Assert.AreEqual(-0.1f, dto.Longitude);
        Assert.AreEqual("Test Road", dto.Address);
        Assert.AreEqual("Europe/London", dto.TimeZone);
        Assert.AreEqual("RVT Test", dto.CustomerDisplayName);
        Assert.AreEqual("Palas GmbH", dto.Manufacturer);
        Assert.AreEqual("1.2.3", dto.FirmwareVersion);
        Assert.AreEqual("R6025V", dto.FleetNr);
        Assert.IsTrue(dto.Offline);
    }

    [TestMethod]
    public void UpdateMonitorEntity_DoesNotOverwriteFleetOrLatestTimestamps()
    {
        var entity = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            SerialId = "21972",
            FleetNr = "R6025V",
            Offline = true,
            LastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime()
        };

        var unspecifiedListedAt = new DateTime(2026, 7, 6, 7, 0, 0, DateTimeKind.Unspecified);
        var dto = new DustMonitorDto(
            id: entity.Id,
            customerId: 99,
            listedAtTime: unspecifiedListedAt,
            serialId: "21972",
            model: "Updated model",
            locationId: 12,
            latitude: 10.5f,
            longitude: 20.5f,
            address: "New address",
            timeZone: "Europe/Athens",
            customerDisplayName: "Updated customer",
            lastDataTime1Min: DateTime.Parse("2030-01-01T00:00:00Z").ToUniversalTime(),
            lastDataTime15Min: null,
            lastDataTime1Hour: null,
            lastDataTime24Hour: null,
            manufacturer: "Palas GmbH",
            firmwareVersion: "9.9.9",
            fleetNr: "SHOULD-NOT-BE-COPIED",
            offline: false);

        MyAtmDbMapper.UpdateMonitorEntity(entity, dto);

        Assert.AreEqual("R6025V", entity.FleetNr);
        Assert.IsTrue(entity.Offline, "The offline state is owned by the local offline workflow, not the vendor catalogue.");
        Assert.AreEqual(DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime(), entity.LastDataTime1Min);
        Assert.AreEqual("Updated model", entity.Model);
        Assert.AreEqual(99, entity.CustomerId);
        Assert.AreEqual(DustMonitorDto.MONITOR_TYPE_DUST, entity.TypeOfMonitor);
        Assert.AreEqual(unspecifiedListedAt.Ticks, entity.ListedAtTime.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, entity.ListedAtTime.Kind);
    }

    [TestMethod]
    public void ToDustLevelEntity_MapsMeasurementDto()
    {
        var dto = new DustDto("21972", 60, DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            pm1: 1.1, pm2_5: 2.2, pm10: 10.1, pmTotal: 13.4, weather_t: 20.5, weather_p: 1012.1, weather_rh: 80.2);

        var entity = MyAtmDbMapper.ToDustLevelEntity(dto);

        Assert.AreEqual("21972", entity.SerialId);
        Assert.AreEqual(60, entity.Avrg);
        Assert.AreEqual(dto.SampleTime, entity.SampleTime);
        Assert.AreEqual(1.1, entity.Pm1);
        Assert.AreEqual(2.2, entity.Pm2_5);
        Assert.AreEqual(10.1, entity.Pm10);
        Assert.AreEqual(13.4, entity.PmTotal);
        Assert.AreEqual(20.5, entity.Weather_t);
        Assert.AreEqual(1012.1, entity.Weather_p);
        Assert.AreEqual(80.2, entity.Weather_rh);
    }
}

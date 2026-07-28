using Rvt.Monitor.Common.Data.Entities;
using Svantek.Api;
using Svantek.Api.Db.EntityFramework;
using Svantek.Api.Db.Mapping;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;

namespace SvantekMonitorTests.Mapping;

[TestClass]
public sealed class SvantekDbMapperTests
{
    [TestMethod]
    public void ToNoiseMonitorReadDto_MapsMonitorStatusAndDeployment()
    {
        Guid id = Guid.NewGuid();
        DateTime deployedStart = DateTime.Parse("2026-07-06T07:30:00Z").ToUniversalTime();
        MonitorEntity entity = new()
        {
            Id = id,
            ListedAtTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            LastDataTime15Min = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime(),
            SerialId = "157206",
            FleetNr = "E125V",
            Offline = true,
            BatteryStatus = 2,
            TypeOfMonitor = NoiseMonitorDto.MONITOR_TYPE_NOISE
        };
        SvantekMonitorStatusEntity status = new()
        {
            SerialId = "157206",
            ProjectId = 123,
            PointId = 456,
            LastStatusTimestamp = "2026-07-06T08:20:00",
            BatteryCharge = 77
        };
        DeploymentEntity deployment = new()
        {
            MonitorId = id,
            StartDate = deployedStart
        };

        NoiseMonitorReadDto dto = SvantekDbMapper.ToNoiseMonitorReadDto(entity, status, deployment);

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("E125V", dto.FleetNr);
        Assert.AreEqual("157206", dto.SerialId);
        Assert.AreEqual(123, dto.ProjectId);
        Assert.AreEqual(456, dto.PointId);
        Assert.AreEqual(entity.ListedAtTime, dto.ListedAtTime);
        Assert.AreEqual(entity.LastDataTime15Min, dto.LastDataTime);
        Assert.AreEqual(DateTime.Parse("2026-07-06T08:20:00"), dto.LastStatusTimestamp);
        Assert.AreEqual(deployedStart, dto.DeployedStart);
        Assert.IsTrue(dto.Offline);
        Assert.AreEqual(SvantekApi.BatteryAlertType.BatteryCaution, dto.BatteryStatus);
        Assert.AreEqual(77, dto.BatteryCharge);
    }

    [TestMethod]
    public void UpdateMonitorEntity_PreservesRuntimeOwnedStateAndUpdatesCatalogueMetadata()
    {
        Guid monitorId = Guid.NewGuid();
        Guid catalogueMonitorId = Guid.NewGuid();
        Assert.AreNotEqual(monitorId, catalogueMonitorId);
        DateTime lastDataTime1Min = DateTime.Parse("2026-07-06T08:01:00Z").ToUniversalTime();
        DateTime lastDataTime15Min = DateTime.Parse("2026-07-06T08:15:00Z").ToUniversalTime();
        DateTime lastDataTime1Hour = DateTime.Parse("2026-07-06T09:00:00Z").ToUniversalTime();
        DateTime lastDataTime24Hour = DateTime.Parse("2026-07-07T00:00:00Z").ToUniversalTime();
        MonitorEntity entity = new()
        {
            Id = monitorId,
            SerialId = "157206",
            CustomerId = 123,
            LocationId = 456,
            LastDataTime1Min = lastDataTime1Min,
            LastDataTime15Min = lastDataTime15Min,
            LastDataTime1Hour = lastDataTime1Hour,
            LastDataTime24Hour = lastDataTime24Hour,
            Offline = true,
            BatteryStatus = 1,
            Model = "Original model",
            FirmwareVersion = "1.0.0"
        };
        NoiseMonitorDto dto = new(
            id: catalogueMonitorId,
            listedAtTime: DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime(),
            lastDataTime: DateTime.Parse("2030-01-01T00:00:00Z").ToUniversalTime(),
            serialId: "157206",
            model: "Updated model",
            firmwareVersion: "9.9.9",
            manufacturer: "Svantek",
            fleetNr: "E125V",
            latitude: 10.5f,
            longitude: 20.5f,
            address: "New address",
            timeZone: "Europe/Athens",
            customerDisplayName: "Updated customer",
            offline: false,
            monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, NoiseMonitorStatus.ACTIVE, 0, "12.4", DateTime.UtcNow, DateTime.UtcNow, "84"));

        SvantekDbMapper.UpdateMonitorEntity(entity, dto);

        Assert.AreEqual(monitorId, entity.Id);
        Assert.AreEqual(123, entity.CustomerId);
        Assert.AreEqual(456, entity.LocationId);
        Assert.AreEqual(lastDataTime1Min, entity.LastDataTime1Min);
        Assert.AreEqual(lastDataTime15Min, entity.LastDataTime15Min);
        Assert.AreEqual(lastDataTime1Hour, entity.LastDataTime1Hour);
        Assert.AreEqual(lastDataTime24Hour, entity.LastDataTime24Hour);
        Assert.IsTrue(entity.Offline);
        Assert.AreEqual(1, entity.BatteryStatus.GetValueOrDefault());
        Assert.AreEqual("Updated model", entity.Model);
        Assert.AreEqual("9.9.9", entity.FirmwareVersion);
        Assert.AreEqual("E125V", entity.FleetNr);
        Assert.AreEqual(NoiseMonitorDto.MONITOR_TYPE_NOISE, entity.TypeOfMonitor);
    }

    [TestMethod]
    public void ToNoiseLevelEntity_MapsNoiseDtoWithSerialId()
    {
        DateTime sampleTime = DateTime.Parse("2026-07-06T08:00:00Z").ToUniversalTime();
        NoiseDto dto = new(sampleTime, 1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8);

        SvantekNoiseLevelEntity entity = SvantekDbMapper.ToNoiseLevelEntity("157206", dto);

        Assert.AreEqual("157206", entity.SerialId);
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
    public void NormalizeSampleTimeForPostgreSql_ConvertsUnspecifiedSampleTimeToUtc()
    {
        DateTime sampleTime = DateTime.SpecifyKind(
            DateTime.Parse("2026-03-15T20:00:20"),
            DateTimeKind.Unspecified);

        DateTime normalized = SvantekDbMapper.NormalizeSampleTimeForPostgreSql(sampleTime);

        Assert.AreEqual(DateTimeKind.Utc, normalized.Kind);
        Assert.AreEqual(DateTime.SpecifyKind(sampleTime, DateTimeKind.Utc), normalized);
    }

    [TestMethod]
    public void UpdateMonitorStatusEntity_MapsStatusFields()
    {
        SvantekMonitorStatusEntity entity = new() { SerialId = "157206" };
        DateTime updateTime = DateTime.Parse("2026-07-06T08:05:00Z").ToUniversalTime();
        NoiseMonitorStatus dto = new(updateTime, NoiseMonitorStatus.ACTIVE, 3, "12.4",
            DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(),
            DateTime.Parse("2026-02-01T00:00:00Z").ToUniversalTime(),
            "84");

        SvantekDbMapper.UpdateMonitorStatusEntity(entity, dto);

        Assert.AreEqual(updateTime, entity.UpdateTime);
        Assert.AreEqual(NoiseMonitorStatus.ACTIVE, entity.Status);
        Assert.AreEqual(3, entity.ErrorCount);
        Assert.AreEqual("12.4", entity.BatteryVoltage);
        Assert.AreEqual("84", entity.PumpHours);
    }
}

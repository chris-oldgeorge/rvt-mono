using AirQ.Api.Db.EntityFramework;
using AirQ.Model.Dto;
using Riok.Mapperly.Abstractions;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.Db.Mapping;

[Mapper]
public static partial class AirQDbMapper
{
    public static NoiseMonitorDto ToNoiseMonitorDto(
        MonitorEntity row,
        IReadOnlyDictionary<string, AirQMonitorStatusEntity> statuses)
    {
        if (!statuses.TryGetValue(row.SerialId, out var status))
        {
            throw AdapterException.Of(string.Format("Missing NoiseMonitorStatus for serial Id={0}", row.SerialId));
        }

        return new NoiseMonitorDto(
            id: row.Id,
            listedAtTime: row.ListedAtTime,
            lastDataTime: row.LastDataTime15Min,
            serialId: row.SerialId,
            model: row.Model,
            firmwareVersion: row.FirmwareVersion,
            manufacturer: row.Manufacturer,
            fleetNr: row.FleetNr,
            latitude: (float)(row.Latitude ?? 0),
            longitude: (float)(row.Longitude ?? 0),
            address: row.LocationAddress,
            timeZone: row.TimeZone,
            customerDisplayName: row.CustomerDisplayName,
            offline: row.Offline ?? false,
            monitorStatus: ToNoiseMonitorStatus(status));
    }

    public static MonitorEntity ToMonitorEntity(NoiseMonitorDto dto)
    {
        var entity = new MonitorEntity { Id = dto.Id };
        UpdateMonitorEntity(entity, dto);
        return entity;
    }

    [MapperIgnoreTarget(nameof(MonitorEntity.Id))]
    [MapperIgnoreTarget(nameof(MonitorEntity.CustomerId))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LocationId))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime15Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime24Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.BatteryStatus))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Id))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastDataTime))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.MonitorStatus))]
    [MapProperty(nameof(NoiseMonitorDto.Address), nameof(MonitorEntity.LocationAddress))]
    public static partial void UpdateMonitorEntity([MappingTarget] MonitorEntity entity, NoiseMonitorDto dto);

    public static AirQNoiseLevelEntity ToNoiseLevelEntity(string serialId, NoiseDto dto)
    {
        var entity = ToNoiseLevelEntity(dto);
        entity.SerialId = serialId;
        return entity;
    }

    [MapperIgnoreTarget(nameof(AirQMonitorStatusEntity.Id))]
    [MapperIgnoreTarget(nameof(AirQMonitorStatusEntity.SerialId))]
    public static partial void UpdateMonitorStatusEntity(
        [MappingTarget] AirQMonitorStatusEntity entity,
        NoiseMonitorStatus dto);

    [MapperIgnoreTarget(nameof(AirQNoiseLevelEntity.SerialId))]
    private static partial AirQNoiseLevelEntity ToNoiseLevelEntity(NoiseDto dto);

    private static NoiseMonitorStatus ToNoiseMonitorStatus(AirQMonitorStatusEntity status)
    {
        return new NoiseMonitorStatus(
            updateTime: status.UpdateTime,
            status: status.Status,
            errorCount: status.ErrorCount,
            batteryVoltage: status.BatteryVoltage,
            calibrationDate: status.CalibrationDate,
            filterChangeDate: status.FilterChangeDate,
            pumpHours: status.PumpHours);
    }
}

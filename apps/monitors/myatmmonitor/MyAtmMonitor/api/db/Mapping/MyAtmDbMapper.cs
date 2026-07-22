using MyAtm.Api.Db.EntityFramework;
using MyAtm.Model.Dto;
using Riok.Mapperly.Abstractions;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.Db.Mapping;

[Mapper]
public static partial class MyAtmDbMapper
{
    public static DustMonitorDto ToDustMonitorDto(MonitorEntity row)
    {
        return new DustMonitorDto(
            id: row.Id,
            customerId: row.CustomerId ?? 0,
            listedAtTime: DateTimeUtil.AsUtc(row.ListedAtTime),
            serialId: row.SerialId,
            model: row.Model,
            locationId: row.LocationId ?? 0,
            latitude: (float)(row.Latitude ?? 0),
            longitude: (float)(row.Longitude ?? 0),
            address: row.LocationAddress,
            timeZone: row.TimeZone,
            customerDisplayName: row.CustomerDisplayName,
            lastDataTime1Min: DateTimeUtil.AsUtc(row.LastDataTime1Min),
            lastDataTime15Min: DateTimeUtil.AsUtc(row.LastDataTime15Min),
            lastDataTime1Hour: DateTimeUtil.AsUtc(row.LastDataTime1Hour),
            lastDataTime24Hour: DateTimeUtil.AsUtc(row.LastDataTime24Hour),
            manufacturer: row.Manufacturer,
            firmwareVersion: row.FirmwareVersion,
            fleetNr: row.FleetNr,
            offline: row.Offline ?? false);
    }

    public static MonitorEntity ToMonitorEntity(DustMonitorDto dto)
    {
        var entity = new MonitorEntity
        {
            Id = dto.Id
        };
        UpdateMonitorEntity(entity, dto);
        return entity;
    }

    public static void UpdateMonitorEntity(MonitorEntity entity, DustMonitorDto dto)
    {
        MapMonitorEntity(entity, dto);
        entity.ListedAtTime = DateTimeUtil.AsUtc(entity.ListedAtTime);
    }

    [MapperIgnoreTarget(nameof(MonitorEntity.Id))]
    [MapperIgnoreTarget(nameof(MonitorEntity.FleetNr))]
    [MapperIgnoreTarget(nameof(MonitorEntity.Offline))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime15Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime24Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.BatteryStatus))]
    [MapperIgnoreSource(nameof(DustMonitorDto.Id))]
    [MapperIgnoreSource(nameof(DustMonitorDto.FleetNr))]
    [MapperIgnoreSource(nameof(DustMonitorDto.Offline))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime1Min))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime15Min))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime1Hour))]
    [MapperIgnoreSource(nameof(DustMonitorDto.LastDataTime24Hour))]
    [MapProperty(nameof(DustMonitorDto.Address), nameof(MonitorEntity.LocationAddress))]
    private static partial void MapMonitorEntity([MappingTarget] MonitorEntity entity, DustMonitorDto dto);

    public static partial MyAtmDustLevelEntity ToDustLevelEntity(DustDto dto);

    public static partial MyAtmAccessoryInfoEntity ToAccessoryInfoEntity(AccessoryInfoDto dto);
}

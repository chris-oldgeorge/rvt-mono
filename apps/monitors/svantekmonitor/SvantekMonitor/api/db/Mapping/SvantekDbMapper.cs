using Riok.Mapperly.Abstractions;
using Rvt.Monitor.Common.Data.Entities;
using Svantek.Api.Db.EntityFramework;
using Svantek.Model.Dto;
using SvantekMonitor.model.dto;
using static Svantek.Api.SvantekApi;

namespace Svantek.Api.Db.Mapping;

[Mapper]
public static partial class SvantekDbMapper
{
    public static NoiseMonitorReadDto ToNoiseMonitorReadDto(
        MonitorEntity monitor,
        SvantekMonitorStatusEntity status,
        DeploymentEntity deployment)
    {
        return new NoiseMonitorReadDto(
            Id: monitor.Id,
            FleetNr: monitor.FleetNr ?? string.Empty,
            SerialId: monitor.SerialId,
            ProjectId: status.ProjectId ?? 0,
            PointId: status.PointId ?? 0,
            ListedAtTime: monitor.ListedAtTime,
            LastDataTime: monitor.LastDataTime15Min,
            LastStatusTimestamp: ParseDateTime(status.LastStatusTimestamp),
            DeployedStart: deployment.StartDate,
            Offline: monitor.Offline ?? false,
            BatteryStatus: (BatteryAlertType)(monitor.BatteryStatus ?? 0),
            BatteryCharge: status.BatteryCharge ?? 100);
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
    [MapperIgnoreTarget(nameof(MonitorEntity.Offline))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime15Min))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime1Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.LastDataTime24Hour))]
    [MapperIgnoreTarget(nameof(MonitorEntity.BatteryStatus))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Id))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.ProjectId))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.PointId))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Active))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastLogin))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastLogout))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.IsOnline))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastStatusTimestamp))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.BatteryCharge))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.BatteryTimeToEmpty))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.PowerSource))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.IsBatteryCharging))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.GsmSignalQuality))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.MeasurementState))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastDataTime))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Offline))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.MonitorStatus))]
    [MapProperty(nameof(NoiseMonitorDto.Address), nameof(MonitorEntity.LocationAddress))]
    public static partial void UpdateMonitorEntity([MappingTarget] MonitorEntity entity, NoiseMonitorDto dto);

    public static SvantekMonitorStatusEntity ToStatusEntity(NoiseMonitorDto dto)
    {
        var entity = new SvantekMonitorStatusEntity
        {
            SerialId = dto.SerialId,
            UpdateTime = DateTime.UtcNow,
            Status = NoiseMonitorStatus.ACTIVE,
            ErrorCount = dto.MonitorStatus?.ErrorCount ?? 0,
            BatteryVoltage = dto.MonitorStatus?.BatteryVoltage,
            CalibrationDate = dto.MonitorStatus?.CalibrationDate,
            FilterChangeDate = dto.MonitorStatus?.FilterChangeDate,
            PumpHours = dto.MonitorStatus?.PumpHours
        };
        UpdateStatusEntity(entity, dto);
        return entity;
    }

    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.SerialId))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.UpdateTime))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.Status))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.ErrorCount))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.BatteryVoltage))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.CalibrationDate))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.FilterChangeDate))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.PumpHours))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Id))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.SerialId))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.FleetNr))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.ListedAtTime))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Model))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.CustomerDisplayName))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Manufacturer))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.TypeOfMonitor))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.FirmwareVersion))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.LastDataTime))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Offline))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Latitude))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Longitude))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.Address))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.TimeZone))]
    [MapperIgnoreSource(nameof(NoiseMonitorDto.MonitorStatus))]
    public static partial void UpdateStatusEntity([MappingTarget] SvantekMonitorStatusEntity entity, NoiseMonitorDto dto);

    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.SerialId))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.ProjectId))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.PointId))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.Active))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.LastLogin))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.LastLogout))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.IsOnline))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.LastStatusTimestamp))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.BatteryCharge))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.BatteryTimeToEmpty))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.PowerSource))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.IsBatteryCharging))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.GsmSignalQuality))]
    [MapperIgnoreTarget(nameof(SvantekMonitorStatusEntity.MeasurementState))]
    [MapProperty(nameof(NoiseMonitorStatus.StatusTime), nameof(SvantekMonitorStatusEntity.UpdateTime))]
    public static partial void UpdateMonitorStatusEntity(
        [MappingTarget] SvantekMonitorStatusEntity entity,
        NoiseMonitorStatus dto);

    public static SvantekNoiseLevelEntity ToNoiseLevelEntity(string serialId, NoiseDto dto)
    {
        var entity = ToNoiseLevelEntity(dto);
        entity.SerialId = serialId;
        return entity;
    }

    public static DateTime NormalizeSampleTimeForPostgreSql(DateTime sampleTime)
    {
        return sampleTime.Kind switch
        {
            DateTimeKind.Utc => sampleTime,
            DateTimeKind.Local => sampleTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(sampleTime, DateTimeKind.Utc)
        };
    }

    [MapperIgnoreTarget(nameof(SvantekNoiseLevelEntity.SerialId))]
    [MapperIgnoreSource(nameof(NoiseDto.SerialId))]
    private static partial SvantekNoiseLevelEntity ToNoiseLevelEntity(NoiseDto dto);

    private static DateTime? ParseDateTime(string? value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}

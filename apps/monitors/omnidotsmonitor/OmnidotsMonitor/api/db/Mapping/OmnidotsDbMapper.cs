using Omnidots.Api.Db.EntityFramework;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Riok.Mapperly.Abstractions;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Utilities;
using static Omnidots.Api.OmnidotsApi;

namespace Omnidots.Api.Db.Mapping;

[Mapper]
public static partial class OmnidotsDbMapper
{
    public static VibrationMonitorDto ToVibrationMonitorDto(
        MonitorEntity monitor,
        OmnidotsMonitorStatusEntity status,
        OmnidotsSensorEntity? sensor,
        DateTime? lastSeen,
        DateTime? deployDate)
    {
        return new VibrationMonitorDto(
            id: monitor.Id,
            listedAtTime: monitor.ListedAtTime,
            serialId: monitor.SerialId,
            model: monitor.Model,
            latitude: (float)(monitor.Latitude ?? 0),
            longitude: (float)(monitor.Longitude ?? 0),
            address: monitor.LocationAddress,
            timeZone: monitor.TimeZone,
            customerDisplayName: monitor.CustomerDisplayName,
            manufacturer: monitor.Manufacturer,
            firmwareVersion: monitor.FirmwareVersion,
            fleetNr: monitor.FleetNr,
            lastDataTime: monitor.LastDataTime1Min,
            monitorStatus: ToStatusDto(status),
            sensor: sensor == null ? null : ToSensorDto(sensor),
            offline: monitor.Offline ?? false,
            batteryStatus: (BatteryAlertType)(monitor.BatteryStatus ?? 0),
            lastSeen: lastSeen,
            deployDate: deployDate);
    }

    public static MonitorEntity ToMonitorEntity(VibrationMonitorDto dto)
    {
        var entity = new MonitorEntity { Id = dto.Id };
        UpdateMonitorEntity(entity, dto);
        return entity;
    }

    public static void UpdateMonitorEntity(MonitorEntity entity, VibrationMonitorDto dto)
    {
        entity.ListedAtTime = dto.ListedAtTime;
        entity.SerialId = dto.SerialId;
        entity.Model = dto.Model;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.LocationAddress = dto.Address;
        entity.TimeZone = dto.TimeZone;
        entity.CustomerDisplayName = dto.CustomerDisplayName != null && dto.CustomerDisplayName.Length > 64
            ? dto.CustomerDisplayName.Substring(0, 64)
            : dto.CustomerDisplayName;
        entity.Manufacturer = dto.Manufacturer;
        entity.FirmwareVersion = dto.FirmwareVersion;
        entity.TypeOfMonitor = dto.TypeOfMonitor;
        entity.Offline = dto.Offline;
    }

    public static VibrationMonitorStatusDto ToStatusDto(OmnidotsMonitorStatusEntity entity)
    {
        return new VibrationMonitorStatusDto(
            serialId: entity.SerialId,
            measurementDuration: entity.MeasurementDuration ?? 0,
            dataSaveLevel: entity.DataSaveLevel ?? 0,
            vdvEnabled: entity.VdvEnabled,
            vdvX: entity.VdvX,
            vdvY: entity.VdvY,
            vdvZ: entity.VdvZ,
            vdvPeriod: entity.VdvPeriod ?? 0,
            traceSaveLevel: entity.TraceSaveLevel ?? 0,
            tracePreTrigger: entity.TracePreTrigger ?? 0,
            tracePostTrigger: entity.TracePostTrigger ?? 0,
            alarmValue: entity.AlarmValue ?? 0,
            flatLevel: entity.FlatLevel,
            disableLed: entity.DisableLed,
            logFlushInterval: entity.LogFlushInterval,
            guideLine: entity.GuideLine,
            buildingLevel: entity.BuildingLevel,
            vectorEnabled: entity.VectorEnabled,
            atopEnabled: entity.AtopEnabled,
            vtopEnabled: entity.VtopEnabled);
    }

    public static SensorDto ToSensorDto(OmnidotsSensorEntity entity)
    {
        return new SensorDto(
            serialId: entity.SerialId,
            name: entity.Name,
            lastseen: entity.Lastseen,
            batteryCharge: entity.BatteryCharge,
            connectedUsing: entity.ConnectedUsing,
            online: entity.Online);
    }

    public static OmnidotsPeakLevelEntity ToPeakLevelEntity(string serialId, PeakRecordDto dto)
    {
        return new OmnidotsPeakLevelEntity
        {
            SerialId = serialId,
            SampleTime = dto.SampleTime,
            XFdom = dto.X?.Fdom,
            XVtop = dto.X?.Vtop,
            XVtopOverflow = dto.X?.VtopOverflow,
            YFdom = dto.Y?.Fdom,
            YVtop = dto.Y?.Vtop,
            YVtopOverflow = dto.Y?.VtopOverflow,
            ZFdom = dto.Z?.Fdom,
            ZVtop = dto.Z?.Vtop,
            ZVtopOverflow = dto.Z?.VtopOverflow
        };
    }

    public static OmnidotsVeffLevelEntity ToVeffLevelEntity(string serialId, VeffRecordDto dto)
    {
        var entity = ToVeffLevelEntity(dto);
        entity.SerialId = serialId;
        return entity;
    }

    public static OmnidotsVdvLevelEntity ToVdvLevelEntity(string serialId, VdvRecordDto dto)
    {
        var entity = ToVdvLevelEntity(dto);
        entity.SerialId = serialId;
        return entity;
    }

    [MapperIgnoreTarget(nameof(OmnidotsMonitorStatusEntity.Id))]
    [MapperIgnoreTarget(nameof(OmnidotsMonitorStatusEntity.SerialId))]
    [MapperIgnoreSource(nameof(VibrationMonitorStatusDto.SerialId))]
    public static partial void UpdateMonitorStatusEntity(
        [MappingTarget] OmnidotsMonitorStatusEntity entity,
        VibrationMonitorStatusDto dto);

    [MapperIgnoreTarget(nameof(OmnidotsSensorEntity.Id))]
    [MapperIgnoreTarget(nameof(OmnidotsSensorEntity.SerialId))]
    [MapperIgnoreSource(nameof(SensorDto.SerialId))]
    public static partial void UpdateSensorEntity(
        [MappingTarget] OmnidotsSensorEntity entity,
        SensorDto dto);

    [MapperIgnoreTarget(nameof(OmnidotsVeffLevelEntity.SerialId))]
    [MapperIgnoreSource(nameof(VeffRecordDto.MeasurementDuration))]
    private static partial OmnidotsVeffLevelEntity ToVeffLevelEntity(VeffRecordDto dto);

    [MapperIgnoreTarget(nameof(OmnidotsVdvLevelEntity.SerialId))]
    [MapperIgnoreSource(nameof(VdvRecordDto.MeasurementDuration))]
    private static partial OmnidotsVdvLevelEntity ToVdvLevelEntity(VdvRecordDto dto);
}

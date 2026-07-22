using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace MyAtm.Api.Db.EntityFramework;

public sealed class MyAtmMonitorContext : MonitorDbContextBase
{
    public MyAtmMonitorContext(DbContextOptions<MyAtmMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<MyAtmDustLevelEntity> DustLevels => Set<MyAtmDustLevelEntity>();
    public DbSet<MyAtmAccessoryInfoEntity> AccessoryInfo => Set<MyAtmAccessoryInfoEntity>();
    public DbSet<MyAtmErrorMessageEntity> MyAtmErrorMessages => Set<MyAtmErrorMessageEntity>();
    public new DbSet<MyAtmAlertOccurrenceEntity> AlertOccurrences => Set<MyAtmAlertOccurrenceEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MyAtmDustLevelEntity>(entity =>
        {
            entity.ToTable(TableName("MyAtmDustLevels", "my_atm_dust_level"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime, row.Avrg });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.Avrg).HasColumnName(Column("Avrg", "avrg"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            entity.Property(row => row.Pm1).HasColumnName(Column("Pm1", "pm_1"));
            entity.Property(row => row.Pm2_5).HasColumnName(Column("Pm2_5", "pm_2_5"));
            entity.Property(row => row.Pm10).HasColumnName(Column("Pm10", "pm_10"));
            entity.Property(row => row.PmTotal).HasColumnName(Column("PmTotal", "pm_total"));
            entity.Property(row => row.Weather_t).HasColumnName(Column("Weather_t", "weather_t"));
            entity.Property(row => row.Weather_p).HasColumnName(Column("Weather_p", "weather_p"));
            entity.Property(row => row.Weather_rh).HasColumnName(Column("Weather_rh", "weather_rh"));
        });

        modelBuilder.Entity<MyAtmAccessoryInfoEntity>(entity =>
        {
            entity.ToTable(TableName("MyAtmAccessoryInfo", "my_atm_accessory_info"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            entity.Property(row => row.OperatingSpanPointDeviation).HasColumnName(Column("OperatingSpanPointDeviation", "operating_span_point_deviation"));
            entity.Property(row => row.OperatingTLed).HasColumnName(Column("OperatingTLed", "operating_t_led"));
            entity.Property(row => row.OperatingTHeating).HasColumnName(Column("OperatingTHeating", "operating_t_heating"));
            entity.Property(row => row.OperatingVolumeFlow).HasColumnName(Column("OperatingVolumeFlow", "operating_volume_flow"));
            entity.Property(row => row.OperatingVolumeFlowSignalLength).HasColumnName(Column("OperatingVolumeFlowSignalLength", "operating_volume_flow_signal_length"));
            entity.Property(row => row.OperatingVolumeFlowTimestamp).HasColumnName(Column("OperatingVolumeFlowTimestamp", "operating_volume_flow_time"));
            entity.Property(row => row.OperatingPeakPosition15s).HasColumnName(Column("OperatingPeakPosition15s", "operating_peak_position_15_s"));
            entity.Property(row => row.OperatingVelocity).HasColumnName(Column("OperatingVelocity", "operating_velocity"));
            entity.Property(row => row.OperatingSlaNoiseLevel).HasColumnName(Column("OperatingSlaNoiseLevel", "operating_sla_noise_level"));
            entity.Property(row => row.OperatingSlaOffsetAdjustmentVoltage).HasColumnName(Column("OperatingSlaOffsetAdjustmentVoltage", "operating_sla_offset_adjustment_voltage"));
            entity.Property(row => row.OperatingTMio).HasColumnName(Column("OperatingTMio", "operating_tmio"));
            entity.Property(row => row.OperatingPMio).HasColumnName(Column("OperatingPMio", "operating_pmio"));
            entity.Property(row => row.OperatingRHMio).HasColumnName(Column("OperatingRHMio", "operating_rh_mio"));
            entity.Property(row => row.OperatingAutoCalibrationPeakPosition).HasColumnName(Column("OperatingAutoCalibrationPeakPosition", "operating_auto_calibration_peak_position"));
            entity.Property(row => row.OperatingPowerLed).HasColumnName(Column("OperatingPowerLed", "operating_power_led"));
            entity.Property(row => row.OperatingPowerPmt).HasColumnName(Column("OperatingPowerPmt", "operating_power_pmt"));
            entity.Property(row => row.OperatingPowerHeating).HasColumnName(Column("OperatingPowerHeating", "operating_power_heating"));
            entity.Property(row => row.OperatingPowerVolumeFlowBlower).HasColumnName(Column("OperatingPowerVolumeFlowBlower", "operating_power_volume_flow_blower"));
            entity.Property(row => row.OperatingPowerHousingBlower).HasColumnName(Column("OperatingPowerHousingBlower", "operating_power_housing_blower"));
            entity.Property(row => row.OperatingPowerSeparatorBlower).HasColumnName(Column("OperatingPowerSeparatorBlower", "operating_power_separator_blower"));
            entity.Property(row => row.OperatingFlowCorrectionFactor).HasColumnName(Column("OperatingFlowCorrectionFactor", "operating_flow_correction_factor"));
            entity.Property(row => row.DigitalCalibrationEnableStatus).HasColumnName(Column("DigitalCalibrationEnableStatus", "digital_calibration_enable_status"));
            entity.Property(row => row.DigitalIadsConnected).HasColumnName(Column("DigitalIadsConnected", "digital_iads_connected"));
            entity.Property(row => row.DigitalIadsActivated).HasColumnName(Column("DigitalIadsActivated", "digital_iads_activated"));
            entity.Property(row => row.DigitalAmbientProtectionAttached).HasColumnName(Column("DigitalAmbientProtectionAttached", "digital_ambient_protection_attached"));
            entity.Property(row => row.DigitalCoincidence).HasColumnName(Column("DigitalCoincidence", "digital_coincidence"));
            entity.Property(row => row.DigitalWeatherStation).HasColumnName(Column("DigitalWeatherStation", "digital_weather_station"));
            entity.Property(row => row.DigitalOperatingModus).HasColumnName(Column("DigitalOperatingModus", "digital_operating_modus"));
            entity.Property(row => row.DigitalVolumeFlow).HasColumnName(Column("DigitalVolumeFlow", "digital_volume_flow"));
            entity.Property(row => row.DigitalSuction).HasColumnName(Column("DigitalSuction", "digital_suction"));
            entity.Property(row => row.DigitalIads).HasColumnName(Column("DigitalIads", "digital_iads"));
            entity.Property(row => row.DigitalCalibration).HasColumnName(Column("DigitalCalibration", "digital_calibration"));
            entity.Property(row => row.DigitalSensorLed).HasColumnName(Column("DigitalSensorLed", "digital_sensor_led"));
            entity.Property(row => row.DigitalSensorData).HasColumnName(Column("DigitalSensorData", "digital_sensor_data"));
            entity.Property(row => row.DigitalSensorNoise).HasColumnName(Column("DigitalSensorNoise", "digital_sensor_noise"));
            entity.Property(row => row.DigitalCountModus).HasColumnName(Column("DigitalCountModus", "digital_count_modus"));
            entity.Property(row => row.DigitalLiquidPumps).HasColumnName(Column("DigitalLiquidPumps", "digital_liquid_pumps"));
            entity.Property(row => row.DigitalCondensationCooling).HasColumnName(Column("DigitalCondensationCooling", "digital_condensation_cooling"));
            entity.Property(row => row.DigitalDropletSize).HasColumnName(Column("DigitalDropletSize", "digital_droplet_size"));
            entity.Property(row => row.DigitalOpticsTemperature).HasColumnName(Column("DigitalOpticsTemperature", "digital_optics_temperature"));
            entity.Property(row => row.DigitalGlobalWarning).HasColumnName(Column("DigitalGlobalWarning", "digital_global_warning"));
            entity.Property(row => row.DigitalGlobalError).HasColumnName(Column("DigitalGlobalError", "digital_global_error"));
            entity.Property(row => row.DigitalEvaporationHeating).HasColumnName(Column("DigitalEvaporationHeating", "digital_evaporation_heating"));
        });

        modelBuilder.Entity<MyAtmErrorMessageEntity>(entity =>
        {
            entity.ToTable(TableName("MyAtmErrorMessages", "my_atm_error_message"), Schema());
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName(Column("Tag", "tag"));
            entity.Property(row => row.Error).HasColumnName(Column("Error", "error"));
            entity.Property(row => row.ErrorTime).HasColumnName(Column("ErrorTime", "error_time"));
        });

        modelBuilder.Entity<MyAtmAlertOccurrenceEntity>(entity =>
        {
            entity.ToTable(TableName("MyAtmAlertOccurrences", "my_atm_alert_occurrence"), Schema());
            entity.HasKey(row => row.OccurrenceKey);
            entity.Property(row => row.OccurrenceKey).HasColumnName(Column("OccurrenceKey", "occurrence_key"));
            entity.Property(row => row.NotificationId).HasColumnName(Column("NotificationId", "notification_id"));
            entity.Property(row => row.MonitorId).HasColumnName(Column("MonitorId", "monitor_id"));
            entity.Property(row => row.RuleId).HasColumnName(Column("RuleId", "rule_id"));
            entity.Property(row => row.Period).HasColumnName(Column("Period", "period"));
            entity.Property(row => row.AlertType).HasColumnName(Column("AlertType", "alert_type"));
            entity.Property(row => row.Field).HasColumnName(Column("Field", "field"));
            entity.Property(row => row.Level).HasColumnName(Column("Level", "level"));
            entity.Property(row => row.TriggeredAt).HasColumnName(Column("TriggeredAt", "triggered_at"));
            entity.Property(row => row.IsSuppressed).HasColumnName(Column("IsSuppressed", "is_suppressed"));
            entity.Property(row => row.CreatedAt).HasColumnName(Column("CreatedAt", "created_at"));
        });

    }

    private string? Schema() => MonitorOptions.IsPostgreSql ? null : "dbo";

    private string TableName(string sqlServerName, string postgreSqlName)
    {
        if (!MonitorOptions.IsPostgreSql)
        {
            return sqlServerName;
        }

        return MonitorOptions.IdentifierMap.TryGetValue(sqlServerName, out var mapped)
            ? mapped.Trim('"')
            : postgreSqlName;
    }

    private string Column(string sqlServerName, string postgreSqlName) =>
        MonitorOptions.IsPostgreSql ? postgreSqlName : sqlServerName;
}

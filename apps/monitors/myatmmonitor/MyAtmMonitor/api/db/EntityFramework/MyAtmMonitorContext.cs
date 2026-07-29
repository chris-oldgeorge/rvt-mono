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
            entity.ToTable("my_atm_dust_level");
            entity.HasKey(row => new { row.SerialId, row.SampleTime, row.Avrg });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.Avrg).HasColumnName("avrg");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.Pm1).HasColumnName("pm_1");
            entity.Property(row => row.Pm2_5).HasColumnName("pm_2_5");
            entity.Property(row => row.Pm10).HasColumnName("pm_10");
            entity.Property(row => row.PmTotal).HasColumnName("pm_total");
            entity.Property(row => row.Weather_t).HasColumnName("weather_t");
            entity.Property(row => row.Weather_p).HasColumnName("weather_p");
            entity.Property(row => row.Weather_rh).HasColumnName("weather_rh");
        });

        modelBuilder.Entity<MyAtmAccessoryInfoEntity>(entity =>
        {
            entity.ToTable("my_atm_accessory_info");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.OperatingSpanPointDeviation).HasColumnName("operating_span_point_deviation");
            entity.Property(row => row.OperatingTLed).HasColumnName("operating_t_led");
            entity.Property(row => row.OperatingTHeating).HasColumnName("operating_t_heating");
            entity.Property(row => row.OperatingVolumeFlow).HasColumnName("operating_volume_flow");
            entity.Property(row => row.OperatingVolumeFlowSignalLength).HasColumnName("operating_volume_flow_signal_length");
            entity.Property(row => row.OperatingVolumeFlowTimestamp).HasColumnName("operating_volume_flow_time");
            entity.Property(row => row.OperatingPeakPosition15s).HasColumnName("operating_peak_position_15_s");
            entity.Property(row => row.OperatingVelocity).HasColumnName("operating_velocity");
            entity.Property(row => row.OperatingSlaNoiseLevel).HasColumnName("operating_sla_noise_level");
            entity.Property(row => row.OperatingSlaOffsetAdjustmentVoltage).HasColumnName("operating_sla_offset_adjustment_voltage");
            entity.Property(row => row.OperatingTMio).HasColumnName("operating_tmio");
            entity.Property(row => row.OperatingPMio).HasColumnName("operating_pmio");
            entity.Property(row => row.OperatingRHMio).HasColumnName("operating_rh_mio");
            entity.Property(row => row.OperatingAutoCalibrationPeakPosition).HasColumnName("operating_auto_calibration_peak_position");
            entity.Property(row => row.OperatingPowerLed).HasColumnName("operating_power_led");
            entity.Property(row => row.OperatingPowerPmt).HasColumnName("operating_power_pmt");
            entity.Property(row => row.OperatingPowerHeating).HasColumnName("operating_power_heating");
            entity.Property(row => row.OperatingPowerVolumeFlowBlower).HasColumnName("operating_power_volume_flow_blower");
            entity.Property(row => row.OperatingPowerHousingBlower).HasColumnName("operating_power_housing_blower");
            entity.Property(row => row.OperatingPowerSeparatorBlower).HasColumnName("operating_power_separator_blower");
            entity.Property(row => row.OperatingFlowCorrectionFactor).HasColumnName("operating_flow_correction_factor");
            entity.Property(row => row.DigitalCalibrationEnableStatus).HasColumnName("digital_calibration_enable_status");
            entity.Property(row => row.DigitalIadsConnected).HasColumnName("digital_iads_connected");
            entity.Property(row => row.DigitalIadsActivated).HasColumnName("digital_iads_activated");
            entity.Property(row => row.DigitalAmbientProtectionAttached).HasColumnName("digital_ambient_protection_attached");
            entity.Property(row => row.DigitalCoincidence).HasColumnName("digital_coincidence");
            entity.Property(row => row.DigitalWeatherStation).HasColumnName("digital_weather_station");
            entity.Property(row => row.DigitalOperatingModus).HasColumnName("digital_operating_modus");
            entity.Property(row => row.DigitalVolumeFlow).HasColumnName("digital_volume_flow");
            entity.Property(row => row.DigitalSuction).HasColumnName("digital_suction");
            entity.Property(row => row.DigitalIads).HasColumnName("digital_iads");
            entity.Property(row => row.DigitalCalibration).HasColumnName("digital_calibration");
            entity.Property(row => row.DigitalSensorLed).HasColumnName("digital_sensor_led");
            entity.Property(row => row.DigitalSensorData).HasColumnName("digital_sensor_data");
            entity.Property(row => row.DigitalSensorNoise).HasColumnName("digital_sensor_noise");
            entity.Property(row => row.DigitalCountModus).HasColumnName("digital_count_modus");
            entity.Property(row => row.DigitalLiquidPumps).HasColumnName("digital_liquid_pumps");
            entity.Property(row => row.DigitalCondensationCooling).HasColumnName("digital_condensation_cooling");
            entity.Property(row => row.DigitalDropletSize).HasColumnName("digital_droplet_size");
            entity.Property(row => row.DigitalOpticsTemperature).HasColumnName("digital_optics_temperature");
            entity.Property(row => row.DigitalGlobalWarning).HasColumnName("digital_global_warning");
            entity.Property(row => row.DigitalGlobalError).HasColumnName("digital_global_error");
            entity.Property(row => row.DigitalEvaporationHeating).HasColumnName("digital_evaporation_heating");
        });

        modelBuilder.Entity<MyAtmErrorMessageEntity>(entity =>
        {
            entity.ToTable("my_atm_error_message");
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName("tag");
            entity.Property(row => row.Error).HasColumnName("error");
            entity.Property(row => row.ErrorTime).HasColumnName("error_time");
        });

        modelBuilder.Entity<MyAtmAlertOccurrenceEntity>(entity =>
        {
            entity.ToTable("my_atm_alert_occurrence");
            entity.HasKey(row => row.OccurrenceKey);
            entity.Property(row => row.OccurrenceKey).HasColumnName("occurrence_key");
            entity.Property(row => row.NotificationId).HasColumnName("notification_id");
            entity.Property(row => row.MonitorId).HasColumnName("monitor_id");
            entity.Property(row => row.RuleId).HasColumnName("rule_id");
            entity.Property(row => row.Period).HasColumnName("period");
            entity.Property(row => row.AlertType).HasColumnName("alert_type");
            entity.Property(row => row.Field).HasColumnName("field");
            entity.Property(row => row.Level).HasColumnName("level");
            entity.Property(row => row.TriggeredAt).HasColumnName("triggered_at");
            entity.Property(row => row.IsSuppressed).HasColumnName("is_suppressed");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
        });
    }
}

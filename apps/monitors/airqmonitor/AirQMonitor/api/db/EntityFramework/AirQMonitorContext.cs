using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace AirQ.Api.Db.EntityFramework;

public sealed class AirQMonitorContext : MonitorDbContextBase
{
    public AirQMonitorContext(DbContextOptions<AirQMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<AirQNoiseLevelEntity> NoiseLevels => Set<AirQNoiseLevelEntity>();
    public DbSet<AirQMonitorStatusEntity> MonitorStatuses => Set<AirQMonitorStatusEntity>();
    public DbSet<AirQErrorMessageEntity> AirQErrorMessages => Set<AirQErrorMessageEntity>();
    public DbSet<AirQNoise8HourAverageEntity> Noise8HourAverages => Set<AirQNoise8HourAverageEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AirQNoiseLevelEntity>(entity =>
        {
            entity.ToTable("air_q_noise_level");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.LAeq).HasColumnName("laeq");
            entity.Property(row => row.LAmax).HasColumnName("lamax");
            entity.Property(row => row.LA90).HasColumnName("la_90");
            entity.Property(row => row.LA10).HasColumnName("la_10");
            entity.Property(row => row.LCeq).HasColumnName("lceq");
            entity.Property(row => row.LCmax).HasColumnName("lcmax");
            entity.Property(row => row.LC90).HasColumnName("lc_90");
            entity.Property(row => row.LC10).HasColumnName("lc_10");
        });

        modelBuilder.Entity<AirQMonitorStatusEntity>(entity =>
        {
            entity.ToTable("air_q_monitor_status");
            entity.HasKey(row => row.SerialId);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.UpdateTime).HasColumnName("update_time");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.ErrorCount).HasColumnName("error_count");
            entity.Property(row => row.BatteryVoltage).HasColumnName("battery_voltage");
            entity.Property(row => row.CalibrationDate).HasColumnName("calibration_date");
            entity.Property(row => row.FilterChangeDate).HasColumnName("filter_change_date");
            entity.Property(row => row.PumpHours).HasColumnName("pump_hours");
        });

        modelBuilder.Entity<AirQErrorMessageEntity>(entity =>
        {
            entity.ToTable("air_q_error_message");
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName("tag");
            entity.Property(row => row.Error).HasColumnName("error");
            entity.Property(row => row.ErrorTime).HasColumnName("error_time");
        });

        modelBuilder.Entity<AirQNoise8HourAverageEntity>(entity =>
        {
            entity.ToTable("air_q_noise_8_hour_average");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.LAeq).HasColumnName("laeq");
            entity.Property(row => row.LAmax).HasColumnName("lamax");
            entity.Property(row => row.LA90).HasColumnName("la_90");
            entity.Property(row => row.LA10).HasColumnName("la_10");
            entity.Property(row => row.LCeq).HasColumnName("lceq");
            entity.Property(row => row.LCmax).HasColumnName("lcmax");
            entity.Property(row => row.LC90).HasColumnName("lc_90");
            entity.Property(row => row.LC10).HasColumnName("lc_10");
            entity.Property(row => row.NumberOfSamples).HasColumnName("number_of_samples");
        });
    }
}

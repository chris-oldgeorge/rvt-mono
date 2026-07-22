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
            entity.ToTable(TableName("AirQNoiseLevels", "air_q_noise_level"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            entity.Property(row => row.LAeq).HasColumnName(Column("LAeq", "laeq"));
            entity.Property(row => row.LAmax).HasColumnName(Column("LAmax", "lamax"));
            entity.Property(row => row.LA90).HasColumnName(Column("LA90", "la_90"));
            entity.Property(row => row.LA10).HasColumnName(Column("LA10", "la_10"));
            entity.Property(row => row.LCeq).HasColumnName(Column("LCeq", "lceq"));
            entity.Property(row => row.LCmax).HasColumnName(Column("LCmax", "lcmax"));
            entity.Property(row => row.LC90).HasColumnName(Column("LC90", "lc_90"));
            entity.Property(row => row.LC10).HasColumnName(Column("LC10", "lc_10"));
        });

        modelBuilder.Entity<AirQMonitorStatusEntity>(entity =>
        {
            entity.ToTable(TableName("AirQMonitorStatus", "air_q_monitor_status"), Schema());
            entity.HasKey(row => row.SerialId);
            if (MonitorOptions.IsPostgreSql)
            {
                entity.Property(row => row.Id).HasColumnName("id");
            }
            else
            {
                entity.Ignore(row => row.Id);
            }

            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.UpdateTime).HasColumnName(Column("UpdateTime", "update_time"));
            entity.Property(row => row.Status).HasColumnName(Column("Status", "status"));
            entity.Property(row => row.ErrorCount).HasColumnName(Column("ErrorCount", "error_count"));
            entity.Property(row => row.BatteryVoltage).HasColumnName(Column("BatteryVoltage", "battery_voltage"));
            entity.Property(row => row.CalibrationDate).HasColumnName(Column("CalibrationDate", "calibration_date"));
            entity.Property(row => row.FilterChangeDate).HasColumnName(Column("FilterChangeDate", "filter_change_date"));
            entity.Property(row => row.PumpHours).HasColumnName(Column("PumpHours", "pump_hours"));
        });

        modelBuilder.Entity<AirQErrorMessageEntity>(entity =>
        {
            entity.ToTable(TableName("AirQErrorMessages", "air_q_error_message"), Schema());
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName(Column("Tag", "tag"));
            entity.Property(row => row.Error).HasColumnName(Column("Error", "error"));
            entity.Property(row => row.ErrorTime).HasColumnName(Column("ErrorTime", "error_time"));
        });

        modelBuilder.Entity<AirQNoise8HourAverageEntity>(entity =>
        {
            entity.ToTable(TableName("AirQNoise8HourAverage", "air_q_noise_8_hour_average"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            entity.Property(row => row.LAeq).HasColumnName(Column("LAeq", "laeq"));
            entity.Property(row => row.LAmax).HasColumnName(Column("LAmax", "lamax"));
            entity.Property(row => row.LA90).HasColumnName(Column("LA90", "la_90"));
            entity.Property(row => row.LA10).HasColumnName(Column("LA10", "la_10"));
            entity.Property(row => row.LCeq).HasColumnName(Column("LCeq", "lceq"));
            entity.Property(row => row.LCmax).HasColumnName(Column("LCmax", "lcmax"));
            entity.Property(row => row.LC90).HasColumnName(Column("LC90", "lc_90"));
            entity.Property(row => row.LC10).HasColumnName(Column("LC10", "lc_10"));
            entity.Property(row => row.NumberOfSamples).HasColumnName(Column("NumberOfSamples", "number_of_samples"));
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Svantek.Api.Db.EntityFramework;

public sealed class SvantekMonitorContext : MonitorDbContextBase
{
    public SvantekMonitorContext(DbContextOptions<SvantekMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<SvantekMonitorStatusEntity> SvantekMonitorStatus => Set<SvantekMonitorStatusEntity>();
    public DbSet<SvantekNoiseLevelEntity> NoiseLevels => Set<SvantekNoiseLevelEntity>();
    public DbSet<SvantekNoise8HourAverageEntity> Noise8HourAverages => Set<SvantekNoise8HourAverageEntity>();
    public DbSet<SvantekErrorMessageEntity> SvantekErrorMessages => Set<SvantekErrorMessageEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            if (MonitorOptions.IsPostgreSql)
            {
                entity.Ignore(row => row.What2words);
                entity.Property(row => row.What3Words).HasColumnName("what_3_words");
            }
            else
            {
                entity.Ignore(row => row.What3Words);
            }
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            var recordingLink = entity.Property<string?>("RecordingLink").HasColumnName(Column("RecordingLink", "recording_link"));
            recordingLink.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            recordingLink.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        });

        modelBuilder.Entity<SvantekMonitorStatusEntity>(entity =>
        {
            entity.ToTable(TableName("SvantekMonitorStatus", "svantek_monitor_status"), Schema());
            entity.HasKey(row => row.SerialId);
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.UpdateTime).HasColumnName(Column("UpdateTime", "update_time"));
            entity.Property(row => row.Status).HasColumnName(Column("Status", "status"));
            entity.Property(row => row.ErrorCount).HasColumnName(Column("ErrorCount", "error_count"));
            entity.Property(row => row.BatteryVoltage).HasColumnName(Column("BatteryVoltage", "battery_voltage"));
            entity.Property(row => row.CalibrationDate).HasColumnName(Column("CalibrationDate", "calibration_date"));
            entity.Property(row => row.FilterChangeDate).HasColumnName(Column("FilterChangeDate", "filter_change_date"));
            entity.Property(row => row.PumpHours).HasColumnName(Column("PumpHours", "pump_hours"));
            entity.Property(row => row.ProjectId).HasColumnName("project_id");
            entity.Property(row => row.PointId).HasColumnName("point_id");
            entity.Property(row => row.Active).HasColumnName("active");
            entity.Property(row => row.LastLogin).HasColumnName("lastlogin");
            entity.Property(row => row.LastLogout).HasColumnName("lastlogout");
            entity.Property(row => row.IsOnline).HasColumnName("isonline");
            entity.Property(row => row.LastStatusTimestamp).HasColumnName("laststatustimestamp");
            entity.Property(row => row.BatteryCharge).HasColumnName("batterycharge");
            entity.Property(row => row.BatteryTimeToEmpty).HasColumnName("batterytimetoempty");
            entity.Property(row => row.PowerSource).HasColumnName("powersource");
            entity.Property(row => row.IsBatteryCharging).HasColumnName("isbatterycharging");
            entity.Property(row => row.GsmSignalQuality).HasColumnName("gsmsignalquality");
            entity.Property(row => row.MeasurementState).HasColumnName("measurementstate");

            if (MonitorOptions.IsPostgreSql)
            {
                entity.Property(row => row.Active).HasConversion(v => ToText(v), v => FromText(v));
                entity.Property(row => row.IsOnline).HasConversion(v => ToText(v), v => FromText(v));
                entity.Property(row => row.IsBatteryCharging).HasConversion(v => ToText(v), v => FromText(v));
            }
        });

        modelBuilder.Entity<SvantekNoiseLevelEntity>(entity =>
        {
            entity.ToTable(TableName("SvantekNoiseLevels", "svantek_noise_level"), Schema());
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

        modelBuilder.Entity<SvantekNoise8HourAverageEntity>(entity =>
        {
            entity.ToTable(TableName("SvantekNoise8HourAverage", "svantek_noise_8_hour_average"), Schema());
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

        modelBuilder.Entity<SvantekErrorMessageEntity>(entity =>
        {
            entity.ToTable(TableName("SvantekErrorMessages", "svantek_error_message"), Schema());
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName(Column("Tag", "tag"));
            entity.Property(row => row.Error).HasColumnName(Column("Error", "error"));
            entity.Property(row => row.ErrorTime).HasColumnName(Column("ErrorTime", "error_time"));
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

    private static string? ToText(bool? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value ? "1" : "0";
    }

    private static bool? FromText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized == "1")
        {
            return true;
        }

        if (normalized == "0")
        {
            return false;
        }

        if (bool.TryParse(normalized, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"String '{value}' was not recognized as a valid Boolean.");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Svantek.Api.Db.EntityFramework;

public sealed class SvantekMonitorContext(DbContextOptions<SvantekMonitorContext> options, MonitorDbOptions monitorOptions) : MonitorDbContextBase(options, monitorOptions)
{
    public DbSet<SvantekMonitorStatusEntity> SvantekMonitorStatus => Set<SvantekMonitorStatusEntity>();
    public DbSet<SvantekNoiseLevelEntity> NoiseLevels => Set<SvantekNoiseLevelEntity>();
    public DbSet<SvantekNoise8HourAverageEntity> Noise8HourAverages => Set<SvantekNoise8HourAverageEntity>();
    public DbSet<SvantekErrorMessageEntity> SvantekErrorMessages => Set<SvantekErrorMessageEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            entity.Ignore(row => row.What2words);
            entity.Property(row => row.What3Words).HasColumnName("what_3_words");
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            PropertyBuilder<string?> recordingLink = entity.Property<string?>("RecordingLink").HasColumnName("recording_link");
            recordingLink.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            recordingLink.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        });

        modelBuilder.Entity<SvantekMonitorStatusEntity>(entity =>
        {
            entity.ToTable("svantek_monitor_status");
            entity.HasKey(row => row.SerialId);
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.UpdateTime).HasColumnName("update_time");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.ErrorCount).HasColumnName("error_count");
            entity.Property(row => row.BatteryVoltage).HasColumnName("battery_voltage");
            entity.Property(row => row.CalibrationDate).HasColumnName("calibration_date");
            entity.Property(row => row.FilterChangeDate).HasColumnName("filter_change_date");
            entity.Property(row => row.PumpHours).HasColumnName("pump_hours");
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

            entity.Property(row => row.Active).HasConversion(v => ToText(v), v => FromText(v));
            entity.Property(row => row.IsOnline).HasConversion(v => ToText(v), v => FromText(v));
            entity.Property(row => row.IsBatteryCharging).HasConversion(v => ToText(v), v => FromText(v));
        });

        modelBuilder.Entity<SvantekNoiseLevelEntity>(entity =>
        {
            entity.ToTable("svantek_noise_level");
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

        modelBuilder.Entity<SvantekNoise8HourAverageEntity>(entity =>
        {
            entity.ToTable("svantek_noise_8_hour_average");
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

        modelBuilder.Entity<SvantekErrorMessageEntity>(entity =>
        {
            entity.ToTable("svantek_error_message");
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName("tag");
            entity.Property(row => row.Error).HasColumnName("error");
            entity.Property(row => row.ErrorTime).HasColumnName("error_time");
        });
    }

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

        string normalized = value.Trim();
        if (normalized == "1")
        {
            return true;
        }

        if (normalized == "0")
        {
            return false;
        }

        if (bool.TryParse(normalized, out bool parsed))
        {
            return parsed;
        }

        throw new FormatException($"String '{value}' was not recognized as a valid Boolean.");
    }
}

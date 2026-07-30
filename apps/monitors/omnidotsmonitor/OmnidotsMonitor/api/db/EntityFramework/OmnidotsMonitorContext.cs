using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Omnidots.Api.Db.EntityFramework;

public sealed class OmnidotsMonitorContext : MonitorDbContextBase
{
    public OmnidotsMonitorContext(DbContextOptions<OmnidotsMonitorContext> options, MonitorDbOptions monitorOptions)
        : base(options, monitorOptions)
    {
    }

    public DbSet<OmnidotsMonitorStatusEntity> MonitorStatuses => Set<OmnidotsMonitorStatusEntity>();
    public DbSet<OmnidotsSensorEntity> Sensors => Set<OmnidotsSensorEntity>();
    public DbSet<OmnidotsPeakLevelEntity> PeakLevels => Set<OmnidotsPeakLevelEntity>();
    public DbSet<OmnidotsVeffLevelEntity> VeffLevels => Set<OmnidotsVeffLevelEntity>();
    public DbSet<OmnidotsVdvLevelEntity> VdvLevels => Set<OmnidotsVdvLevelEntity>();
    public DbSet<OmnidotsErrorMessageEntity> OmnidotsErrorMessages => Set<OmnidotsErrorMessageEntity>();
    public DbSet<OmnidotsTraceIndexEntity> TraceIndexes => Set<OmnidotsTraceIndexEntity>();
    public DbSet<OmnidotsImportCursorEntity> ImportCursors => Set<OmnidotsImportCursorEntity>();
    public DbSet<OmnidotsTraceEntity> Traces => Set<OmnidotsTraceEntity>();

    protected override void OnMonitorModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rvt.Monitor.Common.Data.Entities.MonitorEntity>(entity =>
        {
            entity.Property(row => row.FleetNr).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<OmnidotsMonitorStatusEntity>(entity =>
        {
            entity.ToTable("omnidots_monitor_status");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.SerialId)
                .HasDatabaseName("ix_omnidots_monitor_status_serial_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.MeasurementDuration).HasColumnName("measurement_duration");
            entity.Property(row => row.DataSaveLevel).HasColumnName("data_save_level");
            entity.Property(row => row.VdvEnabled).HasColumnName("vdv_enabled");
            entity.Property(row => row.VdvX).HasColumnName("vdv_x");
            entity.Property(row => row.VdvY).HasColumnName("vdv_y");
            entity.Property(row => row.VdvZ).HasColumnName("vdv_z");
            entity.Property(row => row.VdvPeriod).HasColumnName("vdv_period");
            entity.Property(row => row.TraceSaveLevel).HasColumnName("trace_save_level");
            entity.Property(row => row.TracePreTrigger).HasColumnName("trace_pre_trigger");
            entity.Property(row => row.TracePostTrigger).HasColumnName("trace_post_trigger");
            entity.Property(row => row.AlarmValue).HasColumnName("alarm_value");
            entity.Property(row => row.FlatLevel).HasColumnName("flat_level");
            entity.Property(row => row.DisableLed).HasColumnName("disable_led");
            entity.Property(row => row.LogFlushInterval).HasColumnName("log_flush_interval");
            entity.Property(row => row.GuideLine).HasColumnName("guide_line");
            entity.Property(row => row.BuildingLevel).HasColumnName("building_level");
            entity.Property(row => row.VectorEnabled).HasColumnName("vector_enabled");
            entity.Property(row => row.AtopEnabled).HasColumnName("atop_enabled");
            entity.Property(row => row.VtopEnabled).HasColumnName("vtop_enabled");
        });

        modelBuilder.Entity<OmnidotsSensorEntity>(entity =>
        {
            entity.ToTable("omnidots_sensor");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.SerialId)
                .HasDatabaseName("ix_omnidots_sensor_serial_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.Name).HasColumnName("name");
            entity.Property(row => row.Lastseen).HasColumnName("lastseen");
            entity.Property(row => row.BatteryCharge).HasColumnName("battery_charge");
            entity.Property(row => row.ConnectedUsing).HasColumnName("connected_using");
            entity.Property(row => row.Online).HasColumnName("online");
        });

        modelBuilder.Entity<OmnidotsPeakLevelEntity>(entity =>
        {
            entity.ToTable("omnidots_peak_level");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.XFdom).HasColumnName("x_fdom");
            entity.Property(row => row.XVtop).HasColumnName("x_vtop");
            entity.Property(row => row.XVtopOverflow).HasColumnName("x_vtop_overflow");
            entity.Property(row => row.YFdom).HasColumnName("y_fdom");
            entity.Property(row => row.YVtop).HasColumnName("y_vtop");
            entity.Property(row => row.YVtopOverflow).HasColumnName("y_vtop_overflow");
            entity.Property(row => row.ZFdom).HasColumnName("z_fdom");
            entity.Property(row => row.ZVtop).HasColumnName("z_vtop");
            entity.Property(row => row.ZVtopOverflow).HasColumnName("z_vtop_overflow");
        });

        modelBuilder.Entity<OmnidotsVeffLevelEntity>(entity =>
        {
            entity.ToTable("omnidots_veff_level");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.X).HasColumnName("x");
            entity.Property(row => row.Y).HasColumnName("y");
            entity.Property(row => row.Z).HasColumnName("z");
        });

        modelBuilder.Entity<OmnidotsVdvLevelEntity>(entity =>
        {
            entity.ToTable("omnidots_vdv_level");
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.SampleTime).HasColumnName("sample_time");
            entity.Property(row => row.X).HasColumnName("x");
            entity.Property(row => row.Y).HasColumnName("y");
            entity.Property(row => row.Z).HasColumnName("z");
            entity.Property(row => row.VdvX).HasColumnName("vdv_x");
            entity.Property(row => row.VdvY).HasColumnName("vdv_y");
            entity.Property(row => row.VdvZ).HasColumnName("vdv_z");
        });

        modelBuilder.Entity<OmnidotsErrorMessageEntity>(entity =>
        {
            entity.ToTable("omnidots_error_message");
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName("tag");
            entity.Property(row => row.Error).HasColumnName("error");
            entity.Property(row => row.ErrorTime).HasColumnName("error_time");
        });

        modelBuilder.Entity<OmnidotsTraceIndexEntity>(entity =>
        {
            entity.ToTable("omnidots_trace_index");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.SerialId).HasColumnName("serial_id");
            entity.Property(row => row.StartTime).HasColumnName("start_time");
            entity.Property(row => row.EndTime).HasColumnName("end_time");
        });

        modelBuilder.Entity<OmnidotsImportCursorEntity>(entity =>
        {
            entity.ToTable(
                "omnidots_import_cursor",
                table => table.HasCheckConstraint(
                    "ck_omnidots_import_cursor_series",
                    "\"series\" IN ('Peak', 'Veff', 'Vdv')"));
            entity.HasKey(row => new { row.SerialId, row.Series });
            entity.Property(row => row.SerialId)
                .HasColumnName("serial_id")
                .HasColumnType("text");
            entity.Property(row => row.Series)
                .HasColumnName("series")
                .HasColumnType("text");
            ConfigureUtcCursorInstant(entity.Property(row => row.LastSampleAt), "last_sample_at");
            ConfigureUtcCursorInstant(entity.Property(row => row.UpdatedAt), "updated_at");
        });

        modelBuilder.Entity<OmnidotsTraceEntity>(entity =>
        {
            entity.ToTable("omnidots_trace");
            entity.HasKey(row => new { row.TraceId, row.SampleIndex });
            entity.HasOne<OmnidotsTraceIndexEntity>()
                .WithMany()
                .HasForeignKey(row => row.TraceId)
                .OnDelete(DeleteBehavior.Cascade);
            // The portal owns this table's canonical naming (see
            // docs/database/omnidots-trace-ownership.md). Its cutover named the trace-index
            // foreign key omnidots_trace_index_id; the monitor conforms to that name.
            entity.Property(row => row.TraceId).HasColumnName("omnidots_trace_index_id");
            entity.Property(row => row.SampleIndex)
                .HasColumnName("sample_index")
                .HasColumnType("integer");
            entity.Property(row => row.X).HasColumnName("x");
            entity.Property(row => row.Y).HasColumnName("y");
            entity.Property(row => row.Z).HasColumnName("z");
        });
    }

    private void ConfigureUtcCursorInstant(
        PropertyBuilder<DateTime> property,
        string column)
    {
        property
            .HasColumnName(column)
            .HasColumnType("timestamp with time zone")
            .HasConversion(UtcDateTimeConverter.Instance);
    }

    private static class UtcDateTimeConverter
    {
        public static ValueConverter<DateTime, DateTime> Instance { get; } = new(
            value => value.Kind == DateTimeKind.Utc
                ? value
                : value.Kind == DateTimeKind.Local
                    ? value.ToUniversalTime()
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => value.Kind == DateTimeKind.Utc
                ? value
                : value.Kind == DateTimeKind.Local
                    ? value.ToUniversalTime()
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

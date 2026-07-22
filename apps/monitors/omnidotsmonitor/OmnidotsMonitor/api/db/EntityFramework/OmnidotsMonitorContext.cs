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
            if (!MonitorOptions.IsPostgreSql)
            {
                entity.ToTable("MonitorsList", "dbo", table => table.HasTrigger("tr_MonitorsList_DefaultDeployment"));
            }

            entity.Property(row => row.FleetNr).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<OmnidotsMonitorStatusEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsMonitorStatus", "omnidots_monitor_status"), Schema());
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.SerialId);
            entity.Property(row => row.Id).HasColumnName(Column("Id", "id"));
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.MeasurementDuration).HasColumnName(Column("MeasurementDuration", "measurement_duration"));
            entity.Property(row => row.DataSaveLevel).HasColumnName(Column("DataSaveLevel", "data_save_level"));
            entity.Property(row => row.VdvEnabled).HasColumnName(Column("VdvEnabled", "vdv_enabled"));
            entity.Property(row => row.VdvX).HasColumnName(Column("VdvX", "vdv_x"));
            entity.Property(row => row.VdvY).HasColumnName(Column("VdvY", "vdv_y"));
            entity.Property(row => row.VdvZ).HasColumnName(Column("VdvZ", "vdv_z"));
            entity.Property(row => row.VdvPeriod).HasColumnName(Column("VdvPeriod", "vdv_period"));
            entity.Property(row => row.TraceSaveLevel).HasColumnName(Column("TraceSaveLevel", "trace_save_level"));
            entity.Property(row => row.TracePreTrigger).HasColumnName(Column("TracePreTrigger", "trace_pre_trigger"));
            entity.Property(row => row.TracePostTrigger).HasColumnName(Column("TracePostTrigger", "trace_post_trigger"));
            entity.Property(row => row.AlarmValue).HasColumnName(Column("AlarmValue", "alarm_value"));
            entity.Property(row => row.FlatLevel).HasColumnName(Column("FlatLevel", "flat_level"));
            entity.Property(row => row.DisableLed).HasColumnName(Column("DisableLed", "disable_led"));
            entity.Property(row => row.LogFlushInterval).HasColumnName(Column("LogFlushInterval", "log_flush_interval"));
            entity.Property(row => row.GuideLine).HasColumnName(Column("GuideLine", "guide_line"));
            entity.Property(row => row.BuildingLevel).HasColumnName(Column("BuildingLevel", "building_level"));
            entity.Property(row => row.VectorEnabled).HasColumnName(Column("VectorEnabled", "vector_enabled"));
            entity.Property(row => row.AtopEnabled).HasColumnName(Column("AtopEnabled", "atop_enabled"));
            entity.Property(row => row.VtopEnabled).HasColumnName(Column("VtopEnabled", "vtop_enabled"));
        });

        modelBuilder.Entity<OmnidotsSensorEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsSensors", "omnidots_sensor"), Schema());
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.SerialId);
            entity.Property(row => row.Id).HasColumnName(Column("Id", "id"));
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.Name).HasColumnName(Column("Name", "name"));
            entity.Property(row => row.Lastseen).HasColumnName(Column("Lastseen", "lastseen"));
            entity.Property(row => row.BatteryCharge).HasColumnName(Column("BatteryCharge", "battery_charge"));
            entity.Property(row => row.ConnectedUsing).HasColumnName(Column("ConnectedUsing", "connected_using"));
            entity.Property(row => row.Online).HasColumnName(Column("Online", "online"));
        });

        modelBuilder.Entity<OmnidotsPeakLevelEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsPeakLevels", "omnidots_peak_level"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            ApplySqlServerDateTime(entity.Property(row => row.SampleTime));
            entity.Property(row => row.XFdom).HasColumnName(Column("XFdom", "x_fdom"));
            entity.Property(row => row.XVtop).HasColumnName(Column("XVtop", "x_vtop"));
            entity.Property(row => row.XVtopOverflow).HasColumnName(Column("XVtopOverflow", "x_vtop_overflow"));
            entity.Property(row => row.YFdom).HasColumnName(Column("YFdom", "y_fdom"));
            entity.Property(row => row.YVtop).HasColumnName(Column("YVtop", "y_vtop"));
            entity.Property(row => row.YVtopOverflow).HasColumnName(Column("YVtopOverflow", "y_vtop_overflow"));
            entity.Property(row => row.ZFdom).HasColumnName(Column("ZFdom", "z_fdom"));
            entity.Property(row => row.ZVtop).HasColumnName(Column("ZVtop", "z_vtop"));
            entity.Property(row => row.ZVtopOverflow).HasColumnName(Column("ZVtopOverflow", "z_vtop_overflow"));
        });

        modelBuilder.Entity<OmnidotsVeffLevelEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsVeffLevels", "omnidots_veff_level"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            ApplySqlServerDateTime(entity.Property(row => row.SampleTime));
            entity.Property(row => row.X).HasColumnName(Column("X", "x"));
            entity.Property(row => row.Y).HasColumnName(Column("Y", "y"));
            entity.Property(row => row.Z).HasColumnName(Column("Z", "z"));
        });

        modelBuilder.Entity<OmnidotsVdvLevelEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsVdvLevels", "omnidots_vdv_level"), Schema());
            entity.HasKey(row => new { row.SerialId, row.SampleTime });
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.SampleTime).HasColumnName(Column("SampleTime", "sample_time"));
            ApplySqlServerDateTime(entity.Property(row => row.SampleTime));
            entity.Property(row => row.X).HasColumnName(Column("X", "x"));
            entity.Property(row => row.Y).HasColumnName(Column("Y", "y"));
            entity.Property(row => row.Z).HasColumnName(Column("Z", "z"));
            entity.Property(row => row.VdvX).HasColumnName(Column("VdvX", "vdv_x"));
            entity.Property(row => row.VdvY).HasColumnName(Column("VdvY", "vdv_y"));
            entity.Property(row => row.VdvZ).HasColumnName(Column("VdvZ", "vdv_z"));
        });

        modelBuilder.Entity<OmnidotsErrorMessageEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsErrorMessages", "omnidots_error_message"), Schema());
            entity.HasKey(row => new { row.Tag, row.ErrorTime, row.Error });
            entity.Property(row => row.Tag).HasColumnName(Column("Tag", "tag"));
            entity.Property(row => row.Error).HasColumnName(Column("Error", "error"));
            entity.Property(row => row.ErrorTime).HasColumnName(Column("ErrorTime", "error_time"));
        });

        modelBuilder.Entity<OmnidotsTraceIndexEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsTracesIndex", "omnidots_trace_index"), Schema());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName(Column("Id", "id"));
            entity.Property(row => row.SerialId).HasColumnName(Column("SerialId", "serial_id"));
            entity.Property(row => row.StartTime).HasColumnName(Column("StartTime", "start_time"));
            entity.Property(row => row.EndTime).HasColumnName(Column("EndTime", "end_time"));
        });

        modelBuilder.Entity<OmnidotsImportCursorEntity>(entity =>
        {
            entity.ToTable(
                TableName("OmnidotsImportCursor", "omnidots_import_cursor"),
                Schema(),
                table => table.HasCheckConstraint(
                    MonitorOptions.IsPostgreSql
                        ? "ck_omnidots_import_cursor_series"
                        : "CK_OmnidotsImportCursor_Series",
                    MonitorOptions.IsPostgreSql
                        ? "\"series\" IN ('Peak', 'Veff', 'Vdv')"
                        : "([Series] COLLATE Latin1_General_100_BIN2 = N'Peak' AND DATALENGTH([Series]) = DATALENGTH(N'Peak')) OR " +
                          "([Series] COLLATE Latin1_General_100_BIN2 = N'Veff' AND DATALENGTH([Series]) = DATALENGTH(N'Veff')) OR " +
                          "([Series] COLLATE Latin1_General_100_BIN2 = N'Vdv' AND DATALENGTH([Series]) = DATALENGTH(N'Vdv'))"));
            entity.HasKey(row => new { row.SerialId, row.Series });
            entity.Property(row => row.SerialId)
                .HasColumnName(Column("SerialId", "serial_id"))
                .HasColumnType(MonitorOptions.IsPostgreSql ? "text" : "nvarchar(128)");
            entity.Property(row => row.Series)
                .HasColumnName(Column("Series", "series"))
                .HasColumnType(MonitorOptions.IsPostgreSql ? "text" : "nvarchar(16)");
            ConfigureUtcCursorInstant(entity.Property(row => row.LastSampleAt), "LastSampleAt", "last_sample_at");
            ConfigureUtcCursorInstant(entity.Property(row => row.UpdatedAt), "UpdatedAt", "updated_at");
        });

        modelBuilder.Entity<OmnidotsTraceEntity>(entity =>
        {
            entity.ToTable(TableName("OmnidotsTraces", "omnidots_trace"), Schema());
            entity.HasKey(row => new { row.TraceId, row.SampleIndex });
            entity.HasOne<OmnidotsTraceIndexEntity>()
                .WithMany()
                .HasForeignKey(row => row.TraceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(row => row.TraceId).HasColumnName(Column("TraceId", "trace_id"));
            entity.Property(row => row.SampleIndex)
                .HasColumnName(Column("SampleIndex", "sample_index"))
                .HasColumnType(MonitorOptions.IsPostgreSql ? "integer" : "int");
            entity.Property(row => row.X).HasColumnName(Column("X", "x"));
            entity.Property(row => row.Y).HasColumnName(Column("Y", "y"));
            entity.Property(row => row.Z).HasColumnName(Column("Z", "z"));
        });
    }

    internal string? Schema() => MonitorOptions.IsPostgreSql ? null : "dbo";

    internal string TableName(string sqlServerName, string postgreSqlName)
    {
        if (!MonitorOptions.IsPostgreSql)
        {
            return sqlServerName;
        }

        return MonitorOptions.IdentifierMap.TryGetValue(sqlServerName, out var mapped)
            ? mapped.Trim('"')
            : postgreSqlName;
    }

    internal string Column(string sqlServerName, string postgreSqlName) =>
        MonitorOptions.IsPostgreSql ? postgreSqlName : sqlServerName;

    private void ConfigureUtcCursorInstant(
        PropertyBuilder<DateTime> property,
        string sqlServerColumn,
        string postgreSqlColumn)
    {
        property
            .HasColumnName(Column(sqlServerColumn, postgreSqlColumn))
            .HasColumnType(MonitorOptions.IsPostgreSql ? "timestamp with time zone" : "datetime2")
            .HasConversion(UtcDateTimeConverter.Instance);
    }

    private void ApplySqlServerDateTime(PropertyBuilder<DateTime> property)
    {
        if (!MonitorOptions.IsPostgreSql)
        {
            property.HasColumnType("datetime");
        }
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

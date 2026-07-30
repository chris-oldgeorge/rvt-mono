using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Omnidots.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace OmnidotsAdapterTests.EntityFramework;

[TestClass]
public sealed class OmnidotsModelMappingTests
{
    [TestMethod]
    [DataRow(typeof(OmnidotsMonitorStatusEntity), "omnidots_monitor_status")]
    [DataRow(typeof(OmnidotsSensorEntity), "omnidots_sensor")]
    [DataRow(typeof(OmnidotsPeakLevelEntity), "omnidots_peak_level")]
    [DataRow(typeof(OmnidotsVeffLevelEntity), "omnidots_veff_level")]
    [DataRow(typeof(OmnidotsVdvLevelEntity), "omnidots_vdv_level")]
    [DataRow(typeof(OmnidotsErrorMessageEntity), "omnidots_error_message")]
    [DataRow(typeof(OmnidotsTraceIndexEntity), "omnidots_trace_index")]
    [DataRow(typeof(OmnidotsImportCursorEntity), "omnidots_import_cursor")]
    [DataRow(typeof(OmnidotsTraceEntity), "omnidots_trace")]
    public void OmnidotsContext_MapsCanonicalMonitorTablesWithoutSchemas(Type entityClrType, string tableName)
    {
        using OmnidotsMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(entity);
        Assert.AreEqual(tableName, entity.GetTableName());
        Assert.IsNull(entity.GetSchema());
    }

    [TestMethod]
    public void OmnidotsContext_MapsCanonicalColumnsAndTimestampTypes()
    {
        using OmnidotsMonitorContext context = CreateContext();

        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsMonitorStatusEntity))!,
            ("Id", "id"),
            ("SerialId", "serial_id"),
            ("MeasurementDuration", "measurement_duration"),
            ("DataSaveLevel", "data_save_level"),
            ("VdvEnabled", "vdv_enabled"),
            ("VdvX", "vdv_x"),
            ("VdvY", "vdv_y"),
            ("VdvZ", "vdv_z"),
            ("VdvPeriod", "vdv_period"),
            ("TraceSaveLevel", "trace_save_level"),
            ("TracePreTrigger", "trace_pre_trigger"),
            ("TracePostTrigger", "trace_post_trigger"),
            ("AlarmValue", "alarm_value"),
            ("FlatLevel", "flat_level"),
            ("DisableLed", "disable_led"),
            ("LogFlushInterval", "log_flush_interval"),
            ("GuideLine", "guide_line"),
            ("BuildingLevel", "building_level"),
            ("VectorEnabled", "vector_enabled"),
            ("AtopEnabled", "atop_enabled"),
            ("VtopEnabled", "vtop_enabled"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsSensorEntity))!,
            ("Id", "id"),
            ("SerialId", "serial_id"),
            ("Name", "name"),
            ("Lastseen", "lastseen"),
            ("BatteryCharge", "battery_charge"),
            ("ConnectedUsing", "connected_using"),
            ("Online", "online"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsPeakLevelEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("XFdom", "x_fdom"),
            ("XVtop", "x_vtop"),
            ("XVtopOverflow", "x_vtop_overflow"),
            ("YFdom", "y_fdom"),
            ("YVtop", "y_vtop"),
            ("YVtopOverflow", "y_vtop_overflow"),
            ("ZFdom", "z_fdom"),
            ("ZVtop", "z_vtop"),
            ("ZVtopOverflow", "z_vtop_overflow"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsVeffLevelEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("X", "x"),
            ("Y", "y"),
            ("Z", "z"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsVdvLevelEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("X", "x"),
            ("Y", "y"),
            ("Z", "z"),
            ("VdvX", "vdv_x"),
            ("VdvY", "vdv_y"),
            ("VdvZ", "vdv_z"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsErrorMessageEntity))!,
            ("Tag", "tag"),
            ("Error", "error"),
            ("ErrorTime", "error_time"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsTraceIndexEntity))!,
            ("Id", "id"),
            ("SerialId", "serial_id"),
            ("StartTime", "start_time"),
            ("EndTime", "end_time"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsImportCursorEntity))!,
            ("SerialId", "serial_id"),
            ("Series", "series"),
            ("LastSampleAt", "last_sample_at"),
            ("UpdatedAt", "updated_at"));
        AssertColumns(
            context.Model.FindEntityType(typeof(OmnidotsTraceEntity))!,
            // The portal owns this column's name; see docs/database/omnidots-trace-ownership.md.
            ("TraceId", "omnidots_trace_index_id"),
            ("SampleIndex", "sample_index"),
            ("X", "x"),
            ("Y", "y"),
            ("Z", "z"));

        AssertTimestamp(context, typeof(OmnidotsSensorEntity), nameof(OmnidotsSensorEntity.Lastseen));
        AssertTimestamp(context, typeof(OmnidotsPeakLevelEntity), nameof(OmnidotsPeakLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(OmnidotsVeffLevelEntity), nameof(OmnidotsVeffLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(OmnidotsVdvLevelEntity), nameof(OmnidotsVdvLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(OmnidotsErrorMessageEntity), nameof(OmnidotsErrorMessageEntity.ErrorTime));
        AssertTimestamp(context, typeof(OmnidotsTraceIndexEntity), nameof(OmnidotsTraceIndexEntity.StartTime));
        AssertTimestamp(context, typeof(OmnidotsTraceIndexEntity), nameof(OmnidotsTraceIndexEntity.EndTime));
        AssertTimestamp(context, typeof(OmnidotsImportCursorEntity), nameof(OmnidotsImportCursorEntity.LastSampleAt));
        AssertTimestamp(context, typeof(OmnidotsImportCursorEntity), nameof(OmnidotsImportCursorEntity.UpdatedAt));
    }

    [TestMethod]
    public void OmnidotsContext_PreservesKeysAndCanonicalIndexes()
    {
        using OmnidotsMonitorContext context = CreateContext();

        AssertKey(context, typeof(OmnidotsMonitorStatusEntity), "Id");
        AssertKey(context, typeof(OmnidotsSensorEntity), "Id");
        AssertKey(context, typeof(OmnidotsPeakLevelEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(OmnidotsVeffLevelEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(OmnidotsVdvLevelEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(OmnidotsErrorMessageEntity), "Tag", "ErrorTime", "Error");
        AssertKey(context, typeof(OmnidotsTraceIndexEntity), "Id");
        AssertKey(context, typeof(OmnidotsImportCursorEntity), "SerialId", "Series");
        AssertKey(context, typeof(OmnidotsTraceEntity), "TraceId", "SampleIndex");

        AssertIndex(
            context.Model.FindEntityType(typeof(OmnidotsMonitorStatusEntity))!,
            "ix_omnidots_monitor_status_serial_id",
            false,
            "SerialId");
        AssertIndex(
            context.Model.FindEntityType(typeof(OmnidotsSensorEntity))!,
            "ix_omnidots_sensor_serial_id",
            false,
            "SerialId");
        AssertIndex(
            context.Model.FindEntityType(typeof(MonitorEntity))!,
            "ix_monitor_serial_id_type_of_monitor",
            false,
            "SerialId",
            "TypeOfMonitor");
    }

    [TestMethod]
    public void OmnidotsContext_MapsImportCursorToCanonicalMigrationShape()
    {
        using OmnidotsMonitorContext context = CreateContext();
        IEntityType? cursor = context.Model.FindEntityType(typeof(OmnidotsImportCursorEntity));

        Assert.IsNotNull(cursor);
        Assert.AreEqual("omnidots_import_cursor", cursor.GetTableName());
        Assert.IsNull(cursor.GetSchema());
        AssertColumn(cursor, nameof(OmnidotsImportCursorEntity.SerialId), "serial_id", "text");
        AssertColumn(cursor, nameof(OmnidotsImportCursorEntity.Series), "series", "text");
        AssertColumn(cursor, nameof(OmnidotsImportCursorEntity.LastSampleAt), "last_sample_at", "timestamp with time zone");
        AssertColumn(cursor, nameof(OmnidotsImportCursorEntity.UpdatedAt), "updated_at", "timestamp with time zone");
        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsImportCursorEntity.SerialId), nameof(OmnidotsImportCursorEntity.Series) },
            cursor.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        AssertSeriesConstraint(
            context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OmnidotsImportCursorEntity))!,
            "ck_omnidots_import_cursor_series",
            "\"series\" IN ('Peak', 'Veff', 'Vdv')");
    }

    [TestMethod]
    public void OmnidotsContext_MapsCursorTimestampsWithSymmetricUtcSemantics()
    {
        using OmnidotsMonitorContext context = CreateContext();

        DateTime utcValue = new(2026, 7, 14, 9, 30, 0, DateTimeKind.Utc);
        DateTime[] values =
        [
            utcValue,
            DateTime.SpecifyKind(utcValue, DateTimeKind.Local),
            DateTime.SpecifyKind(utcValue, DateTimeKind.Unspecified)
        ];

        IEntityType? cursor = context.Model.FindEntityType(typeof(OmnidotsImportCursorEntity));
        Assert.IsNotNull(cursor);

        foreach (string? propertyName in new[]
                 {
                     nameof(OmnidotsImportCursorEntity.LastSampleAt),
                     nameof(OmnidotsImportCursorEntity.UpdatedAt)
                 })
        {
            ValueConverter? converter = cursor.FindProperty(propertyName)!.GetValueConverter();
            Assert.IsNotNull(converter, $"{propertyName} must normalize database values to UTC.");

            foreach (DateTime value in values)
            {
                DateTime expected = NormalizeUtc(value);
                DateTime providerValue = (DateTime)converter.ConvertToProvider(value)!;
                DateTime materializedValue = (DateTime)converter.ConvertFromProvider(value)!;

                AssertUtcValue(expected, providerValue, cursor.GetTableName()!, propertyName, value.Kind, "write");
                AssertUtcValue(expected, materializedValue, cursor.GetTableName()!, propertyName, value.Kind, "read");
            }
        }
    }

    [TestMethod]
    public void OmnidotsContext_MapsTraceSampleIndexAndCompositeKey()
    {
        using OmnidotsMonitorContext context = CreateContext();
        IEntityType? trace = context.Model.FindEntityType(typeof(OmnidotsTraceEntity));

        Assert.IsNotNull(trace);
        AssertColumn(trace, nameof(OmnidotsTraceEntity.SampleIndex), "sample_index", "integer");
        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsTraceEntity.TraceId), nameof(OmnidotsTraceEntity.SampleIndex) },
            trace.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void SharedAlertEntities_MapToCanonicalPostgreSqlShape()
    {
        using OmnidotsMonitorContext context = CreateContext();

        AssertAlertOccurrence(context);
        AssertAlertOutbox(context);
    }

    private static void AssertAlertOccurrence(MonitorDbContextBase context)
    {
        IEntityType? entity = context.Model.FindEntityType(typeof(AlertOccurrenceEntity));
        Assert.IsNotNull(entity);
        Assert.AreEqual("alert_occurrence", entity.GetTableName());
        Assert.IsNull(entity.GetSchema());
        CollectionAssert.AreEqual(
            new[] { nameof(AlertOccurrenceEntity.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Id), "id", "uuid", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Source), "source", "varchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.SourceKeyHash), "source_key_hash", "bytea", false, 32);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.NotificationId), "notification_id", "uuid", true);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.MonitorId), "monitor_id", "uuid", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.SerialId), "serial_id", "varchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.EventTime), "event_time", "timestamp with time zone", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AlertType), "alert_type", "integer", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AlertField), "alert_field", "varchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Level), "level", "double precision", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.LimitOn), "limit_on", "double precision", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AveragingPeriod), "averaging_period", "integer", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Outcome), "outcome", "varchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.CreatedAt), "created_at", "timestamp with time zone", false);

        AssertIndex(
            entity,
            "uq_alert_occurrence_source_key",
            true,
            nameof(AlertOccurrenceEntity.Source),
            nameof(AlertOccurrenceEntity.SourceKeyHash));
        AssertForeignKey(entity, typeof(MonitorEntity), DeleteBehavior.Restrict, false, nameof(AlertOccurrenceEntity.MonitorId));
        AssertForeignKey(entity, typeof(NotificationEntity), DeleteBehavior.Restrict, true, nameof(AlertOccurrenceEntity.NotificationId));
    }

    private static void AssertAlertOutbox(MonitorDbContextBase context)
    {
        IEntityType? entity = context.Model.FindEntityType(typeof(AlertDeliveryOutboxEntity));
        Assert.IsNotNull(entity);
        Assert.AreEqual("alert_delivery_outbox", entity.GetTableName());
        Assert.IsNull(entity.GetSchema());
        CollectionAssert.AreEqual(
            new[] { nameof(AlertDeliveryOutboxEntity.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Id), "id", "uuid", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.OccurrenceId), "occurrence_id", "uuid", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.DeliveryKey), "delivery_key", "varchar(64)", false, 64);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Kind), "kind", "varchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Destination), "destination", "varchar(512)", false, 512);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Payload), "payload", "varchar(8192)", false, 8192);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Status), "status", "varchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.AttemptCount), "attempt_count", "integer", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.NextAttemptAt), "next_attempt_at", "timestamp with time zone", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LeaseId), "lease_id", "uuid", true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LeaseUntil), "lease_until", "timestamp with time zone", true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.CompletedAt), "completed_at", "timestamp with time zone", true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LastError), "last_error", "varchar(1024)", true, 1024);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.CreatedAt), "created_at", "timestamp with time zone", false);

        AssertIndex(
            entity,
            "uq_alert_delivery_outbox_delivery_key",
            true,
            nameof(AlertDeliveryOutboxEntity.DeliveryKey));
        AssertIndex(
            entity,
            "ix_alert_delivery_outbox_due",
            false,
            nameof(AlertDeliveryOutboxEntity.Status),
            nameof(AlertDeliveryOutboxEntity.NextAttemptAt),
            nameof(AlertDeliveryOutboxEntity.LeaseUntil),
            nameof(AlertDeliveryOutboxEntity.CreatedAt));
        AssertForeignKey(entity, typeof(AlertOccurrenceEntity), DeleteBehavior.Cascade, false, nameof(AlertDeliveryOutboxEntity.OccurrenceId));
    }

    private static void AssertAlertColumn(
        IEntityType entity,
        string propertyName,
        string columnName,
        string columnType,
        bool nullable,
        int? maxLength = null)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual(columnName, property.GetColumnName());
        Assert.AreEqual(columnType, property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.AreEqual(nullable, property.IsNullable);
        Assert.AreEqual(maxLength, property.GetMaxLength());
    }

    private static void AssertColumns(
        IReadOnlyEntityType entityType,
        params (string Property, string Column)[] expectedColumns)
    {
        Assert.HasCount(expectedColumns.Length, entityType.GetProperties());
        foreach ((string Property, string Column) in expectedColumns)
        {
            Assert.AreEqual(
                Column,
                entityType.FindProperty(Property)!.GetColumnName(),
                Property);
        }
    }

    private static void AssertTimestamp(
        OmnidotsMonitorContext context,
        Type entityClrType,
        string propertyName)
    {
        IProperty? property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual("timestamp with time zone", property.GetRelationalTypeMapping().StoreType);
    }

    private static void AssertKey(
        OmnidotsMonitorContext context,
        Type entityClrType,
        params string[] expectedProperties)
    {
        string[] keyProperties = [.. context.Model
            .FindEntityType(entityClrType)!
            .FindPrimaryKey()!
            .Properties
            .Select(property => property.Name)];
        CollectionAssert.AreEqual(expectedProperties, keyProperties);
    }

    private static void AssertIndex(IEntityType entity, string name, bool unique, params string[] properties)
    {
        IIndex? index = entity.GetIndexes().SingleOrDefault(candidate => candidate.GetDatabaseName() == name);
        Assert.IsNotNull(index, $"Expected index {name} on {entity.GetTableName()}.");
        Assert.AreEqual(unique, index.IsUnique);
        CollectionAssert.AreEqual(properties, index.Properties.Select(property => property.Name).ToArray());
    }

    private static void AssertForeignKey(
        IEntityType entity,
        Type principalType,
        DeleteBehavior deleteBehavior,
        bool nullable,
        params string[] properties)
    {
        IForeignKey? foreignKey = entity.GetForeignKeys().SingleOrDefault(candidate =>
            candidate.PrincipalEntityType.ClrType == principalType &&
            candidate.Properties.Select(property => property.Name).SequenceEqual(properties));
        Assert.IsNotNull(foreignKey);
        Assert.AreEqual(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.AreEqual(nullable, foreignKey.Properties.Single().IsNullable);
    }

    private static void AssertColumn(IEntityType entity, string propertyName, string columnName, string columnType)
    {
        IProperty? property = entity.FindProperty(propertyName);

        Assert.IsNotNull(property);
        Assert.AreEqual(columnName, property.GetColumnName());
        Assert.AreEqual(columnType, property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.IsFalse(property.IsNullable);
    }

    private static void AssertSeriesConstraint(IEntityType entity, string name, string sql)
    {
        ICheckConstraint[] constraints = [.. entity.GetCheckConstraints()];

        Assert.HasCount(1, constraints);
        Assert.AreEqual(name, constraints[0].Name);
        Assert.AreEqual(sql, constraints[0].Sql);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static void AssertUtcValue(
        DateTime expected,
        DateTime actual,
        string tableName,
        string propertyName,
        DateTimeKind sourceKind,
        string direction)
    {
        Assert.AreEqual(
            expected.Ticks,
            actual.Ticks,
            $"{tableName} {propertyName} {direction} conversion from {sourceKind} changed the instant.");
        Assert.AreEqual(
            DateTimeKind.Utc,
            actual.Kind,
            $"{tableName} {propertyName} {direction} conversion from {sourceKind} did not return UTC.");
    }

    private static OmnidotsMonitorContext CreateContext()
    {
        MonitorDbOptions options = new(new Dictionary<string, string>());
        DbContextOptions<OmnidotsMonitorContext> dbOptions = new DbContextOptionsBuilder<OmnidotsMonitorContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;

        return new OmnidotsMonitorContext(dbOptions, options);
    }
}

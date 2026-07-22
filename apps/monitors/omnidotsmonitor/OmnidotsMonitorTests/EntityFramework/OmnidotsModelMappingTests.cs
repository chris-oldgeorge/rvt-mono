using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Omnidots.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace OmnidotsMonitorTests.EntityFramework;

[TestClass]
public sealed class OmnidotsModelMappingTests
{
    private const string SqlServerSeriesConstraintSql =
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Peak' AND DATALENGTH([Series]) = DATALENGTH(N'Peak')) OR " +
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Veff' AND DATALENGTH([Series]) = DATALENGTH(N'Veff')) OR " +
        "([Series] COLLATE Latin1_General_100_BIN2 = N'Vdv' AND DATALENGTH([Series]) = DATALENGTH(N'Vdv'))";

    [TestMethod]
    [DataRow(typeof(OmnidotsMonitorStatusEntity), "OmnidotsMonitorStatus", "omnidots_monitor_status")]
    [DataRow(typeof(OmnidotsSensorEntity), "OmnidotsSensors", "omnidots_sensor")]
    [DataRow(typeof(OmnidotsPeakLevelEntity), "OmnidotsPeakLevels", "omnidots_peak_level")]
    [DataRow(typeof(OmnidotsVeffLevelEntity), "OmnidotsVeffLevels", "omnidots_veff_level")]
    [DataRow(typeof(OmnidotsVdvLevelEntity), "OmnidotsVdvLevels", "omnidots_vdv_level")]
    [DataRow(typeof(OmnidotsTraceIndexEntity), "OmnidotsTracesIndex", "omnidots_trace_index")]
    [DataRow(typeof(OmnidotsTraceEntity), "OmnidotsTraces", "omnidots_trace")]
    public void OmnidotsContext_MapsMonitorTables(Type entityClrType, string sqlServerTable, string postgreSqlTable)
    {
        using var sqlServerContext = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSqlContext = CreateContext(MonitorDatabaseProvider.PostgreSql);

        var sqlServerEntity = sqlServerContext.Model.FindEntityType(entityClrType);
        var postgreSqlEntity = postgreSqlContext.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(sqlServerEntity);
        Assert.IsNotNull(postgreSqlEntity);
        Assert.AreEqual(sqlServerTable, sqlServerEntity.GetTableName());
        Assert.AreEqual("dbo", sqlServerEntity.GetSchema());
        Assert.AreEqual(postgreSqlTable, postgreSqlEntity.GetTableName());
        Assert.IsNull(postgreSqlEntity.GetSchema());
    }

    [TestMethod]
    public void OmnidotsContext_MapsRepresentativeColumns()
    {
        using var sqlServerContext = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSqlContext = CreateContext(MonitorDatabaseProvider.PostgreSql);

        var sqlServerPeak = sqlServerContext.Model.FindEntityType(typeof(OmnidotsPeakLevelEntity));
        var postgreSqlPeak = postgreSqlContext.Model.FindEntityType(typeof(OmnidotsPeakLevelEntity));
        var sqlServerTrace = sqlServerContext.Model.FindEntityType(typeof(OmnidotsTraceEntity));

        Assert.IsNotNull(sqlServerPeak);
        Assert.IsNotNull(postgreSqlPeak);
        Assert.IsNotNull(sqlServerTrace);
        Assert.AreEqual("XFdom", sqlServerPeak.FindProperty(nameof(OmnidotsPeakLevelEntity.XFdom))!.GetColumnName());
        Assert.AreEqual("x_fdom", postgreSqlPeak.FindProperty(nameof(OmnidotsPeakLevelEntity.XFdom))!.GetColumnName());
        Assert.AreEqual("TraceId", sqlServerTrace.FindProperty(nameof(OmnidotsTraceEntity.TraceId))!.GetColumnName());
        Assert.AreEqual("trace_id", postgreSqlContext.Model.FindEntityType(typeof(OmnidotsTraceEntity))!
            .FindProperty(nameof(OmnidotsTraceEntity.TraceId))!.GetColumnName());
    }

    [TestMethod]
    public void OmnidotsContext_MapsImportCursorToProviderMigrationShape()
    {
        using var sqlServerContext = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSqlContext = CreateContext(MonitorDatabaseProvider.PostgreSql);

        var sqlServerCursor = sqlServerContext.Model.FindEntityType(typeof(OmnidotsImportCursorEntity));
        var postgreSqlCursor = postgreSqlContext.Model.FindEntityType(typeof(OmnidotsImportCursorEntity));

        Assert.IsNotNull(sqlServerCursor);
        Assert.IsNotNull(postgreSqlCursor);
        Assert.AreEqual("OmnidotsImportCursor", sqlServerCursor.GetTableName());
        Assert.AreEqual("dbo", sqlServerCursor.GetSchema());
        Assert.AreEqual("omnidots_import_cursor", postgreSqlCursor.GetTableName());
        Assert.IsNull(postgreSqlCursor.GetSchema());

        AssertColumn(sqlServerCursor, nameof(OmnidotsImportCursorEntity.SerialId), "SerialId", "nvarchar(128)");
        AssertColumn(sqlServerCursor, nameof(OmnidotsImportCursorEntity.Series), "Series", "nvarchar(16)");
        AssertColumn(sqlServerCursor, nameof(OmnidotsImportCursorEntity.LastSampleAt), "LastSampleAt", "datetime2");
        AssertColumn(sqlServerCursor, nameof(OmnidotsImportCursorEntity.UpdatedAt), "UpdatedAt", "datetime2");
        AssertColumn(postgreSqlCursor, nameof(OmnidotsImportCursorEntity.SerialId), "serial_id", "text");
        AssertColumn(postgreSqlCursor, nameof(OmnidotsImportCursorEntity.Series), "series", "text");
        AssertColumn(postgreSqlCursor, nameof(OmnidotsImportCursorEntity.LastSampleAt), "last_sample_at", "timestamp with time zone");
        AssertColumn(postgreSqlCursor, nameof(OmnidotsImportCursorEntity.UpdatedAt), "updated_at", "timestamp with time zone");

        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsImportCursorEntity.SerialId), nameof(OmnidotsImportCursorEntity.Series) },
            sqlServerCursor.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsImportCursorEntity.SerialId), nameof(OmnidotsImportCursorEntity.Series) },
            postgreSqlCursor.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        AssertSeriesConstraint(
            sqlServerContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OmnidotsImportCursorEntity))!,
            "CK_OmnidotsImportCursor_Series",
            SqlServerSeriesConstraintSql);
        AssertSeriesConstraint(
            postgreSqlContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OmnidotsImportCursorEntity))!,
            "ck_omnidots_import_cursor_series",
            "\"series\" IN ('Peak', 'Veff', 'Vdv')");
    }

    [TestMethod]
    public void OmnidotsContext_MapsCursorTimestampsWithSymmetricUtcSemantics()
    {
        using var sqlServerContext = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSqlContext = CreateContext(MonitorDatabaseProvider.PostgreSql);

        var utcValue = new DateTime(2026, 7, 14, 9, 30, 0, DateTimeKind.Utc);
        var values = new[]
        {
            utcValue,
            DateTime.SpecifyKind(utcValue, DateTimeKind.Local),
            DateTime.SpecifyKind(utcValue, DateTimeKind.Unspecified)
        };

        foreach (var context in new[] { sqlServerContext, postgreSqlContext })
        {
            var cursor = context.Model.FindEntityType(typeof(OmnidotsImportCursorEntity));
            Assert.IsNotNull(cursor);

            foreach (var propertyName in new[]
                     {
                         nameof(OmnidotsImportCursorEntity.LastSampleAt),
                         nameof(OmnidotsImportCursorEntity.UpdatedAt)
                     })
            {
                var converter = cursor.FindProperty(propertyName)!.GetValueConverter();
                Assert.IsNotNull(converter, $"{propertyName} must normalize database values to UTC.");

                foreach (var value in values)
                {
                    var expected = NormalizeUtc(value);
                    var providerValue = (DateTime)converter.ConvertToProvider(value)!;
                    var materializedValue = (DateTime)converter.ConvertFromProvider(value)!;

                    AssertUtcValue(expected, providerValue, cursor.GetTableName()!, propertyName, value.Kind, "write");
                    AssertUtcValue(expected, materializedValue, cursor.GetTableName()!, propertyName, value.Kind, "read");
                }
            }
        }
    }

    [TestMethod]
    public void OmnidotsContext_MapsTraceSampleIndexAndCompositeKeyForEachProvider()
    {
        using var sqlServerContext = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSqlContext = CreateContext(MonitorDatabaseProvider.PostgreSql);

        var sqlServerTrace = sqlServerContext.Model.FindEntityType(typeof(OmnidotsTraceEntity));
        var postgreSqlTrace = postgreSqlContext.Model.FindEntityType(typeof(OmnidotsTraceEntity));

        Assert.IsNotNull(sqlServerTrace);
        Assert.IsNotNull(postgreSqlTrace);
        AssertColumn(sqlServerTrace, nameof(OmnidotsTraceEntity.SampleIndex), "SampleIndex", "int");
        AssertColumn(postgreSqlTrace, nameof(OmnidotsTraceEntity.SampleIndex), "sample_index", "integer");
        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsTraceEntity.TraceId), nameof(OmnidotsTraceEntity.SampleIndex) },
            sqlServerTrace.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { nameof(OmnidotsTraceEntity.TraceId), nameof(OmnidotsTraceEntity.SampleIndex) },
            postgreSqlTrace.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void SharedAlertEntities_MapToBothProviderShapes()
    {
        using var sqlServer = CreateContext(MonitorDatabaseProvider.SqlServer);
        using var postgreSql = CreateContext(MonitorDatabaseProvider.PostgreSql);

        AssertAlertOccurrence(sqlServer, "AlertOccurrences", "dbo", "SourceKeyHash", "binary(32)");
        AssertAlertOccurrence(postgreSql, "alert_occurrence", null, "source_key_hash", "bytea");
        AssertAlertOutbox(sqlServer, "AlertDeliveryOutbox", "dbo", "LeaseId", "uniqueidentifier");
        AssertAlertOutbox(postgreSql, "alert_delivery_outbox", null, "lease_id", "uuid");
    }

    private static void AssertAlertOccurrence(
        MonitorDbContextBase context,
        string table,
        string? schema,
        string sourceKeyHashColumn,
        string sourceKeyHashType)
    {
        var entity = context.Model.FindEntityType(typeof(AlertOccurrenceEntity));
        Assert.IsNotNull(entity);
        Assert.AreEqual(table, entity.GetTableName());
        Assert.AreEqual(schema, entity.GetSchema());
        CollectionAssert.AreEqual(
            new[] { nameof(AlertOccurrenceEntity.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        var postgreSql = schema is null;
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Id), postgreSql ? "id" : "Id", postgreSql ? "uuid" : "uniqueidentifier", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Source), postgreSql ? "source" : "Source", postgreSql ? "varchar(128)" : "nvarchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.SourceKeyHash), sourceKeyHashColumn, sourceKeyHashType, false, 32);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.NotificationId), postgreSql ? "notification_id" : "NotificationId", postgreSql ? "uuid" : "uniqueidentifier", true);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.MonitorId), postgreSql ? "monitor_id" : "MonitorId", postgreSql ? "uuid" : "uniqueidentifier", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.SerialId), postgreSql ? "serial_id" : "SerialId", postgreSql ? "varchar(128)" : "nvarchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.EventTime), postgreSql ? "event_time" : "EventTime", postgreSql ? "timestamp with time zone" : "datetime2", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AlertType), postgreSql ? "alert_type" : "AlertType", postgreSql ? "integer" : "int", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AlertField), postgreSql ? "alert_field" : "AlertField", postgreSql ? "varchar(128)" : "nvarchar(128)", false, 128);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Level), postgreSql ? "level" : "Level", postgreSql ? "double precision" : "float", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.LimitOn), postgreSql ? "limit_on" : "LimitOn", postgreSql ? "double precision" : "float", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.AveragingPeriod), postgreSql ? "averaging_period" : "AveragingPeriod", postgreSql ? "integer" : "int", false);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.Outcome), postgreSql ? "outcome" : "Outcome", postgreSql ? "varchar(32)" : "nvarchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertOccurrenceEntity.CreatedAt), postgreSql ? "created_at" : "CreatedAt", postgreSql ? "timestamp with time zone" : "datetime2", false);

        AssertIndex(
            entity,
            postgreSql ? "uq_alert_occurrence_source_key" : "UQ_AlertOccurrences_SourceKey",
            true,
            nameof(AlertOccurrenceEntity.Source),
            nameof(AlertOccurrenceEntity.SourceKeyHash));
        AssertForeignKey(entity, typeof(MonitorEntity), DeleteBehavior.Restrict, false, nameof(AlertOccurrenceEntity.MonitorId));
        AssertForeignKey(entity, typeof(NotificationEntity), DeleteBehavior.Restrict, true, nameof(AlertOccurrenceEntity.NotificationId));
    }

    private static void AssertAlertOutbox(
        MonitorDbContextBase context,
        string table,
        string? schema,
        string leaseIdColumn,
        string leaseIdType)
    {
        var entity = context.Model.FindEntityType(typeof(AlertDeliveryOutboxEntity));
        Assert.IsNotNull(entity);
        Assert.AreEqual(table, entity.GetTableName());
        Assert.AreEqual(schema, entity.GetSchema());
        CollectionAssert.AreEqual(
            new[] { nameof(AlertDeliveryOutboxEntity.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        var postgreSql = schema is null;
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Id), postgreSql ? "id" : "Id", postgreSql ? "uuid" : "uniqueidentifier", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.OccurrenceId), postgreSql ? "occurrence_id" : "OccurrenceId", postgreSql ? "uuid" : "uniqueidentifier", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.DeliveryKey), postgreSql ? "delivery_key" : "DeliveryKey", postgreSql ? "varchar(64)" : "nvarchar(64)", false, 64);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Kind), postgreSql ? "kind" : "Kind", postgreSql ? "varchar(32)" : "nvarchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Destination), postgreSql ? "destination" : "Destination", postgreSql ? "varchar(512)" : "nvarchar(512)", false, 512);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Payload), postgreSql ? "payload" : "Payload", postgreSql ? "varchar(8192)" : "nvarchar(max)", false, 8192);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.Status), postgreSql ? "status" : "Status", postgreSql ? "varchar(32)" : "nvarchar(32)", false, 32);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.AttemptCount), postgreSql ? "attempt_count" : "AttemptCount", postgreSql ? "integer" : "int", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.NextAttemptAt), postgreSql ? "next_attempt_at" : "NextAttemptAt", postgreSql ? "timestamp with time zone" : "datetime2", false);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LeaseId), leaseIdColumn, leaseIdType, true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LeaseUntil), postgreSql ? "lease_until" : "LeaseUntil", postgreSql ? "timestamp with time zone" : "datetime2", true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.CompletedAt), postgreSql ? "completed_at" : "CompletedAt", postgreSql ? "timestamp with time zone" : "datetime2", true);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.LastError), postgreSql ? "last_error" : "LastError", postgreSql ? "varchar(256)" : "nvarchar(256)", true, 256);
        AssertAlertColumn(entity, nameof(AlertDeliveryOutboxEntity.CreatedAt), postgreSql ? "created_at" : "CreatedAt", postgreSql ? "timestamp with time zone" : "datetime2", false);

        AssertIndex(
            entity,
            postgreSql ? "uq_alert_delivery_outbox_delivery_key" : "UQ_AlertDeliveryOutbox_DeliveryKey",
            true,
            nameof(AlertDeliveryOutboxEntity.DeliveryKey));
        AssertIndex(
            entity,
            postgreSql ? "ix_alert_delivery_outbox_due" : "IX_AlertDeliveryOutbox_Due",
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
        var property = entity.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual(columnName, property.GetColumnName());
        Assert.AreEqual(columnType, property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.AreEqual(nullable, property.IsNullable);
        Assert.AreEqual(maxLength, property.GetMaxLength());
    }

    private static void AssertIndex(IEntityType entity, string name, bool unique, params string[] properties)
    {
        var index = entity.GetIndexes().SingleOrDefault(candidate => candidate.GetDatabaseName() == name);
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
        var foreignKey = entity.GetForeignKeys().SingleOrDefault(candidate =>
            candidate.PrincipalEntityType.ClrType == principalType &&
            candidate.Properties.Select(property => property.Name).SequenceEqual(properties));
        Assert.IsNotNull(foreignKey);
        Assert.AreEqual(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.AreEqual(nullable, foreignKey.Properties.Single().IsNullable);
    }

    private static void AssertColumn(IEntityType entity, string propertyName, string columnName, string columnType)
    {
        var property = entity.FindProperty(propertyName);

        Assert.IsNotNull(property);
        Assert.AreEqual(columnName, property.GetColumnName());
        Assert.AreEqual(columnType, property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.IsFalse(property.IsNullable);
    }

    private static void AssertSeriesConstraint(IEntityType entity, string name, string sql)
    {
        var constraints = entity.GetCheckConstraints().ToArray();

        Assert.AreEqual(1, constraints.Length);
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

    private static OmnidotsMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var options = new MonitorDbOptions(provider, new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<OmnidotsMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OmnidotsMonitorContext(dbOptions, options);
    }
}

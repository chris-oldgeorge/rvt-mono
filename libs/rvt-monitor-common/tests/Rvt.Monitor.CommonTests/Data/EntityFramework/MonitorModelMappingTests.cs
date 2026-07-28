using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Data.EntityFramework;

[TestClass]
public sealed class MonitorModelMappingTests
{
    private sealed record PropertyExpectation(
        string PropertyName,
        string Column,
        Type ClrType,
        bool IsNullable,
        string StoreType,
        int? MaxLength = null);

    private sealed class TestMonitorContext(
        DbContextOptions<TestMonitorContext> options,
        MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions)
    {
    }

    [TestMethod]
    [DataRow(typeof(MonitorEntity), "monitor")]
    [DataRow(typeof(RvtAlertRuleEntity), "rvt_alert_rule")]
    [DataRow(typeof(NotificationEntity), "notification")]
    [DataRow(typeof(MonitorDeliveryOutboxEntity), "monitor_delivery_outbox")]
    [DataRow(typeof(NotificationSentEntity), "notification_sent")]
    [DataRow(typeof(DeploymentEntity), "deployment")]
    [DataRow(typeof(ContractEntity), "contract")]
    [DataRow(typeof(SiteEntity), "site")]
    [DataRow(typeof(SiteUserEntity), "site_user")]
    [DataRow(typeof(NotificationSettingEntity), "notification_setting")]
    [DataRow(typeof(AspNetUserEntity), "AspNetUsers")]
    [DataRow(typeof(SiteAverageEntity), "site_average")]
    [DataRow(typeof(ErrorMessageEntity), "error_log")]
    [DataRow(typeof(AlertOccurrenceEntity), "alert_occurrence")]
    [DataRow(typeof(AlertDeliveryOutboxEntity), "alert_delivery_outbox")]
    public void SharedModel_MapsFixedCanonicalTablesWithoutSchemas(Type entityClrType, string table)
    {
        using TestMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(entity);
        Assert.AreEqual(table, entity.GetTableName());
        Assert.IsNull(entity.GetSchema());
    }

    [TestMethod]
    public void SharedModel_MapsCanonicalMonitorColumnsTypesAndIndex()
    {
        using TestMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(entity);
        AssertProperty(entity, nameof(MonitorEntity.Id), "id", "uuid");
        AssertProperty(entity, nameof(MonitorEntity.FleetNr), "fleet_row_count", "text");
        AssertProperty(entity, nameof(MonitorEntity.SerialId), "serial_id", "text");
        AssertProperty(entity, nameof(MonitorEntity.ListedAtTime), "listed_at_time", "timestamp with time zone");
        AssertProperty(entity, nameof(MonitorEntity.Offline), "offline", "boolean");
        Assert.AreEqual(
            "ix_monitor_serial_id_type_of_monitor",
            entity.GetIndexes().Single().GetDatabaseName());
    }

    [TestMethod]
    public void SharedContext_ExposesDurableAlertSetsAndFactoryBoundary()
    {
        using TestMonitorContext context = CreateContext();

        Assert.AreSame(context.Set<AlertOccurrenceEntity>(), context.AlertOccurrences);
        Assert.AreSame(context.Set<AlertDeliveryOutboxEntity>(), context.AlertDeliveryOutbox);
        Assert.IsTrue(typeof(IMonitorDbContextFactory<TestMonitorContext>)
            .IsAssignableFrom(typeof(TestContextFactory)));
    }

    [TestMethod]
    public void SharedModel_AppliesSharedMonitorTableOverridesAcrossCachedModels()
    {
        using TestMonitorContext firstContext = CreateContext(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MonitorsList"] = "first_monitor"
        });
        using TestMonitorContext secondContext = CreateContext(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MonitorsList"] = "second_monitor"
        });

        Assert.AreEqual(
            "first_monitor",
            firstContext.Model.FindEntityType(typeof(MonitorEntity))!.GetTableName());
        Assert.AreEqual(
            "second_monitor",
            secondContext.Model.FindEntityType(typeof(MonitorEntity))!.GetTableName());
        Assert.AreNotSame(firstContext.Model, secondContext.Model);
    }

    [TestMethod]
    public void SharedModel_RejectsUnsafeSharedTableOverride()
    {
        using TestMonitorContext context = CreateContext(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MonitorsList"] = "monitor; DROP TABLE notification;--"
        });

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => _ = context.Model);

        StringAssert.Contains(exception.Message, "Unsafe SQL identifier");
    }

    [TestMethod]
    public void SharedModel_MapsDeliveryOutboxCanonicalContract()
    {
        using TestMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(typeof(MonitorDeliveryOutboxEntity));

        Assert.IsNotNull(entity);
        Assert.AreEqual("monitor_delivery_outbox", entity.GetTableName());
        Assert.IsNull(entity.GetSchema());
        AssertProperties(
            entity,
            new PropertyExpectation("Id", "id", typeof(Guid), false, "uuid"),
            new PropertyExpectation("Producer", "producer", typeof(string), false, "text", 64),
            new PropertyExpectation("NotificationId", "notification_id", typeof(Guid?), true, "uuid"),
            new PropertyExpectation("CorrelationKey", "correlation_key", typeof(string), true, "text", 450),
            new PropertyExpectation("DeliveryKey", "delivery_key", typeof(string), false, "text", 450),
            new PropertyExpectation("Kind", "kind", typeof(MonitorDeliveryKind), false, "text", 64),
            new PropertyExpectation("Destination", "destination", typeof(string), false, "text", 512),
            new PropertyExpectation("PayloadVersion", "payload_version", typeof(int), false, "integer"),
            new PropertyExpectation("Payload", "payload", typeof(string), false, "text"),
            new PropertyExpectation("Status", "status", typeof(string), false, "text", 32),
            new PropertyExpectation("AttemptCount", "attempt_count", typeof(int), false, "integer"),
            new PropertyExpectation("NextAttemptAt", "next_attempt_at", typeof(DateTime), false, "timestamp with time zone"),
            new PropertyExpectation("LeaseId", "lease_id", typeof(Guid?), true, "uuid"),
            new PropertyExpectation("LeaseUntil", "lease_until", typeof(DateTime?), true, "timestamp with time zone"),
            new PropertyExpectation("CompletedAt", "completed_at", typeof(DateTime?), true, "timestamp with time zone"),
            new PropertyExpectation("DeadLetteredAt", "dead_lettered_at", typeof(DateTime?), true, "timestamp with time zone"),
            new PropertyExpectation("LastError", "last_error", typeof(string), true, "text", 1024),
            new PropertyExpectation("CreatedAt", "created_at", typeof(DateTime), false, "timestamp with time zone"));

        Assert.IsNull(context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(MonitorDeliveryOutboxEntity))!
            .FindProperty(nameof(MonitorDeliveryOutboxEntity.DeliveryKey))!
            .GetCollation());
        CollectionAssert.AreEqual(
            new[]
            {
                "ix_monitor_delivery_outbox_due",
                "ix_monitor_delivery_outbox_notification_id",
                "uq_monitor_delivery_outbox_producer_delivery_key"
            },
            entity.GetIndexes()
                .Select(index => index.GetDatabaseName())
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.IsTrue(entity.GetIndexes().Single(index =>
            index.GetDatabaseName() == "uq_monitor_delivery_outbox_producer_delivery_key").IsUnique);

        IForeignKey notificationForeignKey = entity.GetForeignKeys().Single();
        Assert.AreEqual(typeof(NotificationEntity), notificationForeignKey.PrincipalEntityType.ClrType);
        Assert.AreEqual(DeleteBehavior.SetNull, notificationForeignKey.DeleteBehavior);
    }

    [TestMethod]
    public void SharedModel_MapsAlertOccurrenceCanonicalConstraintsAndIndex()
    {
        using TestMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(typeof(AlertOccurrenceEntity));

        Assert.IsNotNull(entity);
        AssertProperties(
            entity,
            new PropertyExpectation("Id", "id", typeof(Guid), false, "uuid"),
            new PropertyExpectation("Source", "source", typeof(string), false, "varchar(128)", 128),
            new PropertyExpectation("SourceKeyHash", "source_key_hash", typeof(byte[]), false, "bytea", 32),
            new PropertyExpectation("NotificationId", "notification_id", typeof(Guid?), true, "uuid"),
            new PropertyExpectation("MonitorId", "monitor_id", typeof(Guid), false, "uuid"),
            new PropertyExpectation("SerialId", "serial_id", typeof(string), false, "varchar(128)", 128),
            new PropertyExpectation("EventTime", "event_time", typeof(DateTime), false, "timestamp with time zone"),
            new PropertyExpectation("AlertType", "alert_type", typeof(int), false, "integer"),
            new PropertyExpectation("AlertField", "alert_field", typeof(string), false, "varchar(128)", 128),
            new PropertyExpectation("Level", "level", typeof(double), false, "double precision"),
            new PropertyExpectation("LimitOn", "limit_on", typeof(double), false, "double precision"),
            new PropertyExpectation("AveragingPeriod", "averaging_period", typeof(int), false, "integer"),
            new PropertyExpectation("Outcome", "outcome", typeof(string), false, "varchar(32)", 32),
            new PropertyExpectation("CreatedAt", "created_at", typeof(DateTime), false, "timestamp with time zone"));

        IEntityType designTimeEntity = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(AlertOccurrenceEntity))!;
        CollectionAssert.AreEqual(
            new[]
            {
                "ck_alert_occurrence_outcome",
                "ck_alert_occurrence_source_key_hash"
            },
            designTimeEntity.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.AreEqual(
            "octet_length(\"source_key_hash\") = 32",
            designTimeEntity.GetCheckConstraints().Single(constraint =>
                constraint.Name == "ck_alert_occurrence_source_key_hash").Sql);
        Assert.AreEqual(
            "\"outcome\" IN ('Accepted','Ignored','Suppressed')",
            designTimeEntity.GetCheckConstraints().Single(constraint =>
                constraint.Name == "ck_alert_occurrence_outcome").Sql);
        CollectionAssert.AreEqual(
            new[]
            {
                "ix_alert_occurrence_monitor_id",
                "ix_alert_occurrence_notification_id",
                "uq_alert_occurrence_source_key"
            },
            entity.GetIndexes()
                .Select(index => index.GetDatabaseName())
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [TestMethod]
    public void SharedModel_MapsAlertDeliveryOutboxCanonicalConstraintsAndIndexes()
    {
        using TestMonitorContext context = CreateContext();
        IEntityType? entity = context.Model.FindEntityType(typeof(AlertDeliveryOutboxEntity));

        Assert.IsNotNull(entity);
        AssertProperties(
            entity,
            new PropertyExpectation("Id", "id", typeof(Guid), false, "uuid"),
            new PropertyExpectation("OccurrenceId", "occurrence_id", typeof(Guid), false, "uuid"),
            new PropertyExpectation("DeliveryKey", "delivery_key", typeof(string), false, "varchar(64)", 64),
            new PropertyExpectation("Kind", "kind", typeof(string), false, "varchar(32)", 32),
            new PropertyExpectation("Destination", "destination", typeof(string), false, "varchar(512)", 512),
            new PropertyExpectation("Payload", "payload", typeof(string), false, "varchar(8192)", 8192),
            new PropertyExpectation("Status", "status", typeof(string), false, "varchar(32)", 32),
            new PropertyExpectation("AttemptCount", "attempt_count", typeof(int), false, "integer"),
            new PropertyExpectation("NextAttemptAt", "next_attempt_at", typeof(DateTime), false, "timestamp with time zone"),
            new PropertyExpectation("LeaseId", "lease_id", typeof(Guid?), true, "uuid"),
            new PropertyExpectation("LeaseUntil", "lease_until", typeof(DateTime?), true, "timestamp with time zone"),
            new PropertyExpectation("CompletedAt", "completed_at", typeof(DateTime?), true, "timestamp with time zone"),
            new PropertyExpectation("LastError", "last_error", typeof(string), true, "varchar(256)", 256),
            new PropertyExpectation("CreatedAt", "created_at", typeof(DateTime), false, "timestamp with time zone"));

        IEntityType designTimeEntity = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(AlertDeliveryOutboxEntity))!;
        CollectionAssert.AreEqual(
            new[]
            {
                "ck_alert_delivery_outbox_kind",
                "ck_alert_delivery_outbox_status"
            },
            designTimeEntity.GetCheckConstraints()
                .Select(constraint => constraint.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.AreEqual(
            "\"kind\" IN ('MqttAlert','Email','Sms')",
            designTimeEntity.GetCheckConstraints().Single(constraint =>
                constraint.Name == "ck_alert_delivery_outbox_kind").Sql);
        Assert.AreEqual(
            "\"status\" IN ('Pending','Leased','Completed','DeadLetter')",
            designTimeEntity.GetCheckConstraints().Single(constraint =>
                constraint.Name == "ck_alert_delivery_outbox_status").Sql);
        CollectionAssert.AreEqual(
            new[]
            {
                "ix_alert_delivery_outbox_due",
                "ix_alert_delivery_outbox_occurrence_id",
                "uq_alert_delivery_outbox_delivery_key"
            },
            entity.GetIndexes()
                .Select(index => index.GetDatabaseName())
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static void AssertProperty(
        IReadOnlyEntityType entity,
        string propertyName,
        string column,
        string storeType)
    {
        IReadOnlyProperty? property = entity.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual(column, property.GetColumnName());
        Assert.AreEqual(storeType, property.GetRelationalTypeMapping().StoreType);
    }

    private static void AssertProperties(
        IReadOnlyEntityType entity,
        params PropertyExpectation[] expectedProperties)
    {
        Assert.HasCount(expectedProperties.Length, entity.GetProperties());
        foreach (PropertyExpectation expected in expectedProperties)
        {
            IReadOnlyProperty? property = entity.FindProperty(expected.PropertyName);
            Assert.IsNotNull(property, $"Missing property {expected.PropertyName}.");
            Assert.AreEqual(expected.Column, property.GetColumnName(), expected.PropertyName);
            Assert.AreEqual(expected.ClrType, property.ClrType, expected.PropertyName);
            Assert.AreEqual(expected.IsNullable, property.IsNullable, expected.PropertyName);
            Assert.AreEqual(
                expected.StoreType,
                property.GetRelationalTypeMapping().StoreType,
                expected.PropertyName);
            Assert.AreEqual(expected.MaxLength, property.GetMaxLength(), expected.PropertyName);
        }
    }

    private static TestMonitorContext CreateContext(
        IReadOnlyDictionary<string, string>? identifierMap = null)
    {
        MonitorDbOptions monitorOptions = new MonitorDbOptions(
            identifierMap ?? new Dictionary<string, string>(StringComparer.Ordinal));
        DbContextOptions<TestMonitorContext> dbOptions = new DbContextOptionsBuilder<TestMonitorContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;
        return new TestMonitorContext(dbOptions, monitorOptions);
    }

    private sealed class TestContextFactory(TestMonitorContext context)
        : IMonitorDbContextFactory<TestMonitorContext>
    {
        public TestMonitorContext CreateDbContext() => context;
    }
}

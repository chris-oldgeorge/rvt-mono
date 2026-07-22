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
    private sealed record OutboxPropertyExpectation(
        string PropertyName,
        string SqlServerColumn,
        string PostgreSqlColumn,
        Type ClrType,
        bool IsNullable,
        string SqlServerStoreType,
        string PostgreSqlStoreType,
        int? SqlServerMaxLength = null);

    private sealed class TestMonitorContext(DbContextOptions<TestMonitorContext> options, MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions)
    {
    }

    [TestMethod]
    public void SharedModel_MapsSqlServerMonitorTable()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("MonitorsList", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("SerialId", entityType.FindProperty(nameof(MonitorEntity.SerialId))!.GetColumnName());
    }

    [TestMethod]
    public void SharedModel_MapsPostgreSqlMonitorTable()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("monitor", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("serial_id", entityType.FindProperty(nameof(MonitorEntity.SerialId))!.GetColumnName());
    }

    [TestMethod]
    [DataRow(typeof(RvtAlertRuleEntity), "RvtAlertRules", "rvt_alert_rule")]
    [DataRow(typeof(NotificationEntity), "Notifications", "notification")]
    [DataRow(typeof(NotificationSentEntity), "NotificationsSent", "notification_sent")]
    [DataRow(typeof(DeploymentEntity), "Deployments", "deployment")]
    [DataRow(typeof(ContractEntity), "Contracts", "contract")]
    [DataRow(typeof(SiteEntity), "Sites", "site")]
    [DataRow(typeof(SiteUserEntity), "SiteUsers", "site_user")]
    [DataRow(typeof(NotificationSettingEntity), "NotificationSettings", "notification_setting")]
    [DataRow(typeof(AspNetUserEntity), "AspNetUsers", "AspNetUsers")]
    [DataRow(typeof(SiteAverageEntity), "SiteAverages", "site_average")]
    [DataRow(typeof(ErrorMessageEntity), "ErrorMessages", "error_log")]
    [DataRow(typeof(AlertOccurrenceEntity), "AlertOccurrences", "alert_occurrence")]
    [DataRow(typeof(AlertDeliveryOutboxEntity), "AlertDeliveryOutbox", "alert_delivery_outbox")]
    public void SharedModel_MapsCommonTableNames(Type entityClrType, string sqlServerTable, string postgreSqlTable)
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
    public void SharedContext_ExposesDurableAlertSetsAndFactoryBoundary()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);

        Assert.AreSame(context.Set<AlertOccurrenceEntity>(), context.AlertOccurrences);
        Assert.AreSame(context.Set<AlertDeliveryOutboxEntity>(), context.AlertDeliveryOutbox);
        Assert.IsTrue(typeof(IMonitorDbContextFactory<TestMonitorContext>).IsAssignableFrom(typeof(TestContextFactory)));
    }

    private sealed class TestContextFactory(TestMonitorContext context)
        : IMonitorDbContextFactory<TestMonitorContext>
    {
        public TestMonitorContext CreateDbContext() => context;
    }

    [TestMethod]
    public void SharedModel_CachesPostgreSqlModelsByIdentifierMap()
    {
        using var firstContext = CreateContext(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MonitorsList"] = "first_monitor"
            });
        using var secondContext = CreateContext(
            MonitorDatabaseProvider.PostgreSql,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MonitorsList"] = "second_monitor"
            });

        var firstEntityType = firstContext.Model.FindEntityType(typeof(MonitorEntity));
        var secondEntityType = secondContext.Model.FindEntityType(typeof(MonitorEntity));

        Assert.IsNotNull(firstEntityType);
        Assert.IsNotNull(secondEntityType);
        Assert.AreEqual("first_monitor", firstEntityType.GetTableName());
        Assert.AreEqual("second_monitor", secondEntityType.GetTableName());
    }

    [DataTestMethod]
    [DataRow(MonitorDatabaseProvider.PostgreSql, "monitor_delivery_outbox", null, "producer")]
    [DataRow(MonitorDatabaseProvider.SqlServer, "MonitorDeliveryOutbox", "dbo", "Producer")]
    public void SharedModel_MapsDeliveryOutbox(
        MonitorDatabaseProvider provider,
        string table,
        string? schema,
        string producerColumn)
    {
        using var context = CreateContext(provider);
        var entity = context.Model.FindEntityType(typeof(MonitorDeliveryOutboxEntity));

        Assert.IsNotNull(entity);
        Assert.AreEqual(table, entity.GetTableName());
        Assert.AreEqual(schema, entity.GetSchema());
        Assert.AreEqual(producerColumn, entity.FindProperty(nameof(MonitorDeliveryOutboxEntity.Producer))!.GetColumnName());
        AssertDeliveryOutboxPropertyParity(entity, provider);
        var designTimeEntity = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(MonitorDeliveryOutboxEntity));
        Assert.IsNotNull(designTimeEntity);
        Assert.AreEqual(
            provider == MonitorDatabaseProvider.SqlServer ? "Latin1_General_100_BIN2" : null,
            designTimeEntity.FindProperty(nameof(MonitorDeliveryOutboxEntity.DeliveryKey))!.GetCollation());
        CollectionAssert.AreEqual(
            new[] { nameof(MonitorDeliveryOutboxEntity.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        var uniqueDeliveryIndex = entity.GetIndexes().Single(index => index.IsUnique);
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(MonitorDeliveryOutboxEntity.Producer),
                nameof(MonitorDeliveryOutboxEntity.DeliveryKey)
            },
            uniqueDeliveryIndex.Properties.Select(property => property.Name).ToArray());
        Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[]
            {
                nameof(MonitorDeliveryOutboxEntity.Producer),
                nameof(MonitorDeliveryOutboxEntity.Status),
                nameof(MonitorDeliveryOutboxEntity.NextAttemptAt)
            })));

        var notificationForeignKey = entity.GetForeignKeys().Single();
        Assert.AreEqual(typeof(NotificationEntity), notificationForeignKey.PrincipalEntityType.ClrType);
        CollectionAssert.AreEqual(
            new[] { nameof(MonitorDeliveryOutboxEntity.NotificationId) },
            notificationForeignKey.Properties.Select(property => property.Name).ToArray());
        Assert.AreEqual(DeleteBehavior.SetNull, notificationForeignKey.DeleteBehavior);

        var kindProperty = entity.FindProperty(nameof(MonitorDeliveryOutboxEntity.Kind));
        Assert.IsNotNull(kindProperty);
        var kindConverter = kindProperty.GetTypeMapping().Converter;
        Assert.IsNotNull(kindConverter);
        Assert.AreEqual(
            nameof(MonitorDeliveryKind.Email),
            kindConverter.ConvertToProvider(MonitorDeliveryKind.Email));
    }

    private static void AssertDeliveryOutboxPropertyParity(
        IReadOnlyEntityType entity,
        MonitorDatabaseProvider provider)
    {
        var expectedProperties = new[]
        {
            new OutboxPropertyExpectation("Id", "Id", "id", typeof(Guid), false, "uniqueidentifier", "uuid"),
            new OutboxPropertyExpectation("Producer", "Producer", "producer", typeof(string), false, "nvarchar(64)", "text", 64),
            new OutboxPropertyExpectation("NotificationId", "NotificationId", "notification_id", typeof(Guid?), true, "uniqueidentifier", "uuid"),
            new OutboxPropertyExpectation("CorrelationKey", "CorrelationKey", "correlation_key", typeof(string), true, "nvarchar(450)", "text", 450),
            new OutboxPropertyExpectation("DeliveryKey", "DeliveryKey", "delivery_key", typeof(string), false, "nvarchar(450)", "text", 450),
            new OutboxPropertyExpectation("Kind", "Kind", "kind", typeof(MonitorDeliveryKind), false, "nvarchar(64)", "text", 64),
            new OutboxPropertyExpectation("Destination", "Destination", "destination", typeof(string), false, "nvarchar(512)", "text", 512),
            new OutboxPropertyExpectation("PayloadVersion", "PayloadVersion", "payload_version", typeof(int), false, "int", "integer"),
            new OutboxPropertyExpectation("Payload", "Payload", "payload", typeof(string), false, "nvarchar(max)", "text"),
            new OutboxPropertyExpectation("Status", "Status", "status", typeof(string), false, "nvarchar(32)", "text", 32),
            new OutboxPropertyExpectation("AttemptCount", "AttemptCount", "attempt_count", typeof(int), false, "int", "integer"),
            new OutboxPropertyExpectation("NextAttemptAt", "NextAttemptAt", "next_attempt_at", typeof(DateTime), false, "datetime2", "timestamp with time zone"),
            new OutboxPropertyExpectation("LeaseId", "LeaseId", "lease_id", typeof(Guid?), true, "uniqueidentifier", "uuid"),
            new OutboxPropertyExpectation("LeaseUntil", "LeaseUntil", "lease_until", typeof(DateTime?), true, "datetime2", "timestamp with time zone"),
            new OutboxPropertyExpectation("CompletedAt", "CompletedAt", "completed_at", typeof(DateTime?), true, "datetime2", "timestamp with time zone"),
            new OutboxPropertyExpectation("DeadLetteredAt", "DeadLetteredAt", "dead_lettered_at", typeof(DateTime?), true, "datetime2", "timestamp with time zone"),
            new OutboxPropertyExpectation("LastError", "LastError", "last_error", typeof(string), true, "nvarchar(1024)", "text", 1024),
            new OutboxPropertyExpectation("CreatedAt", "CreatedAt", "created_at", typeof(DateTime), false, "datetime2", "timestamp with time zone")
        };

        Assert.HasCount(expectedProperties.Length, entity.GetProperties());
        foreach (var expected in expectedProperties)
        {
            var property = entity.FindProperty(expected.PropertyName);
            Assert.IsNotNull(property, $"Missing property {expected.PropertyName}.");
            Assert.AreEqual(
                provider == MonitorDatabaseProvider.PostgreSql ? expected.PostgreSqlColumn : expected.SqlServerColumn,
                property.GetColumnName(),
                $"Unexpected column name for {expected.PropertyName}.");
            Assert.AreEqual(expected.ClrType, property.ClrType, $"Unexpected CLR type for {expected.PropertyName}.");
            Assert.AreEqual(expected.IsNullable, property.IsNullable, $"Unexpected nullability for {expected.PropertyName}.");
            Assert.AreEqual(
                provider == MonitorDatabaseProvider.PostgreSql ? expected.PostgreSqlStoreType : expected.SqlServerStoreType,
                property.GetRelationalTypeMapping().StoreType,
                $"Unexpected store type for {expected.PropertyName}.");
            Assert.AreEqual(
                provider == MonitorDatabaseProvider.PostgreSql ? null : expected.SqlServerMaxLength,
                property.GetMaxLength(),
                $"Unexpected max length for {expected.PropertyName}.");
        }
    }

    private static TestMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        return CreateContext(provider, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static TestMonitorContext CreateContext(
        MonitorDatabaseProvider provider,
        IReadOnlyDictionary<string, string> identifierMap)
    {
        var monitorOptions = new MonitorDbOptions(provider, identifierMap);
        var dbOptionsBuilder = new DbContextOptionsBuilder<TestMonitorContext>();
        if (provider == MonitorDatabaseProvider.PostgreSql)
        {
            dbOptionsBuilder.UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata");
        }
        else
        {
            dbOptionsBuilder.UseSqlServer(
                "Server=localhost;Database=metadata;User Id=metadata;Password=metadata;TrustServerCertificate=true");
        }

        return new TestMonitorContext(dbOptionsBuilder.Options, monitorOptions);
    }
}

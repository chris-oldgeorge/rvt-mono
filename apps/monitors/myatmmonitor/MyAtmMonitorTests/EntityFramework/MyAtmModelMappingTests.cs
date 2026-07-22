using Microsoft.EntityFrameworkCore;
using MyAtm.Api.Db;
using MyAtm.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;

namespace MyAtmMonitorTests.EntityFramework;

[TestClass]
public sealed class MyAtmModelMappingTests
{
    [TestMethod]
    public void MyAtmContext_MapsDustLevelForSqlServer()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(MyAtmDustLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("MyAtmDustLevels", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("Pm10", entityType.FindProperty(nameof(MyAtmDustLevelEntity.Pm10))!.GetColumnName());
    }

    [TestMethod]
    public void MyAtmContext_MapsDustLevelForPostgreSql()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(MyAtmDustLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("my_atm_dust_level", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("pm_10", entityType.FindProperty(nameof(MyAtmDustLevelEntity.Pm10))!.GetColumnName());
    }

    [DataTestMethod]
    [DataRow(MonitorDatabaseProvider.SqlServer, "MyAtmAlertOccurrences", "dbo", "OccurrenceKey", "MonitorDeliveryOutbox", "DeliveryKey", "Producer")]
    [DataRow(MonitorDatabaseProvider.PostgreSql, "my_atm_alert_occurrence", null, "occurrence_key", "monitor_delivery_outbox", "delivery_key", "producer")]
    public void MyAtmContext_KeepsOccurrenceAndMapsOnlyTheSharedDeliveryOutbox(
        MonitorDatabaseProvider provider,
        string occurrenceTable,
        string? schema,
        string occurrenceKeyColumn,
        string outboxTable,
        string deliveryKeyColumn,
        string producerColumn)
    {
        using var context = CreateContext(provider);
        var occurrence = context.Model.FindEntityType(typeof(MyAtmAlertOccurrenceEntity));
        var outbox = context.Model.FindEntityType(typeof(MonitorDeliveryOutboxEntity));

        Assert.IsNotNull(occurrence);
        Assert.IsNotNull(outbox);
        Assert.IsFalse(context.Model.GetEntityTypes().Any(entity =>
            entity.ClrType.Name == "MyAtmOutboxMessageEntity"));
        Assert.AreEqual(occurrenceTable, occurrence.GetTableName());
        Assert.AreEqual(schema, occurrence.GetSchema());
        Assert.AreEqual(occurrenceKeyColumn, occurrence.FindProperty(nameof(MyAtmAlertOccurrenceEntity.OccurrenceKey))!.GetColumnName());
        Assert.AreEqual(outboxTable, outbox.GetTableName());
        Assert.AreEqual(deliveryKeyColumn, outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.DeliveryKey))!.GetColumnName());
        Assert.AreEqual(producerColumn, outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.Producer))!.GetColumnName());
        var leaseId = outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.LeaseId));
        Assert.IsNotNull(leaseId);
        Assert.IsTrue(leaseId.IsNullable);
        Assert.AreEqual(
            provider == MonitorDatabaseProvider.PostgreSql ? "lease_id" : "LeaseId",
            leaseId.GetColumnName());
        Assert.IsTrue(outbox.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(MonitorDeliveryOutboxEntity.Producer),
                nameof(MonitorDeliveryOutboxEntity.Status),
                nameof(MonitorDeliveryOutboxEntity.NextAttemptAt)
            })));
        Assert.IsFalse(outbox.GetForeignKeys().Any(foreignKey => foreignKey.PrincipalEntityType == occurrence));
    }

    private static MyAtmMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var options = new MonitorDbOptions(provider, new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<MyAtmMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MyAtmMonitorContext(dbOptions, options);
    }
}

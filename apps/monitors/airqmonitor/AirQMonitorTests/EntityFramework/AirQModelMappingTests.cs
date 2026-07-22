using AirQ.Api.Db;
using AirQ.Api.Db.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;

namespace AirQMonitorTests.EntityFramework;

[TestClass]
public sealed class AirQModelMappingTests
{
    [TestMethod]
    public void AirQContext_MapsNoiseLevelForSqlServer()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(AirQNoiseLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("AirQNoiseLevels", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("LAeq", entityType.FindProperty(nameof(AirQNoiseLevelEntity.LAeq))!.GetColumnName());
    }

    [TestMethod]
    public void AirQContext_MapsNoiseLevelForPostgreSql()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(AirQNoiseLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("air_q_noise_level", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("laeq", entityType.FindProperty(nameof(AirQNoiseLevelEntity.LAeq))!.GetColumnName());
    }

    private static AirQMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var options = new MonitorDbOptions(provider, new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<AirQMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AirQMonitorContext(dbOptions, options);
    }
}

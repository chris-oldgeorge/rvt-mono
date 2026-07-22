using Microsoft.EntityFrameworkCore;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Svantek.Api.Db;
using Svantek.Api.Db.EntityFramework;

namespace SvantekMonitorTests.EntityFramework;

[TestClass]
public sealed class SvantekModelMappingTests
{
    [TestMethod]
    public void SvantekContext_MapsNoiseLevelForSqlServer()
    {
        using var context = CreateContext(MonitorDatabaseProvider.SqlServer);
        var entityType = context.Model.FindEntityType(typeof(SvantekNoiseLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("SvantekNoiseLevels", entityType.GetTableName());
        Assert.AreEqual("dbo", entityType.GetSchema());
        Assert.AreEqual("LAmax", entityType.FindProperty(nameof(SvantekNoiseLevelEntity.LAmax))!.GetColumnName());
    }

    [TestMethod]
    public void SvantekContext_MapsNoiseLevelForPostgreSql()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(SvantekNoiseLevelEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("svantek_noise_level", entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
        Assert.AreEqual("lamax", entityType.FindProperty(nameof(SvantekNoiseLevelEntity.LAmax))!.GetColumnName());
    }

    [TestMethod]
    public void SvantekContext_MapsStatusAndEightHourAverageTables()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);

        Assert.AreEqual("svantek_monitor_status", context.Model.FindEntityType(typeof(SvantekMonitorStatusEntity))!.GetTableName());
        Assert.AreEqual("svantek_noise_8_hour_average", context.Model.FindEntityType(typeof(SvantekNoise8HourAverageEntity))!.GetTableName());
    }

    [TestMethod]
    public void SvantekContext_MapsDeploymentForPostgreSqlSchema()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(DeploymentEntity));

        Assert.IsNotNull(entityType);
        Assert.AreEqual("deployment", entityType.GetTableName());
        Assert.IsNull(entityType.FindProperty(nameof(DeploymentEntity.What2words)));
        Assert.AreEqual("what_3_words", entityType.FindProperty(nameof(DeploymentEntity.What3Words))!.GetColumnName());
    }

    [TestMethod]
    public void SvantekContext_ConvertsPostgreSqlTextBooleans()
    {
        using var context = CreateContext(MonitorDatabaseProvider.PostgreSql);
        var entityType = context.Model.FindEntityType(typeof(SvantekMonitorStatusEntity));
        var converter = entityType!
            .FindProperty(nameof(SvantekMonitorStatusEntity.Active))!
            .GetTypeMapping()
            .Converter;

        Assert.IsNotNull(converter);
        Assert.IsTrue((bool)converter.ConvertFromProvider("1")!);
        Assert.IsFalse((bool)converter.ConvertFromProvider("0")!);
        Assert.IsTrue((bool)converter.ConvertFromProvider("True")!);
        Assert.AreEqual("1", converter.ConvertToProvider(true));
        Assert.AreEqual("0", converter.ConvertToProvider(false));
    }

    private static SvantekMonitorContext CreateContext(MonitorDatabaseProvider provider)
    {
        var options = new MonitorDbOptions(provider, new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<SvantekMonitorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SvantekMonitorContext(dbOptions, options);
    }
}

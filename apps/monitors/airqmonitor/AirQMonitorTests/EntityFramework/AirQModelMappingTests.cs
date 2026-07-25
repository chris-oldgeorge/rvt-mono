using AirQ.Api.Db.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;

namespace AirQMonitorTests.EntityFramework;

[TestClass]
public sealed class AirQModelMappingTests
{
    [TestMethod]
    [DataRow(typeof(AirQNoiseLevelEntity), "air_q_noise_level")]
    [DataRow(typeof(AirQMonitorStatusEntity), "air_q_monitor_status")]
    [DataRow(typeof(AirQErrorMessageEntity), "air_q_error_message")]
    [DataRow(typeof(AirQNoise8HourAverageEntity), "air_q_noise_8_hour_average")]
    public void AirQContext_MapsCanonicalTablesWithoutSchemas(Type entityClrType, string tableName)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(entityType);
        Assert.AreEqual(tableName, entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
    }

    [TestMethod]
    public void AirQContext_MapsCanonicalColumnsAndTimestampTypes()
    {
        using var context = CreateContext();

        AssertColumns(
            context.Model.FindEntityType(typeof(AirQNoiseLevelEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("LAeq", "laeq"),
            ("LAmax", "lamax"),
            ("LA90", "la_90"),
            ("LA10", "la_10"),
            ("LCeq", "lceq"),
            ("LCmax", "lcmax"),
            ("LC90", "lc_90"),
            ("LC10", "lc_10"));
        AssertColumns(
            context.Model.FindEntityType(typeof(AirQMonitorStatusEntity))!,
            ("Id", "id"),
            ("SerialId", "serial_id"),
            ("UpdateTime", "update_time"),
            ("Status", "status"),
            ("ErrorCount", "error_count"),
            ("BatteryVoltage", "battery_voltage"),
            ("CalibrationDate", "calibration_date"),
            ("FilterChangeDate", "filter_change_date"),
            ("PumpHours", "pump_hours"));
        AssertColumns(
            context.Model.FindEntityType(typeof(AirQErrorMessageEntity))!,
            ("Tag", "tag"),
            ("Error", "error"),
            ("ErrorTime", "error_time"));
        AssertColumns(
            context.Model.FindEntityType(typeof(AirQNoise8HourAverageEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("LAeq", "laeq"),
            ("LAmax", "lamax"),
            ("LA90", "la_90"),
            ("LA10", "la_10"),
            ("LCeq", "lceq"),
            ("LCmax", "lcmax"),
            ("LC90", "lc_90"),
            ("LC10", "lc_10"),
            ("NumberOfSamples", "number_of_samples"));

        AssertTimestamp(context, typeof(AirQNoiseLevelEntity), nameof(AirQNoiseLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(AirQMonitorStatusEntity), nameof(AirQMonitorStatusEntity.UpdateTime));
        AssertTimestamp(context, typeof(AirQMonitorStatusEntity), nameof(AirQMonitorStatusEntity.CalibrationDate));
        AssertTimestamp(context, typeof(AirQMonitorStatusEntity), nameof(AirQMonitorStatusEntity.FilterChangeDate));
        AssertTimestamp(context, typeof(AirQErrorMessageEntity), nameof(AirQErrorMessageEntity.ErrorTime));
        AssertTimestamp(context, typeof(AirQNoise8HourAverageEntity), nameof(AirQNoise8HourAverageEntity.SampleTime));
    }

    [TestMethod]
    public void AirQContext_PreservesKeysAndSharedMonitorIndex()
    {
        using var context = CreateContext();

        AssertKey(context, typeof(AirQNoiseLevelEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(AirQMonitorStatusEntity), "SerialId");
        AssertKey(context, typeof(AirQErrorMessageEntity), "Tag", "ErrorTime", "Error");
        AssertKey(context, typeof(AirQNoise8HourAverageEntity), "SerialId", "SampleTime");

        var monitor = context.Model.FindEntityType(typeof(MonitorEntity));
        Assert.IsNotNull(monitor);
        Assert.AreEqual(
            "ix_monitor_serial_id_type_of_monitor",
            monitor.GetIndexes().Single().GetDatabaseName());
    }

    private static void AssertColumns(
        IReadOnlyEntityType entityType,
        params (string Property, string Column)[] expectedColumns)
    {
        Assert.HasCount(expectedColumns.Length, entityType.GetProperties());
        foreach (var expected in expectedColumns)
        {
            Assert.AreEqual(
                expected.Column,
                entityType.FindProperty(expected.Property)!.GetColumnName(),
                expected.Property);
        }
    }

    private static void AssertTimestamp(
        AirQMonitorContext context,
        Type entityClrType,
        string propertyName)
    {
        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual("timestamp with time zone", property.GetRelationalTypeMapping().StoreType);
    }

    private static void AssertKey(
        AirQMonitorContext context,
        Type entityClrType,
        params string[] expectedProperties)
    {
        var keyProperties = context.Model
            .FindEntityType(entityClrType)!
            .FindPrimaryKey()!
            .Properties
            .Select(property => property.Name)
            .ToArray();
        CollectionAssert.AreEqual(expectedProperties, keyProperties);
    }

    private static AirQMonitorContext CreateContext()
    {
        var options = new MonitorDbOptions(new Dictionary<string, string>());
        var dbOptions = new DbContextOptionsBuilder<AirQMonitorContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;

        return new AirQMonitorContext(dbOptions, options);
    }
}

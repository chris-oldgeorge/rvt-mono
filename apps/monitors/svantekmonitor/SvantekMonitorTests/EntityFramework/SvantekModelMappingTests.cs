using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;
using Svantek.Api.Db.EntityFramework;

namespace SvantekMonitorTests.EntityFramework;

[TestClass]
public sealed class SvantekModelMappingTests
{
    [TestMethod]
    [DataRow(typeof(SvantekMonitorStatusEntity), "svantek_monitor_status")]
    [DataRow(typeof(SvantekNoiseLevelEntity), "svantek_noise_level")]
    [DataRow(typeof(SvantekNoise8HourAverageEntity), "svantek_noise_8_hour_average")]
    [DataRow(typeof(SvantekErrorMessageEntity), "svantek_error_message")]
    public void SvantekContext_MapsCanonicalTablesWithoutSchemas(Type entityClrType, string tableName)
    {
        using SvantekMonitorContext context = CreateContext();
        IEntityType? entityType = context.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(entityType);
        Assert.AreEqual(tableName, entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
    }

    [TestMethod]
    public void SvantekContext_MapsCanonicalColumnsAndTimestampTypes()
    {
        using SvantekMonitorContext context = CreateContext();

        AssertColumns(
            context.Model.FindEntityType(typeof(SvantekMonitorStatusEntity))!,
            ("SerialId", "serial_id"),
            ("UpdateTime", "update_time"),
            ("Status", "status"),
            ("ErrorCount", "error_count"),
            ("BatteryVoltage", "battery_voltage"),
            ("CalibrationDate", "calibration_date"),
            ("FilterChangeDate", "filter_change_date"),
            ("PumpHours", "pump_hours"),
            ("ProjectId", "project_id"),
            ("PointId", "point_id"),
            ("Active", "active"),
            ("LastLogin", "lastlogin"),
            ("LastLogout", "lastlogout"),
            ("IsOnline", "isonline"),
            ("LastStatusTimestamp", "laststatustimestamp"),
            ("BatteryCharge", "batterycharge"),
            ("BatteryTimeToEmpty", "batterytimetoempty"),
            ("PowerSource", "powersource"),
            ("IsBatteryCharging", "isbatterycharging"),
            ("GsmSignalQuality", "gsmsignalquality"),
            ("MeasurementState", "measurementstate"));
        AssertColumns(
            context.Model.FindEntityType(typeof(SvantekNoiseLevelEntity))!,
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
            context.Model.FindEntityType(typeof(SvantekNoise8HourAverageEntity))!,
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
        AssertColumns(
            context.Model.FindEntityType(typeof(SvantekErrorMessageEntity))!,
            ("Tag", "tag"),
            ("Error", "error"),
            ("ErrorTime", "error_time"));

        AssertTimestamp(context, typeof(SvantekMonitorStatusEntity), nameof(SvantekMonitorStatusEntity.UpdateTime));
        AssertTimestamp(context, typeof(SvantekMonitorStatusEntity), nameof(SvantekMonitorStatusEntity.CalibrationDate));
        AssertTimestamp(context, typeof(SvantekMonitorStatusEntity), nameof(SvantekMonitorStatusEntity.FilterChangeDate));
        AssertTimestamp(context, typeof(SvantekNoiseLevelEntity), nameof(SvantekNoiseLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(SvantekNoise8HourAverageEntity), nameof(SvantekNoise8HourAverageEntity.SampleTime));
        AssertTimestamp(context, typeof(SvantekErrorMessageEntity), nameof(SvantekErrorMessageEntity.ErrorTime));
    }

    private static readonly string[] expected = ["SerialId", "TypeOfMonitor"];

    [TestMethod]
    public void SvantekContext_PreservesKeysAndSharedMonitorIndex()
    {
        using SvantekMonitorContext context = CreateContext();

        AssertKey(context, typeof(SvantekMonitorStatusEntity), "SerialId");
        AssertKey(context, typeof(SvantekNoiseLevelEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(SvantekNoise8HourAverageEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(SvantekErrorMessageEntity), "Tag", "ErrorTime", "Error");

        IEntityType? monitor = context.Model.FindEntityType(typeof(MonitorEntity));
        Assert.IsNotNull(monitor);
        IIndex index = monitor.GetIndexes().Single();
        Assert.AreEqual(
            "ix_monitor_serial_id_type_of_monitor",
            index.GetDatabaseName());
        CollectionAssert.AreEqual(
            expected,
            index.Properties.Select(property => property.Name).ToArray());
        Assert.IsFalse(index.IsUnique);
    }

    [TestMethod]
    public void SvantekContext_MapsCanonicalDeploymentAndNotificationProperties()
    {
        using SvantekMonitorContext context = CreateContext();
        IEntityType? deployment = context.Model.FindEntityType(typeof(DeploymentEntity));
        IEntityType? notification = context.Model.FindEntityType(typeof(NotificationEntity));

        Assert.IsNotNull(deployment);
        Assert.IsNull(deployment.FindProperty(nameof(DeploymentEntity.What2words)));
        Assert.AreEqual(
            "what_3_words",
            deployment.FindProperty(nameof(DeploymentEntity.What3Words))!.GetColumnName());
        Assert.IsNotNull(notification);
        IProperty? recordingLink = notification.FindProperty("RecordingLink");
        Assert.IsNotNull(recordingLink);
        Assert.AreEqual(
            "recording_link",
            recordingLink.GetColumnName());
        Assert.IsTrue(recordingLink.IsShadowProperty());
        Assert.AreEqual(PropertySaveBehavior.Ignore, recordingLink.GetBeforeSaveBehavior());
        Assert.AreEqual(PropertySaveBehavior.Save, recordingLink.GetAfterSaveBehavior());
    }

    [TestMethod]
    public void SvantekContext_PreservesTextBooleanConversion()
    {
        using SvantekMonitorContext context = CreateContext();
        IEntityType? entityType = context.Model.FindEntityType(typeof(SvantekMonitorStatusEntity));

        foreach (string? propertyName in new[]
                 {
                     nameof(SvantekMonitorStatusEntity.Active),
                     nameof(SvantekMonitorStatusEntity.IsOnline),
                     nameof(SvantekMonitorStatusEntity.IsBatteryCharging)
                 })
        {
            IProperty property = entityType!.FindProperty(propertyName)!;
            ValueConverter? converter = property.GetTypeMapping().Converter;

            Assert.IsNotNull(converter);
            Assert.AreEqual("text", property.GetRelationalTypeMapping().StoreType);
            Assert.IsTrue((bool)converter.ConvertFromProvider("1")!);
            Assert.IsFalse((bool)converter.ConvertFromProvider("0")!);
            Assert.IsTrue((bool)converter.ConvertFromProvider("True")!);
            Assert.AreEqual("1", converter.ConvertToProvider(true));
            Assert.AreEqual("0", converter.ConvertToProvider(false));
        }
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
        SvantekMonitorContext context,
        Type entityClrType,
        string propertyName)
    {
        IProperty? property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual("timestamp with time zone", property.GetRelationalTypeMapping().StoreType);
    }

    private static void AssertKey(
        SvantekMonitorContext context,
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

    private static SvantekMonitorContext CreateContext()
    {
        MonitorDbOptions options = new(new Dictionary<string, string>());
        DbContextOptions<SvantekMonitorContext> dbOptions = new DbContextOptionsBuilder<SvantekMonitorContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;

        return new SvantekMonitorContext(dbOptions, options);
    }
}

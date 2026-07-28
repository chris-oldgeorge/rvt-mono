using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MyAtm.Api.Db.EntityFramework;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.Entities;

namespace MyAtmMonitorTests.EntityFramework;

[TestClass]
public sealed class MyAtmModelMappingTests
{
    [TestMethod]
    [DataRow(typeof(MyAtmDustLevelEntity), "my_atm_dust_level")]
    [DataRow(typeof(MyAtmAccessoryInfoEntity), "my_atm_accessory_info")]
    [DataRow(typeof(MyAtmErrorMessageEntity), "my_atm_error_message")]
    [DataRow(typeof(MyAtmAlertOccurrenceEntity), "my_atm_alert_occurrence")]
    public void MyAtmContext_MapsCanonicalTablesWithoutSchemas(Type entityClrType, string tableName)
    {
        using MyAtmMonitorContext context = CreateContext();
        IEntityType? entityType = context.Model.FindEntityType(entityClrType);

        Assert.IsNotNull(entityType);
        Assert.AreEqual(tableName, entityType.GetTableName());
        Assert.IsNull(entityType.GetSchema());
    }

    [TestMethod]
    public void MyAtmContext_MapsCanonicalColumnsAndTimestampTypes()
    {
        using MyAtmMonitorContext context = CreateContext();

        AssertColumns(
            context.Model.FindEntityType(typeof(MyAtmDustLevelEntity))!,
            ("SerialId", "serial_id"),
            ("Avrg", "avrg"),
            ("SampleTime", "sample_time"),
            ("Pm1", "pm_1"),
            ("Pm2_5", "pm_2_5"),
            ("Pm10", "pm_10"),
            ("PmTotal", "pm_total"),
            ("Weather_t", "weather_t"),
            ("Weather_p", "weather_p"),
            ("Weather_rh", "weather_rh"));
        AssertColumns(
            context.Model.FindEntityType(typeof(MyAtmAccessoryInfoEntity))!,
            ("SerialId", "serial_id"),
            ("SampleTime", "sample_time"),
            ("OperatingSpanPointDeviation", "operating_span_point_deviation"),
            ("OperatingTLed", "operating_t_led"),
            ("OperatingTHeating", "operating_t_heating"),
            ("OperatingVolumeFlow", "operating_volume_flow"),
            ("OperatingVolumeFlowSignalLength", "operating_volume_flow_signal_length"),
            ("OperatingVolumeFlowTimestamp", "operating_volume_flow_time"),
            ("OperatingPeakPosition15s", "operating_peak_position_15_s"),
            ("OperatingVelocity", "operating_velocity"),
            ("OperatingSlaNoiseLevel", "operating_sla_noise_level"),
            ("OperatingSlaOffsetAdjustmentVoltage", "operating_sla_offset_adjustment_voltage"),
            ("OperatingTMio", "operating_tmio"),
            ("OperatingPMio", "operating_pmio"),
            ("OperatingRHMio", "operating_rh_mio"),
            ("OperatingAutoCalibrationPeakPosition", "operating_auto_calibration_peak_position"),
            ("OperatingPowerLed", "operating_power_led"),
            ("OperatingPowerPmt", "operating_power_pmt"),
            ("OperatingPowerHeating", "operating_power_heating"),
            ("OperatingPowerVolumeFlowBlower", "operating_power_volume_flow_blower"),
            ("OperatingPowerHousingBlower", "operating_power_housing_blower"),
            ("OperatingPowerSeparatorBlower", "operating_power_separator_blower"),
            ("OperatingFlowCorrectionFactor", "operating_flow_correction_factor"),
            ("DigitalCalibrationEnableStatus", "digital_calibration_enable_status"),
            ("DigitalIadsConnected", "digital_iads_connected"),
            ("DigitalIadsActivated", "digital_iads_activated"),
            ("DigitalAmbientProtectionAttached", "digital_ambient_protection_attached"),
            ("DigitalCoincidence", "digital_coincidence"),
            ("DigitalWeatherStation", "digital_weather_station"),
            ("DigitalOperatingModus", "digital_operating_modus"),
            ("DigitalVolumeFlow", "digital_volume_flow"),
            ("DigitalSuction", "digital_suction"),
            ("DigitalIads", "digital_iads"),
            ("DigitalCalibration", "digital_calibration"),
            ("DigitalSensorLed", "digital_sensor_led"),
            ("DigitalSensorData", "digital_sensor_data"),
            ("DigitalSensorNoise", "digital_sensor_noise"),
            ("DigitalCountModus", "digital_count_modus"),
            ("DigitalLiquidPumps", "digital_liquid_pumps"),
            ("DigitalCondensationCooling", "digital_condensation_cooling"),
            ("DigitalDropletSize", "digital_droplet_size"),
            ("DigitalOpticsTemperature", "digital_optics_temperature"),
            ("DigitalGlobalWarning", "digital_global_warning"),
            ("DigitalGlobalError", "digital_global_error"),
            ("DigitalEvaporationHeating", "digital_evaporation_heating"));
        AssertColumns(
            context.Model.FindEntityType(typeof(MyAtmErrorMessageEntity))!,
            ("Tag", "tag"),
            ("Error", "error"),
            ("ErrorTime", "error_time"));
        AssertColumns(
            context.Model.FindEntityType(typeof(MyAtmAlertOccurrenceEntity))!,
            ("OccurrenceKey", "occurrence_key"),
            ("NotificationId", "notification_id"),
            ("MonitorId", "monitor_id"),
            ("RuleId", "rule_id"),
            ("Period", "period"),
            ("AlertType", "alert_type"),
            ("Field", "field"),
            ("Level", "level"),
            ("TriggeredAt", "triggered_at"),
            ("IsSuppressed", "is_suppressed"),
            ("CreatedAt", "created_at"));

        AssertTimestamp(context, typeof(MyAtmDustLevelEntity), nameof(MyAtmDustLevelEntity.SampleTime));
        AssertTimestamp(context, typeof(MyAtmAccessoryInfoEntity), nameof(MyAtmAccessoryInfoEntity.SampleTime));
        AssertTimestamp(context, typeof(MyAtmErrorMessageEntity), nameof(MyAtmErrorMessageEntity.ErrorTime));
        AssertTimestamp(context, typeof(MyAtmAlertOccurrenceEntity), nameof(MyAtmAlertOccurrenceEntity.TriggeredAt));
        AssertTimestamp(context, typeof(MyAtmAlertOccurrenceEntity), nameof(MyAtmAlertOccurrenceEntity.CreatedAt));
    }

    private static readonly string[] expected = ["SerialId", "TypeOfMonitor"];

    [TestMethod]
    public void MyAtmContext_PreservesKeysAndSharedMonitorIndex()
    {
        using MyAtmMonitorContext context = CreateContext();

        AssertKey(context, typeof(MyAtmDustLevelEntity), "SerialId", "SampleTime", "Avrg");
        AssertKey(context, typeof(MyAtmAccessoryInfoEntity), "SerialId", "SampleTime");
        AssertKey(context, typeof(MyAtmErrorMessageEntity), "Tag", "ErrorTime", "Error");
        AssertKey(context, typeof(MyAtmAlertOccurrenceEntity), "OccurrenceKey");

        IEntityType? monitor = context.Model.FindEntityType(typeof(MonitorEntity));
        Assert.IsNotNull(monitor);
        IIndex index = monitor.GetIndexes().Single();
        Assert.AreEqual("ix_monitor_serial_id_type_of_monitor", index.GetDatabaseName());
        CollectionAssert.AreEqual(
            expected,
            index.Properties.Select(property => property.Name).ToArray());
        Assert.IsFalse(index.IsUnique);
    }

    [TestMethod]
    public void MyAtmContext_MapsOnlyTheCanonicalSharedDeliveryOutbox()
    {
        using MyAtmMonitorContext context = CreateContext();
        IEntityType? occurrence = context.Model.FindEntityType(typeof(MyAtmAlertOccurrenceEntity));
        IEntityType? outbox = context.Model.FindEntityType(typeof(MonitorDeliveryOutboxEntity));

        Assert.IsNotNull(occurrence);
        Assert.IsNotNull(outbox);
        Assert.IsFalse(context.Model.GetEntityTypes().Any(entity =>
            entity.ClrType.Name == "MyAtmOutboxMessageEntity"));
        Assert.AreEqual("monitor_delivery_outbox", outbox.GetTableName());
        Assert.IsNull(outbox.GetSchema());
        Assert.AreEqual("producer", outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.Producer))!.GetColumnName());
        Assert.AreEqual("delivery_key", outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.DeliveryKey))!.GetColumnName());
        Assert.AreEqual("lease_id", outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.LeaseId))!.GetColumnName());
        Assert.IsTrue(outbox.FindProperty(nameof(MonitorDeliveryOutboxEntity.LeaseId))!.IsNullable);
        Assert.IsFalse(outbox.GetForeignKeys().Any(foreignKey => foreignKey.PrincipalEntityType == occurrence));

        AssertIndex(
            outbox,
            "uq_monitor_delivery_outbox_producer_delivery_key",
            true,
            "Producer",
            "DeliveryKey");
        AssertIndex(
            outbox,
            "ix_monitor_delivery_outbox_due",
            false,
            "Producer",
            "Status",
            "NextAttemptAt");
        AssertIndex(
            outbox,
            "ix_monitor_delivery_outbox_notification_id",
            false,
            "NotificationId");

        AssertTimestamp(context, typeof(MonitorDeliveryOutboxEntity), nameof(MonitorDeliveryOutboxEntity.NextAttemptAt));
        AssertTimestamp(context, typeof(MonitorDeliveryOutboxEntity), nameof(MonitorDeliveryOutboxEntity.LeaseUntil));
        AssertTimestamp(context, typeof(MonitorDeliveryOutboxEntity), nameof(MonitorDeliveryOutboxEntity.CompletedAt));
        AssertTimestamp(context, typeof(MonitorDeliveryOutboxEntity), nameof(MonitorDeliveryOutboxEntity.DeadLetteredAt));
        AssertTimestamp(context, typeof(MonitorDeliveryOutboxEntity), nameof(MonitorDeliveryOutboxEntity.CreatedAt));
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
        MyAtmMonitorContext context,
        Type entityClrType,
        string propertyName)
    {
        IProperty? property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual("timestamp with time zone", property.GetRelationalTypeMapping().StoreType);
    }

    private static void AssertKey(
        MyAtmMonitorContext context,
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

    private static void AssertIndex(
        IReadOnlyEntityType entityType,
        string databaseName,
        bool unique,
        params string[] properties)
    {
        IReadOnlyIndex index = entityType.GetIndexes().Single(candidate => candidate.GetDatabaseName() == databaseName);
        Assert.AreEqual(unique, index.IsUnique);
        CollectionAssert.AreEqual(
            properties,
            index.Properties.Select(property => property.Name).ToArray());
    }

    private static MyAtmMonitorContext CreateContext()
    {
        MonitorDbOptions options = new(new Dictionary<string, string>());
        DbContextOptions<MyAtmMonitorContext> dbOptions = new DbContextOptionsBuilder<MyAtmMonitorContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;

        return new MyAtmMonitorContext(dbOptions, options);
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Omnidots.Api.UseCases;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using AlarmAlarms = Omnidots.Model.Json.AlarmDataV2.Alarms;
using AlarmAxes = Omnidots.Model.Json.AlarmDataV2.Axes;
using AlarmAxis = Omnidots.Model.Json.AlarmDataV2.Axis;
using AlarmData = Omnidots.Model.Json.AlarmDataV2.Data;
using AlarmVtop = Omnidots.Model.Json.AlarmDataV2.Vtop;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class OmnidotsAlarmTranslatorTests
{
    private static readonly TimeSpan SuppressionWindow = TimeSpan.FromMinutes(5);
    private readonly OmnidotsAlarmTranslator translator = new();

    [TestMethod]
    public void Translate_ValidAlert_ReturnsExactCommonSignal()
    {
        var body = Encoding.UTF8.GetBytes("authenticated raw body");
        var alarm = ValidAlarm();

        var signal = translator.Translate(alarm, body, SuppressionWindow);

        Assert.AreEqual("omnidots.webhook", signal.Source);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(body)), signal.SourceEventKey);
        Assert.AreEqual(new DateTime(2024, 7, 15, 10, 0, 0, DateTimeKind.Utc), signal.EventTime);
        Assert.AreEqual("23423", signal.SerialId);
        Assert.AreEqual(AlertType.Alert, signal.AlertType);
        Assert.AreEqual("vtop x", signal.Field);
        Assert.AreEqual(12, signal.Level);
        Assert.AreEqual(10, signal.Limit);
        Assert.AreEqual(0, signal.AveragingPeriod);
        Assert.AreEqual(
            AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms,
            signal.DeliveryChannels);
        Assert.AreEqual("Vibration Alert vtop x level=12 limit=10", signal.Message);
        Assert.AreEqual(SuppressionWindow, signal.SuppressionWindow);
    }

    [DataRow("data")]
    [DataRow("alarms")]
    [DataRow("axes")]
    [DataRow("x")]
    [DataRow("y")]
    [DataRow("z")]
    [DataRow("x-vtop")]
    [DataRow("y-vtop")]
    [DataRow("z-vtop")]
    [DataTestMethod]
    public void Translate_MissingRequiredNestedObject_RejectsPayload(string missingObject)
    {
        var alarm = ValidAlarm();
        var data = alarm.Data1!;
        var axes = data.Axes!;
        switch (missingObject)
        {
            case "data":
                alarm.Data1 = null!;
                break;
            case "alarms":
                data.Alarms = null;
                break;
            case "axes":
                data.Axes = null;
                break;
            case "x":
                axes.X = null;
                break;
            case "y":
                axes.Y = null;
                break;
            case "z":
                axes.Z = null;
                break;
            case "x-vtop":
                axes.X!.Vtop = null;
                break;
            case "y-vtop":
                axes.Y!.Vtop = null;
                break;
            case "z-vtop":
                axes.Z!.Vtop = null;
                break;
            default:
                Assert.Fail($"Unknown test case {missingObject}.");
                break;
        }

        Assert.ThrowsExactly<AdapterException>(() =>
            translator.Translate(alarm, [1, 2, 3], SuppressionWindow));
    }

    [DataRow(0)]
    [DataRow(-1)]
    [DataTestMethod]
    public void Translate_NonpositiveMeasuringPoint_RejectsPayload(int measuringPointId)
    {
        var alarm = ValidAlarm();
        alarm.MeasuringPointId = measuringPointId;

        Assert.ThrowsExactly<AdapterException>(() =>
            translator.Translate(alarm, [1], SuppressionWindow));
    }

    [DataRow("timestamp-nan")]
    [DataRow("timestamp-infinity")]
    [DataRow("x-value")]
    [DataRow("y-value")]
    [DataRow("z-value")]
    [DataRow("level-1")]
    [DataRow("level-2")]
    [DataRow("level-3")]
    [DataTestMethod]
    public void Translate_NonfiniteTimestampValueOrThreshold_RejectsPayload(string invalidValue)
    {
        var alarm = ValidAlarm();
        var data = alarm.Data1!;
        var alarms = data.Alarms!;
        var axes = data.Axes!;
        switch (invalidValue)
        {
            case "timestamp-nan":
                alarm.CreatedAt = double.NaN;
                break;
            case "timestamp-infinity":
                alarm.CreatedAt = double.PositiveInfinity;
                break;
            case "x-value":
                axes.X!.Vtop!.Value = double.NaN;
                break;
            case "y-value":
                axes.Y!.Vtop!.Value = double.NegativeInfinity;
                break;
            case "z-value":
                axes.Z!.Vtop!.Value = double.PositiveInfinity;
                break;
            case "level-1":
                alarms.AlarmLevel1 = double.NaN;
                break;
            case "level-2":
                alarms.AlarmLevel2 = double.PositiveInfinity;
                break;
            case "level-3":
                alarms.AlarmLevel3 = double.NegativeInfinity;
                break;
            default:
                Assert.Fail($"Unknown test case {invalidValue}.");
                break;
        }

        Assert.ThrowsExactly<AdapterException>(() =>
            translator.Translate(alarm, [1], SuppressionWindow));
    }

    [DataRow("level-1")]
    [DataRow("level-2")]
    [DataRow("level-3")]
    [DataTestMethod]
    public void Translate_NegativeThreshold_RejectsPayload(string threshold)
    {
        var alarm = ValidAlarm();
        var alarms = alarm.Data1!.Alarms!;
        switch (threshold)
        {
            case "level-1":
                alarms.AlarmLevel1 = -1;
                break;
            case "level-2":
                alarms.AlarmLevel2 = -1;
                break;
            case "level-3":
                alarms.AlarmLevel3 = -1;
                break;
        }

        Assert.ThrowsExactly<AdapterException>(() =>
            translator.Translate(alarm, [1], SuppressionWindow));
    }

    [DataRow(double.MaxValue)]
    [DataRow(-1.0e20)]
    [DataTestMethod]
    public void Translate_UnrepresentableUnixTimestamp_RejectsPayload(double timestamp)
    {
        var alarm = ValidAlarm();
        alarm.CreatedAt = timestamp;

        Assert.ThrowsExactly<AdapterException>(() =>
            translator.Translate(alarm, [1], SuppressionWindow));
    }

    [TestMethod]
    public void Translate_FractionalUnixTimestamp_PreservesVendorMillisecondPrecisionAsUtc()
    {
        var alarm = ValidAlarm();
        alarm.CreatedAt = 1702317013.999;

        var signal = translator.Translate(alarm, [1], SuppressionWindow);

        Assert.AreEqual(
            DateTimeOffset.FromUnixTimeMilliseconds(1702317013999).UtcDateTime,
            signal.EventTime);
        Assert.AreEqual(DateTimeKind.Utc, signal.EventTime.Kind);
    }

    [DataRow(12, 12, 5, "y")]
    [DataRow(12, 5, 12, "z")]
    [DataRow(5, 12, 12, "z")]
    [DataRow(12, 12, 12, "z")]
    [DataTestMethod]
    public void Translate_MaximumAxisTie_PreservesDeterministicSelection(
        double x,
        double y,
        double z,
        string expectedAxis)
    {
        var alarm = ValidAlarm();
        var axes = alarm.Data1!.Axes!;
        axes.X!.Vtop!.Value = x;
        axes.Y!.Vtop!.Value = y;
        axes.Z!.Vtop!.Value = z;

        var signal = translator.Translate(alarm, [1], SuppressionWindow);

        Assert.AreEqual($"vtop {expectedAxis}", signal.Field);
    }

    [DataRow(2, AlertType.Ignore, 0, AlertDeliveryChannels.None)]
    [DataRow(3, AlertType.Ignore, 3, AlertDeliveryChannels.None)]
    [DataRow(7, AlertType.Caution, 7, AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)]
    [DataRow(10, AlertType.Alert, 10, AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms)]
    [DataTestMethod]
    public void Translate_SeverityBoundary_SelectsExactLimitAndChannels(
        double value,
        AlertType expectedType,
        double expectedLimit,
        AlertDeliveryChannels expectedChannels)
    {
        var alarm = ValidAlarm();
        var axes = alarm.Data1!.Axes!;
        axes.X!.Vtop!.Value = value;
        axes.Y!.Vtop!.Value = value - 1;
        axes.Z!.Vtop!.Value = value - 2;

        var signal = translator.Translate(alarm, [1], SuppressionWindow);

        Assert.AreEqual(expectedType, signal.AlertType);
        Assert.AreEqual(expectedLimit, signal.Limit);
        Assert.AreEqual(expectedChannels, signal.DeliveryChannels);
    }

    [TestMethod]
    public void Translate_NonInvariantCurrentCulture_FormatsInvariantExactMessage()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var alarm = ValidAlarm();
            var axes = alarm.Data1!.Axes!;
            axes.X!.Vtop!.Value = 12.5;
            axes.Y!.Vtop!.Value = 8;
            axes.Z!.Vtop!.Value = 4;

            var signal = translator.Translate(alarm, [1], SuppressionWindow);

            Assert.AreEqual("Vibration Alert vtop x level=12.5 limit=10", signal.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static AlarmDataV2 ValidAlarm() => new()
    {
        CreatedAt = new DateTimeOffset(
            new DateTime(2024, 7, 15, 10, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds(),
        MeasuringPointId = 23423,
        Data1 = new AlarmData
        {
            Alarms = new AlarmAlarms
            {
                AlarmLevel1 = 30,
                AlarmLevel2 = 70,
                AlarmLevel3 = 100
            },
            Axes = new AlarmAxes
            {
                X = new AlarmAxis { Vtop = new AlarmVtop { Value = 12 } },
                Y = new AlarmAxis { Vtop = new AlarmVtop { Value = 8 } },
                Z = new AlarmAxis { Vtop = new AlarmVtop { Value = 4 } }
            }
        }
    };
}

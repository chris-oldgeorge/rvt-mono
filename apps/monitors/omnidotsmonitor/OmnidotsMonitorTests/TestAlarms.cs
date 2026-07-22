using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    [TestClass]
    public class TestAlarms
    {

        public TestAlarms()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestAlarms");
        }


        [TestMethod]
        public void TestParseAlarmJson()
        {
            var json = TestUtil.ReadTextFromFile("testdata/alarm_ignore.json");
            var alarm = JsonSerializer.Deserialize<AlarmData>(json);

            Assert.IsNotNull(alarm);
            Assert.AreEqual(OmnidotsProtocol.MSG_SENSOR_GUIDELINE_ALARM, alarm.Category);
            Assert.AreEqual("warning", alarm.Level);
            Assert.AreEqual(1702317013.999, alarm.CreatedAt);

            Assert.AreEqual("WOHEPU", alarm!.Data!.Sensor);
            Assert.AreEqual("WOHEPU-82022", alarm!.Data!.MeasuringPoint);
            Assert.AreEqual("", alarm!.Data!.AlarmNames!.AlarmLevel1);
            Assert.AreEqual("", alarm!.Data!.AlarmNames!.AlarmLevel2);
            Assert.AreEqual("", alarm!.Data!.AlarmNames!.Alarm_level3);
            Assert.AreEqual(1702317013.999, alarm.Data.CreatedAt);
            Assert.AreEqual(30, alarm!.Data!.Alarms!.AlarmLevel1);
            Assert.AreEqual(70, alarm!.Data!.Alarms!.AlarmLevel2);
            Assert.AreEqual(100, alarm!.Data!.Alarms!.AlarmLevel3);

            var x = alarm!.Data!.Axes!.X!;
            Assert.AreEqual(17.5, x.fdom);
            Assert.AreEqual(1.6326589584350586, x.vtop!.Value);
            Assert.AreEqual(16.326589584350586, x.vtop!.GuideLineOverflow);
            Assert.AreEqual(10, x.vtop!.GuideLineLimit);
            Assert.AreEqual(54.421965281168625, x.vtop!.AlarmLimitOverflows!.AlarmLevel1);
            Assert.AreEqual(23.323699406215123, x.vtop!.AlarmLimitOverflows!.AlarmLevel2);
            Assert.AreEqual(16.326589584350586, x.vtop!.AlarmLimitOverflows!.AlarmLevel3);
            Assert.AreEqual(3, x.vtop!.AlarmLimits!.AlarmLevel1);
            Assert.AreEqual(7, x.vtop!.AlarmLimits!.AlarmLevel2);
            Assert.AreEqual(10, x.vtop!.AlarmLimits!.AlarmLevel3);

            var y = alarm!.Data!.Axes!.Y!;
            Assert.AreEqual(17.5, y.fdom);
            Assert.AreEqual(0.7223203182220459, y.vtop!.Value);
            Assert.AreEqual(7.223203182220458, y.vtop!.GuideLineOverflow);
            Assert.AreEqual(10, y.vtop!.GuideLineLimit);
            Assert.AreEqual(24.07734394073486, y.vtop!.AlarmLimitOverflows!.AlarmLevel1);
            Assert.AreEqual(10.31886168888637, y.vtop!.AlarmLimitOverflows!.AlarmLevel2);
            Assert.AreEqual(7.223203182220458, y.vtop!.AlarmLimitOverflows!.AlarmLevel3);
            Assert.AreEqual(3, y.vtop!.AlarmLimits!.AlarmLevel1);
            Assert.AreEqual(7, y.vtop!.AlarmLimits!.AlarmLevel2);
            Assert.AreEqual(10, y.vtop!.AlarmLimits!.AlarmLevel3);

            var z = alarm!.Data!.Axes!.Z!;
            Assert.AreEqual(17.5, z.fdom);
            Assert.AreEqual(4.057920932769775, z.vtop!.Value);
            Assert.AreEqual(40.579209327697754, z.vtop!.GuideLineOverflow);
            Assert.AreEqual(10, z.vtop!.GuideLineLimit);
            Assert.AreEqual(135.26403109232587, z.vtop!.AlarmLimitOverflows!.AlarmLevel1);
            Assert.AreEqual(57.970299039568225, z.vtop!.AlarmLimitOverflows!.AlarmLevel2);
            Assert.AreEqual(40.579209327697754, z.vtop!.AlarmLimitOverflows!.AlarmLevel3);
            Assert.AreEqual(3, z.vtop!.AlarmLimits!.AlarmLevel1);
            Assert.AreEqual(7, z.vtop!.AlarmLimits!.AlarmLevel2);
            Assert.AreEqual(10, z.vtop!.AlarmLimits!.AlarmLevel3);

            Assert.AreEqual(null, alarm.Data.Category);
            Assert.AreEqual(0.20000000298023224, alarm.Data.DataSaveLevel);
            Assert.AreEqual(2, alarm.Data.MeasurementDuration);
            Assert.AreEqual(10, alarm.Data.TraceSaveLevel);
            Assert.AreEqual(3000, alarm.Data.TracePreTrigger);
            Assert.AreEqual(3000, alarm.Data.TracePostTrigger);
            Assert.AreEqual(null, alarm.Data.MeasuringType);
            Assert.AreEqual(null, alarm.Data.VibrationType);
            Assert.AreEqual(30, alarm.Data.AlarmLevel);
            Assert.AreEqual("BS7385_250Hz", alarm.Data.GuideLine);
            Assert.AreEqual(180, alarm.Data.TraceTimeLimit);
            Assert.AreEqual("unspecified", alarm.Data.BuildingLevel);
            Assert.AreEqual("On", alarm.Data.VectorRnabled);
            Assert.AreEqual("Off", alarm.Data.VdvEnabled);
            Assert.AreEqual("On", alarm.Data.VtopEnabled);
            Assert.AreEqual("Off", alarm.Data.AtopEnabled);
            Assert.AreEqual("Off", alarm.Data.NoiseSavingEnabled);
            Assert.AreEqual(23423, alarm.MeasuringPointId);

            var txt = "Alarm level 1: Your measuring point WOHEPU-82022 (WOHEPU), measured an exceedance";
            Assert.IsTrue(alarm.Text!.StartsWith(txt));


            //    var axes = alarm.Data.Axes!;

            //axes.GetTriggeringAlarmInfo(out char axis, out AlertType alertType,
            //                            out double level, out double limit);


            //Assert.AreEqual('z', axis);
            //Assert.AreEqual(AlertType.Ignore, alertType);
            //Assert.AreEqual(z.vtop!.Value, level);
            //Assert.AreEqual(z.vtop!.AlarmLimits.AlarmLevel1, limit);

        }

        [TestMethod]
        public void TestParseOnlineJson()
        {
            var json = TestUtil.ReadTextFromFile("testdata/online.json");
            var alarm = JsonSerializer.Deserialize<AlarmData>(json);

            Assert.IsNotNull(alarm);
            Assert.AreEqual(OmnidotsProtocol.MSG_SENSOR_ONLINE, alarm.Category);
            Assert.AreEqual("success", alarm.Level);
            Assert.AreEqual(null, alarm.CreatedAt);

            Assert.AreEqual("WOHEPU", alarm!.Data!.Sensor);
            Assert.AreEqual(1701854384.887, alarm!.Data!.Datetime);
            Assert.IsNull(alarm!.Data!.AlarmNames);
            Assert.IsNull(alarm!.Data!.Alarms);
            Assert.IsNull(alarm!.Data!.Axes);

            Assert.IsNull(alarm.Data.Category);
            Assert.AreEqual("Your measuring point WOHEPU-82022 (WOHEPU) went online on Dec. 6, 2023, 9:19:44 a.m. GMT.",
                alarm.Text!);
        }

        [TestMethod]
        public void TestParseOfflineJson()
        {
            var json = TestUtil.ReadTextFromFile("testdata/stop_clipping.json");
            var alarm = JsonSerializer.Deserialize<AlarmData>(json);

            Assert.IsNotNull(alarm);
            Assert.AreEqual(OmnidotsProtocol.MSG_SENSOR_OFFLINE, alarm.Category);
            Assert.AreEqual("error", alarm.Level);
            Assert.AreEqual(null, alarm.CreatedAt);

            Assert.AreEqual("WOHEPU", alarm!.Data!.Sensor);
            Assert.AreEqual(1701798511.988, alarm!.Data!.Datetime);
            Assert.IsNull(alarm!.Data!.AlarmNames);
            Assert.IsNull(alarm!.Data!.Alarms);
            Assert.IsNull(alarm!.Data!.Axes);

            Assert.IsNull(alarm.Data.Category);
            var txt = "Your measuring point WOHEPU-82022 (WOHEPU) stopped measuring on Dec. 5, 2023, 5:48:31 p.m. GMT";
            Assert.IsTrue(alarm.Text!.StartsWith(txt));
        }

        [TestMethod]
        public void AlarmDataV2_MissingNestedObjectsRemainObservableAsNull()
        {
            var missingData = JsonSerializer.Deserialize<AlarmDataV2>("{}");
            var missingAlarmAndAxes = JsonSerializer.Deserialize<AlarmDataV2>("""
                {"data":{}}
                """);
            var missingAxes = JsonSerializer.Deserialize<AlarmDataV2>("""
                {"data":{"alarms":{},"axes":{}}}
                """);
            var missingVtop = JsonSerializer.Deserialize<AlarmDataV2>("""
                {"data":{"alarms":{},"axes":{"x":{},"y":{},"z":{}}}}
                """);

            Assert.IsNotNull(missingData);
            Assert.IsNull(missingData.Data1);

            Assert.IsNotNull(missingAlarmAndAxes);
            Assert.IsNotNull(missingAlarmAndAxes.Data1);
            Assert.IsNull(missingAlarmAndAxes.Data1.Alarms);
            Assert.IsNull(missingAlarmAndAxes.Data1.Axes);

            Assert.IsNotNull(missingAxes);
            Assert.IsNotNull(missingAxes.Data1?.Axes);
            Assert.IsNull(missingAxes.Data1.Axes.X);
            Assert.IsNull(missingAxes.Data1.Axes.Y);
            Assert.IsNull(missingAxes.Data1.Axes.Z);

            Assert.IsNotNull(missingVtop);
            Assert.IsNull(missingVtop.Data1?.Axes?.X?.Vtop);
            Assert.IsNull(missingVtop.Data1?.Axes?.Y?.Vtop);
            Assert.IsNull(missingVtop.Data1?.Axes?.Z?.Vtop);
        }



        [DataRow("testdata/alarm_ignore.json", "vtop z", AlertType.Ignore, 4.057920932769775, 3)]
        [DataRow("testdata/alarm_caution.json", "vtop y", AlertType.Caution, 7.1234567890, 7)]
        [DataRow("testdata/alarm_alert.json", "vtop x", AlertType.Alert, 11.1234567890, 10)]
        [DataTestMethod]
        public void TestAlarmTriggers_OutputCorrectValues_Success(string filename,
                                                                  string expectedAxis,
                                                                  AlertType expectedAlertType,
                                                                  double expectedLevel,
                                                                  double expectedLimit)
        {
            var json = TestUtil.ReadTextFromFile(filename);
            var alarm = JsonSerializer.Deserialize<AlarmData>(json);

            Assert.IsNotNull(alarm);
            Assert.AreEqual(OmnidotsProtocol.MSG_SENSOR_GUIDELINE_ALARM, alarm.Category);
            Assert.AreEqual("warning", alarm.Level);
            Assert.AreEqual("WOHEPU", alarm!.Data!.Sensor);
            Assert.AreEqual("WOHEPU-82022", alarm!.Data!.MeasuringPoint);

            var monitorId = Guid.NewGuid();
            var notification = alarm.GetNotification(monitorId);

            Assert.AreEqual(expectedAxis, notification.AlertField);
            Assert.AreEqual(expectedAlertType, notification.AlertType);
            Assert.AreEqual(expectedLevel, notification.Level);
            Assert.AreEqual(expectedLimit, notification.LimitOn);
        }
    }
}

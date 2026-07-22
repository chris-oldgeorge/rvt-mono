using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Model.Json
{
    public class AlarmLimits
    {
        [JsonPropertyName("alarm_level_1")]
        public double AlarmLevel1 { get; set; }
        [JsonPropertyName("alarm_level_2")]
        public double AlarmLevel2 { get; set; }
        [JsonPropertyName("alarm_level_3")]
        public double AlarmLevel3 { get; set; }
    }

    public class AlarmNames
    {
        [JsonPropertyName("alarm_level_1")]
        public string? AlarmLevel1 { get; set; }
        [JsonPropertyName("alarm_level_2")]
        public string? AlarmLevel2 { get; set; }
        [JsonPropertyName("alarm_level_3")]
        public string? Alarm_level3 { get; set; }
    }

    public class Axes
    {
        [JsonPropertyName("x")]
        public AlarmFdomVtop? X { get; set; }
        [JsonPropertyName("y")]
        public AlarmFdomVtop? Y { get; set; }
        [JsonPropertyName("z")]
        public AlarmFdomVtop? Z { get; set; }
    }

    public class Data
    {
        [JsonPropertyName("sensor")]
        public string? Sensor { get; set; }
        [JsonPropertyName("measuring_point")]
        public string? MeasuringPoint { get; set; }
        [JsonPropertyName("alarm_names")]
        public AlarmNames? AlarmNames { get; set; }
        [JsonPropertyName("created_at")]
        public double CreatedAt { get; set; }
        [JsonPropertyName("alarms")]
        public AlarmLimits? Alarms { get; set; }
        [JsonPropertyName("axes")]
        public Axes? Axes { get; set; }
        [JsonPropertyName("category")]
        public string? Category { get; set; }
        [JsonPropertyName("data_save_level")]
        public double DataSaveLevel { get; set; }
        [JsonPropertyName("measurement_duration")]
        public int MeasurementDuration { get; set; }
        [JsonPropertyName("trace_save_level")]
        public double TraceSaveLevel { get; set; }
        [JsonPropertyName("trace_pre_trigger")]
        public double TracePreTrigger { get; set; }
        [JsonPropertyName("trace_post_trigger")]
        public double TracePostTrigger { get; set; }
        [JsonPropertyName("measuring_type")]
        public string? MeasuringType { get; set; }
        [JsonPropertyName("vibration_type")]
        public string? VibrationType { get; set; }
        [JsonPropertyName("alarm_level")]
        public double AlarmLevel { get; set; }
        [JsonPropertyName("guide_line")]
        public string? GuideLine { get; set; }
        [JsonPropertyName("trace_time_limit")]
        public int TraceTimeLimit { get; set; }
        [JsonPropertyName("building_level")]
        public string? BuildingLevel { get; set; }
        [JsonPropertyName("vector_enabled")]
        public string? VectorRnabled { get; set; }
        [JsonPropertyName("vdv_enabled")]
        public string? VdvEnabled { get; set; }
        [JsonPropertyName("atop_enabled")]
        public string? AtopEnabled { get; set; }
        [JsonPropertyName("vtop_enabled")]
        public string? VtopEnabled { get; set; }
        [JsonPropertyName("noise_saving_enabled")]
        public string? NoiseSavingEnabled { get; set; }
        [JsonPropertyName("datetime")]
        public double Datetime { get; set; }
    }

    public class AlarmData
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }
        [JsonPropertyName("level")]
        public string? Level { get; set; }
        [JsonPropertyName("created_at")]
        public double? CreatedAt { get; set; }
        [JsonPropertyName("data")]
        public Data? Data { get; set; }
        [JsonPropertyName("created_for")]
        public string? CreatedFor { get; set; }
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        [JsonPropertyName("measuring_point_id")]
        public int MeasuringPointId { get; set; }

        public NotificationDto GetNotification(Guid monitorId)
        {

            var vtop = GetAxesWithMaxValue(out string axis);
            var millis = CreatedAt! * 1000;
            var createdTime = DateTimeUtil.FromMillis((long)millis);
            var fieldStr = "vtop {0}";

            vtop.GetAlertTypeValueAndLimit(alertType: out AlertType alertType,
                                      level: out double level,
                                      limit: out double limit);

            RvtLogger.Logger.LogInformation("Creating notification alertType={Value1} level={Value2} limit={Value3}",
                                             alertType, level, limit);

            var notification = new NotificationDto(id: Guid.NewGuid(),
                 notificationTime: createdTime,
                 limitOn: limit,
                 averagingPeriod: Data!.MeasurementDuration,
                 level: level,
                 closedTime: null,
                 closedByUser: null,
                 alertType: alertType,
                 alertField: string.Format(fieldStr, axis),
                 monitorId: monitorId);

            notification.ApiMessage = Text;

            return notification;
        }

        private AlarmFdomVtop GetAxesWithMaxValue(out string axis)
        {
            var axes = Data!.Axes!;
            var x = axes.X!.vtop!.Value;
            var y = axes.Y!.vtop!.Value;
            var z = axes.Z!.vtop!.Value;

            if (x > y && x > z)
            {
                axis = "x";
                return axes.X;
            }

            if (y > z)
            {
                axis = "y";
                return axes.Y;
            }
            axis = "z";
            return axes.Z;
        }
    }

    public class Vtop
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }
        [JsonPropertyName("guide_line_overflow")]
        public double GuideLineOverflow { get; set; }
        [JsonPropertyName("guide_line_limit")]
        public double GuideLineLimit { get; set; }
        [JsonPropertyName("alarm_limit_overflows")]
        public AlarmLimits? AlarmLimitOverflows { get; set; }
        [JsonPropertyName("alarm_limits")]
        public AlarmLimits? AlarmLimits { get; set; }
    }

    public class AlarmFdomVtop
    {
        [JsonPropertyName("fdom")] public double fdom { get; set; }
        [JsonPropertyName("vtop")] public Vtop? vtop { get; set; }


        internal void GetAlertTypeValueAndLimit(out AlertType alertType, out double level, out double limit)
        {
            level = vtop!.Value;

            var alarmLimits = vtop.AlarmLimits!;

            if (level >= alarmLimits.AlarmLevel3)
            {
                alertType = AlertType.Alert;
                limit = alarmLimits.AlarmLevel3;
            }
            else if (level >= alarmLimits.AlarmLevel2)
            {
                alertType = AlertType.Caution;
                limit = alarmLimits.AlarmLevel2;
            }
            else if (level >= alarmLimits.AlarmLevel1)
            {
                alertType = AlertType.Ignore;
                limit = alarmLimits.AlarmLevel1;
            }
            else
            {
                throw AdapterException.Of("Alarm Value did not exceed limits !");
            }
        }
    }
}

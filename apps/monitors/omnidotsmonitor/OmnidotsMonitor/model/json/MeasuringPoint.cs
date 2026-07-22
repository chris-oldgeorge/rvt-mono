using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class MeasuringPoint
    {
        [JsonRequired]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonRequired]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonRequired]
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_led")]
        public bool DisableLed { get; set; }

        [JsonRequired]
        [JsonPropertyName("log_flush_interval")]
        public int LogFlushInterval { get; set; }

        [JsonRequired]
        [JsonPropertyName("timezone")]
        public string? TimeZone { get; set; }

        [JsonRequired]
        [JsonPropertyName("vtop_enabled")]
        public string? VtopEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("atop_enabled")]
        public string? AtopEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("vector_enabled")]
        public string? VectorEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("guide_line")]
        public string? GuideLine { get; set; }

        [JsonRequired]
        [JsonPropertyName("building_level")]
        public string? BuildingLevel { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonRequired]
        [JsonPropertyName("measurement_duration")]
        public int MeasurementDuration { get; set; }

        [JsonRequired]
        [JsonPropertyName("data_save_level")]
        public double DataSaveLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("noise_saving_enabled")]
        public string? NoiseSavingEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("vdv_enabled")]
        public string? VdvEnabled { get; set; }

        [JsonPropertyName("vdv_x")]
        public string? VdvX { get; set; }

        [JsonPropertyName("vdv_y")]
        public string? VdvY { get; set; }

        [JsonPropertyName("vdv_z")]
        public string? VdvZ { get; set; }

        [JsonPropertyName("vdv_period")]
        public int VdvPeriod { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_save_level")]
        public double TraceSaveLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_pre_trigger")]
        public double TracePreTrigger { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_post_trigger")]
        public double TracePostTrigger { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_1")]
        public string? ScheduleEnable1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_1")]
        public string? ScheduleDisable1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_2")]
        public string? ScheduleEnable2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_2")]
        public string? ScheduleDisable2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_3")]
        public string? ScheduleEnable3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_3")]
        public string? ScheduleDisable3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_4")]
        public string? ScheduleEnable4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_4")]
        public string? ScheduleDisable4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_5")]
        public string? ScheduleEnable5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_5")]
        public string? ScheduleDisable5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_6")]
        public string? ScheduleEnable6 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_6")]
        public string? ScheduleDisable6 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_enable_0")]
        public string? ScheduleEnable0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("schedule_disable_0")]
        public string? ScheduleDisable0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_value")]
        public double AlarmValue { get; set; }

        [JsonPropertyName("sensor")]
        public Sensor? Sensor { get; set; }

        [JsonPropertyName("flat_level")]
        public double? FlatLevel { get; set; }

        [JsonPropertyName("noise_measurement_duration")]
        public int? NoiseMeasurementDuration { get; set; }
    }
}


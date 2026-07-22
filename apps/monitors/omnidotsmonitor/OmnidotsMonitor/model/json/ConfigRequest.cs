using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class WebhookRecipient
    {
        [JsonRequired]
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonRequired]
        [JsonPropertyName("secret")]
        public string? Secret { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_level_1")]
        public bool AlarmLevel1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_level_2")]
        public bool AlarmLevel2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_level_3")]
        public bool AlarmLevel3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("measuring_point_administrator")]
        public bool MeasuringPointAdministrator { get; set; }

    }


    public class ConfigRequest
    {

        // what should we make required in here ?
        [JsonRequired]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonRequired]
        [JsonPropertyName("sensor_name")]
        public string? SensorName { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_led")]
        public bool DisableLed { get; set; }

        [JsonRequired]
        [JsonPropertyName("log_flush_interval")]
        public int LogFlushInterval { get; set; }

        [JsonRequired]
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonRequired]
        [JsonPropertyName("guide_line")]
        public string? GuideLine { get; set; }

        [JsonRequired]
        [JsonPropertyName("building_level")]
        public string? BuildingLevel { get; set; }

        [JsonPropertyName("flat_level")]
        public double FlatLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_recipients")]
        public List<List<string>>? AlarmRecipients { get; set; }

        [JsonRequired]
        [JsonPropertyName("measuring_type")]
        public string? MeasuringType { get; set; }

        [JsonRequired]
        [JsonPropertyName("vibration_type")]
        public string? VibrationType { get; set; }

        [JsonRequired]
        [JsonPropertyName("data_save_level")]
        public double DataSaveLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("measurement_duration")]
        public double MeasurementDuration { get; set; }

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
        [JsonPropertyName("alarm_level_1")]
        public int AlarmLevel1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_level_2")]
        public int AlarmLevel2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("alarm_level_3")]
        public int AlarmLevel3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("email_delay")]
        public int EmailDelay { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_0")]
        public string? EnableTime0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_0")]
        public string? DisableTime0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_1")]
        public string? EnableTime1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_1")]
        public string? DisableTime1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_2")]
        public string? EnableTime2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_2")]
        public string? DisableTime2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_3")]
        public string? EnableTime3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_3")]
        public string? DisableTime3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_4")]
        public string? EnableTime4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_4")]
        public string? DisableTime4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_5")]
        public string? EnableTime5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_5")]
        public string? DisableTime5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("enable_time_6")]
        public string? EnableTime6 { get; set; }

        [JsonRequired]
        [JsonPropertyName("disable_time_6")]
        public string? DisableTime6 { get; set; }

        [JsonRequired]
        [JsonPropertyName("webhook_recipient")]
        public WebhookRecipient? WebhookRecipient { get; set; }
    }
}

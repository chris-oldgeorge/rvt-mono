using System.Text.Json.Serialization;

namespace Omnidots.Model.Json;

public class AlarmDataV2
{
    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }

    [JsonPropertyName("data")]
    public Data? Data1 { get; set; }

    [JsonPropertyName("measuring_point_id")]
    public int MeasuringPointId { get; set; }

    public class Data
    {
        [JsonPropertyName("alarms")]
        public Alarms? Alarms { get; set; }

        [JsonPropertyName("axes")]
        public Axes? Axes { get; set; }

        [JsonPropertyName("created_at")]
        public double CreatedAt { get; set; }

        [JsonPropertyName("measuring_point")]
        public string MeasuringPoint { get; set; } = string.Empty;

        [JsonPropertyName("pvs")]
        public Pvs? Pvs { get; set; }

        [JsonPropertyName("sensor")]
        public string Sensor { get; set; } = string.Empty;
    }

    public class Alarms
    {
        [JsonPropertyName("alarm_level_1")]
        public double AlarmLevel1 { get; set; }

        [JsonPropertyName("alarm_level_2")]
        public double AlarmLevel2 { get; set; }

        [JsonPropertyName("alarm_level_3")]
        public double AlarmLevel3 { get; set; }
    }

    public class Axes
    {
        [JsonPropertyName("x")]
        public Axis? X { get; set; }

        [JsonPropertyName("y")]
        public Axis? Y { get; set; }

        [JsonPropertyName("z")]
        public Axis? Z { get; set; }
    }

    public class Axis
    {
        [JsonPropertyName("fdom")]
        public double Fdom { get; set; }

        [JsonPropertyName("vtop")]
        public Vtop? Vtop { get; set; }
    }

    public class Vtop
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    public class Pvs
    {
        [JsonPropertyName("fdom")]
        public double Fdom { get; set; }

        [JsonPropertyName("vtop")]
        public Vtop? Vtop { get; set; }
    }
}

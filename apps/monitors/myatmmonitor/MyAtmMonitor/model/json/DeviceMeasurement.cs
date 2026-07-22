using System.Text.Json.Serialization;

namespace MyAtm.Model.Json
{

    public class DeviceMeasurement : BaseDeviceMeasurement
    {
        [JsonRequired]
        [JsonPropertyName("pm1")]
        public Double? Pm1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm2_5")]
        public Double? Pm2_5 { get; set; }


        [JsonRequired]
        [JsonPropertyName("pm10")]
        public Double? Pm10 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_total")]
        public Double? PmTotal { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_t")]
        public Double? Weather_t { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_p")]
        public Double? Weather_p { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_rh")]
        public Double? Weather_rh { get; set; }

    }

}

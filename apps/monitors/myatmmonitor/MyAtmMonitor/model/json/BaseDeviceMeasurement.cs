using System.Text.Json.Serialization;

namespace MyAtm.Model.Json
{

    public class BaseDeviceMeasurement
    {

        [JsonRequired]
        [JsonPropertyName("avrg")]
        public int Avrg { get; set; }

        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

    }

}

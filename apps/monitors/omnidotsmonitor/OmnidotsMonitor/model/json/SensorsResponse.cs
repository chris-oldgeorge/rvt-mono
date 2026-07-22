using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    public class SensorsResponse : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("sensors")]
        public List<Sensor>? Sensors { get; set; }
    }
}

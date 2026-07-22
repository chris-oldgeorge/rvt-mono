using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class VeffRecords : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("samples")]
        public List<VeffSample>? Samples { get; set; }
    }

    public class VeffSample
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }
    }
}

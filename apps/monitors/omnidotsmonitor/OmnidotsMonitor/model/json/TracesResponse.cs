using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class TracesReponse : OmnidotsResponse
    {

        [JsonRequired]
        [JsonPropertyName("traces")]
        public List<TraceData>? Traces { get; set; }
    }

    public class TraceData
    {
        [JsonRequired]
        [JsonPropertyName("start_time")]
        public long StartTime { get; set; }

        [JsonRequired]
        [JsonPropertyName("end_time")]
        public long EndTime { get; set; }

        [JsonPropertyName("x")]
        public List<double>? X { get; set; }

        [JsonPropertyName("y")]
        public List<double>? Y { get; set; }

        [JsonPropertyName("z")]
        public List<double>? Z { get; set; }

    }

}

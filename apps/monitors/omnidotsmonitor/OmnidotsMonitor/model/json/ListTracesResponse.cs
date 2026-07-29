using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class TracesListResponse : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("traces")]
        public List<TraceInfo>? Traces { get; set; }
    }

    public class TraceInfo
    {
        [JsonRequired]
        [JsonPropertyName("start_time")]
        public long StartTime { get; set; }

        [JsonRequired]
        [JsonPropertyName("end_time")]
        public long EndTime { get; set; }
    }
}

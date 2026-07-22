using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class MeasuringPointsResponse : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("measuring_points")]
        public List<MeasuringPoint>? MeasuringPoints { get; set; }

    }
}

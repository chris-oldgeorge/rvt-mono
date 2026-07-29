using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    public class VdvRecords : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("samples")]
        public List<VdvSample>? Samples { get; set; }
    }

    public class VdvSample : VeffSample
    {

        [JsonRequired]
        [JsonPropertyName("vdv_period")]
        public int Timezone { get; set; }

        [JsonRequired]
        [JsonPropertyName("vdv_x")]
        public string? VdvX { get; set; }

        [JsonRequired]
        [JsonPropertyName("vdv_y")]
        public string? VdvY { get; set; }

        [JsonRequired]
        [JsonPropertyName("vdv_z")]
        public string? VdvZ { get; set; }
    }
}

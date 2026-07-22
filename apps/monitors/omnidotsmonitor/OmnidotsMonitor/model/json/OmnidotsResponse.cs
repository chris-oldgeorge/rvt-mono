
using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    public class OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    public class TokenResponse : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("token")]
        public string? Token { get; set; }

    }
}

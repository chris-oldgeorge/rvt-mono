using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    // Summary: JSON DTOs for Omnidots API error responses.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class ErrorResponses : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("details")]
        public Dictionary<string, string>? Details { get; set; }

    }

    public class ErrorResponse : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("details")]
        public Dictionary<string, List<string>> Details { get; set; } = new();

        [JsonPropertyName("help")]
        public string? Help { get; set; }
    }
}

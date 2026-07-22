using System.Text.Json.Serialization;

namespace AirQ.Model.Http
{
    public class InstrumentResponse
    {
        [JsonRequired]
        [JsonPropertyName("instrumentID")]
        public string? InstrumentID { get; set; }

        [JsonRequired]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonRequired]
        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonRequired]
        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }

        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("timeZone")]
        public string? TimeZone { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

    }
}

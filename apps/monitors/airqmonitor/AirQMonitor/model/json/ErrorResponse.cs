using System.Text.Json.Serialization;

namespace AirQ.Model.Http
{

    public class ErrorResponse
    {
        [JsonRequired]
        public string? Response { get; set; }
    }
}

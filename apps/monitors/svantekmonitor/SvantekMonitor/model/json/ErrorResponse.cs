using System.Text.Json.Serialization;

namespace Svantek.Model.Http
{

    public class ErrorResponse
    {
        [JsonRequired]
        public string? Response { get; set; }
    }

}

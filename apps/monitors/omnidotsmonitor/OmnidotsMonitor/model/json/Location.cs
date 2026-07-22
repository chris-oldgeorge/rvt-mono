using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class Location
    {
        [JsonRequired]
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

}

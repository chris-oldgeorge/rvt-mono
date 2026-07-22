using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{
    public class Sensor
    {
        [JsonRequired]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonRequired]
        [JsonPropertyName("lastseen")]
        public DateTime? Lastseen { get; set; }

        [JsonRequired]
        [JsonPropertyName("battery_charge")]
        public int? BatteryCharge { get; set; }

        [JsonRequired]
        [JsonPropertyName("connected_using")]
        public string? ConnectedUsing { get; set; }

        [JsonRequired]
        [JsonPropertyName("wifi_password")]
        public string? WifiPassword { get; set; }

        [JsonRequired]
        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonRequired]
        [JsonPropertyName("location")]
        public Location? Location { get; set; }
    }
}

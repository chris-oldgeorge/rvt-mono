using System.Text.Json.Serialization;

namespace Svantek.Model.Http
{
    // Summary: JSON DTOs for Svantek station status responses.
    // Major updates:
    // - 2026-06-18: Added safe defaults for deserialization-backed DTOs to reduce nullability warnings.
    public class StationsResponse
    {
        public string status { get; set; } = string.Empty;
        public List<Station> stations { get; set; } = new();
    }

    public class Station
    {
        public int serial { get; set; }
        public string type { get; set; } = string.Empty;

        [JsonPropertyName("user-text")]
        public object usertext { get; set; } = new();
        public bool active { get; set; }

        [JsonPropertyName("last-login")]
        public string lastlogin { get; set; } = string.Empty;

        [JsonPropertyName("last-logout")]
        public string lastlogout { get; set; } = string.Empty;

        [JsonPropertyName("is-online")]
        public bool isonline { get; set; }

        [JsonPropertyName("meter-firmware")]
        public double meterfirmware { get; set; }

        [JsonPropertyName("last-status-timestamp")]
        public string laststatustimestamp { get; set; } = string.Empty;

        [JsonPropertyName("battery-charge")]
        public int batterycharge { get; set; }

        [JsonPropertyName("battery-time-to-empty")]
        public int batterytimetoempty { get; set; }

        [JsonPropertyName("power-source")]
        public string powersource { get; set; } = string.Empty;

        [JsonPropertyName("is-battery-charging")]
        public bool isbatterycharging { get; set; }

        [JsonPropertyName("gsm-signal-quality")]
        public int gsmsignalquality { get; set; }

        [JsonPropertyName("memory-free")]
        public object memoryfree { get; set; } = new();

        [JsonPropertyName("memory-free-percent")]
        public int memoryfreepercent { get; set; }

        [JsonPropertyName("warning-list")]
        public List<string> warninglist { get; set; } = new();

        [JsonPropertyName("warning-level")]
        public int warninglevel { get; set; }

        [JsonPropertyName("measurement-state")]
        public string measurementstate { get; set; } = string.Empty;

        [JsonPropertyName("gps-latitude")]
        public string gpslatitude { get; set; } = string.Empty;

        [JsonPropertyName("gps-longitude")]
        public string gpslongitude { get; set; } = string.Empty;

        [JsonPropertyName("battery-time-to-full")]
        public int? batterytimetofull { get; set; }
    }
}

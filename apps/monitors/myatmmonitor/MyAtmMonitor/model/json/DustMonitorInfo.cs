// File summary: MyAtmosphere device-info JSON contracts used when building MyAtm monitor DTOs.
// Major updates:
// - 2026-06-18: Accepted both current infos_firmware_version and legacy firmware_version fields so tests and older API payloads deserialize consistently.
using System.Text.Json.Serialization;

namespace MyAtm.Model.Json.DeviceInfo
{
    //  All devices from https://api.my-atmosphere.cloud/api are produced by Palas GmbH.

    // Device configuration payload. Firmware naming has varied between MyAtmosphere responses, so both names feed the same DTO-facing value.
    public class Configuration
    {
        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonRequired]
        [JsonPropertyName("saved_at")]
        public DateTime SavedAt { get; set; }

        [JsonPropertyName("infos_firmware_version")]
        public string? FirmwareVersion { get; set; }

        [JsonPropertyName("firmware_version")]
        public string? LegacyFirmwareVersion
        {
            get => FirmwareVersion;
            set => FirmwareVersion ??= value;
        }
    }

    public class CurrentLocation
    {
        [JsonRequired]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonRequired]
        [JsonPropertyName("customerId")]
        public int CustomerId { get; set; }

        [JsonRequired]
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonRequired]
        [JsonPropertyName("timeZone")]
        public string? TimeZone { get; set; }

        [JsonRequired]
        [JsonPropertyName("effectiveSince")]
        public DateTime EffectiveSince { get; set; }

        [JsonPropertyName("effectiveTill")]
        public DateTime? EffectiveTill { get; set; }

    }

    public class Customer
    {
        public int id { get; set; }
        public string? publicId { get; set; }
        public string? displayName { get; set; }
        public int? parentId { get; set; }
        public bool archived { get; set; }
    }

    public class CustomerAssignment
    {
        public int id { get; set; }
        public Customer? customer { get; set; }
        public DateTime effectiveSince { get; set; }
        public DateTime? effectiveTill { get; set; }
    }

    public class Location
    {
        public int id { get; set; }
        public string? deviceSerialNumber { get; set; }
        public int customerId { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string? address { get; set; }
        public string? timeZone { get; set; }
        public DateTime effectiveSince { get; set; }
        public DateTime? effectiveTill { get; set; }
        public bool current { get; set; }
    }

    public class DustMonitorInfo
    {
        [JsonRequired]
        [JsonPropertyName("configuration")]
        public Configuration? Configuration { get; set; }

        [JsonRequired]
        [JsonPropertyName("currentLocation")]
        public CurrentLocation? CurrentLocation { get; set; }

        [JsonRequired]
        [JsonPropertyName("currentCustomerAssignment")]
        public CustomerAssignment? CurrentCustomerAssignment { get; set; }

        [JsonRequired]
        [JsonPropertyName("locations")]
        public List<Location>? Locations { get; set; }

        [JsonRequired]
        [JsonPropertyName("customerAssignments")]
        public List<CustomerAssignment>? CustomerAssignments { get; set; }

        [JsonRequired]
        [JsonPropertyName("supportedMeasurementTypes")]
        public List<string>? SupportedMeasurementTypes { get; set; }

        [JsonRequired]
        [JsonPropertyName("serialNumber")]
        public string? SerialNumber { get; set; }

        [JsonRequired]
        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}

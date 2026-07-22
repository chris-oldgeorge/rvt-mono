using System.Text.Json.Serialization;

namespace MyAtm.Model.Json.Customer
{

    public class CurrentCustomerAssignment
    {
        [JsonRequired]
        [JsonPropertyName("customerDisplayName")]
        public string? CustomerDisplayName { get; set; }

        [JsonRequired]
        [JsonPropertyName("customerId")]
        public int CustomerId { get; set; }

        [JsonRequired]
        [JsonPropertyName("effectiveSince")]
        public DateTime EffectiveSince { get; set; }
    }


    public class CurrentLocation
    {
        [JsonRequired]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("deviceSerialNumber")]
        public string? DeviceSerialNumber { get; set; }

        [JsonPropertyName("customerId")]
        public int? CustomerId { get; set; }

        [JsonRequired]
        [JsonPropertyName("latitude")]
        public float Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public float Longitude { get; set; }

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

        [JsonPropertyName("current")]
        public bool? Current { get; set; }
    }

    public class DustMonitor
    {
        [JsonRequired]
        [JsonPropertyName("serialNumber")]
        public string? SerialNumber { get; set; }

        [JsonRequired]
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonRequired]
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonRequired]
        [JsonPropertyName("sharedPublicly")]
        public bool SharedPublicly { get; set; }

        [JsonRequired]
        [JsonPropertyName("includeInMonitoring")]
        public bool IncludeInMonitoring { get; set; }

        [JsonRequired]
        [JsonPropertyName("currentLocation")]
        public CurrentLocation? CurrentLocation { get; set; }

        [JsonRequired]
        [JsonPropertyName("currentCustomerAssignment")]
        public CurrentCustomerAssignment? CurrentCustomerAssignment { get; set; }
    }
}

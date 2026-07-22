using System.Globalization;
using System.Text.Json.Serialization;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Svantek.Model.Http
{
    // Summary: Models Svantek sample payloads and converts named numeric fields using API-stable parsing.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: parse API numeric strings with invariant culture so database tests are locale independent.
    public class SampleData
    {
        [JsonRequired]
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonRequired]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonRequired]
        [JsonPropertyName("value")]
        public string? Value { get; set; }

    }

    public class SampleResponse
    {
        [JsonRequired]
        [JsonPropertyName("utc")]
        public DateTime? Utc { get; set; }

        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonRequired]
        [JsonPropertyName("instrumentID")]
        public string? InstrumentID { get; set; }

        [JsonRequired]
        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonRequired]
        [JsonPropertyName("gpsCoordinates")]
        public string? GpsCoordinates { get; set; }

        [JsonRequired]
        [JsonPropertyName("data")]
        public List<SampleData>? Data { get; set; }

        // Summary: Returns the requested metric value as a culture-independent double.
        public double GetFieldValue(string name)
        {
            foreach (var data in Data!)
            {
                if (name.Equals(data.Name))
                {
                    try
                    {
                        return double.Parse(data.Value!, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw AdapterException.Of("Failed ! " + name + " was not a number", e);
                    }
                }
            }
            return (double)0;
        }

    }
}

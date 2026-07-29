using System.Text.Json.Serialization;

namespace AirQ.Model.Http
{
    public class MetaDataResponse
    {
        [JsonRequired]
        [JsonPropertyName("lastUpdated")]
        public DateTime? LastUpdated { get; set; }

        [JsonRequired]
        [JsonPropertyName("calibrationDate")]
        public DateTime? CalibrationDate { get; set; }

        [JsonRequired]
        [JsonPropertyName("filterChangeDate")]
        public DateTime? FilterChangeDate { get; set; }

        [JsonRequired]
        [JsonPropertyName("massCollectedOnFilter")]
        public string? MassCollectedOnFilter { get; set; }

        [JsonRequired]
        [JsonPropertyName("filterUsage")]
        public string? FilterUsage { get; set; }

        [JsonRequired]
        [JsonPropertyName("photometerGain")]
        public string? PhotometerGain { get; set; }

        [JsonRequired]
        [JsonPropertyName("pumpHours")]
        public string? PumpHours { get; set; }

        [JsonRequired]
        [JsonPropertyName("laser")]
        public string? Laser { get; set; }

        [JsonRequired]
        [JsonPropertyName("light")]
        public string? Light { get; set; }

        [JsonRequired]
        [JsonPropertyName("flowIndex")]
        public string? FlowIndex { get; set; }

        [JsonRequired]
        [JsonPropertyName("setPoint")]
        public string? SetPoint { get; set; }

        [JsonRequired]
        [JsonPropertyName("batteryVoltage")]
        public string? BatteryVoltage { get; set; }

        [JsonRequired]
        [JsonPropertyName("caseTemperature")]
        public string? CaseTemperature { get; set; }

        [JsonRequired]
        [JsonPropertyName("memoryUsage")]
        public string? MemoryUsage { get; set; }

        [JsonRequired]
        [JsonPropertyName("inletHeating")]
        public string? InletHeating { get; set; }

        [JsonRequired]
        [JsonPropertyName("instrumentID")]
        public string? InstrumentID { get; set; }
    }
}

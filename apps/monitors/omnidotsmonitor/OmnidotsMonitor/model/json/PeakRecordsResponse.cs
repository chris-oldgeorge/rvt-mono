using System.Text.Json.Serialization;

namespace Omnidots.Model.Json
{

    public class PeakRecords : OmnidotsResponse
    {
        [JsonRequired]
        [JsonPropertyName("samples")]
        public List<PeakSample>? Samples { get; set; }
    }

    public class PeakSample
    {
        [JsonPropertyName("x")]
        public FDomVtopOverflow? X { get; set; }

        [JsonPropertyName("y")]
        public FDomVtopOverflow? Y { get; set; }

        [JsonPropertyName("z")]
        public FDomVtopOverflow? Z { get; set; }

        [JsonRequired]
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonRequired]
        [JsonPropertyName("data_save_level")]
        public double DataSaveLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("measurement_duration")]
        public int MeasurementDuration { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_save_level")]
        public double TraceSaveLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_pre_trigger")]
        public double TracePreTrigger { get; set; }

        [JsonRequired]
        [JsonPropertyName("trace_post_trigger")]
        public double TracePostTrigger { get; set; }

        [JsonRequired]
        [JsonPropertyName("guide_line")]
        public string? GuideLine { get; set; }

        [JsonRequired]
        [JsonPropertyName("building_level")]
        public string? BuildingLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("vector_enabled")]
        public string? VectorEnabled { get; set; }

        [JsonPropertyName("vdv_period")]
        public int VdvPeriod { get; set; }

        [JsonPropertyName("vdv_x")]
        public string? VdvX { get; set; }

        [JsonPropertyName("vdv_y")]
        public string? VdvY { get; set; }

        [JsonPropertyName("vdv_z")]
        public string? VdvZ { get; set; }

        [JsonRequired]
        [JsonPropertyName("vdv_enabled")]
        public string? VdvEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("atop_enabled")]
        public string? AtopEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("vtop_enabled")]
        public string? VtopEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("noise_saving_enabled")]
        public string? NoiseSavingEnabled { get; set; }

        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

    }

    public class FDomVtopOverflow
    {
        public FDomVtopOverflow(double fdom, double vtop, double vtopOverflow)
        {
            Fdom = fdom;
            Vtop = vtop;
            VtopOverflow = vtopOverflow;
        }

        public double Fdom { get; set; }
        public double Vtop { get; set; }

        [JsonPropertyName("vtopOverflow")]
        public double VtopOverflow { get; set; }
    }
}

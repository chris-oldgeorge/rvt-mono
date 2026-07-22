using System.Reflection;
using System.Text;
using Svantek.Model.Http;
using SvantekMonitor.model.dto;

namespace Svantek.Model.Dto
{

    // Summary: Represents one Svantek noise sample ready for monitor database ingestion.
    // Major updates:
    // - 2026-06-18 Warning remediation: restored SampleResponse conversion constructor used by tests and ingestion utilities.
    // - 2026-06-18 Warning remediation: added default-safe string initialisation for nullable analysis.
    public class NoiseDto : DtoBase
    {
        public string SerialId { get; set; } = string.Empty;
        public DateTime SampleTime { get; set; }
        public double LAeq { get; set; }
        public double LAmax { get; set; }
        public double LA90 { get; set; }
        public double LA10 { get; set; }
        public double LCeq { get; set; }
        public double LCmax { get; set; }
        public double LC90 { get; set; }
        public double LC10 { get; set; }

        public NoiseDto()
        {
        }

        public NoiseDto(
            DateTime sampleTime,
            double lAeq,
            double lAmax,
            double lA90,
            double lA10,
            double lCeq,
            double lCmax,
            double lC90,
            double lC10)
        {
            SerialId = string.Empty;
            SampleTime = sampleTime;
            LAeq = lAeq;
            LAmax = lAmax;
            LA90 = lA90;
            LA10 = lA10;
            LCeq = lCeq;
            LCmax = lCmax;
            LC90 = lC90;
            LC10 = lC10;
        }

        // Summary: Converts a raw Svantek API sample into the canonical noise DTO fields.
        public NoiseDto(SampleResponse sample)
        {
            ArgumentNullException.ThrowIfNull(sample);

            SerialId = sample.InstrumentID ?? string.Empty;
            SampleTime = sample.Timestamp ?? DateTime.MinValue;
            LAeq = sample.GetFieldValue("LAeq(T)");
            LAmax = sample.GetFieldValue("LAmax(T)");
            LA90 = sample.GetFieldValue("LA90(T)");
            LA10 = sample.GetFieldValue("LA10(T)");
            LCeq = sample.GetFieldValue("LCeq(T)");
            LCmax = sample.GetFieldValue("LCmax(T)");
            LC90 = sample.GetFieldValue("LC90(T)");
            LC10 = sample.GetFieldValue("LC10(T)");
        }
    }
}

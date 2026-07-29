using System.Text;
using AirQ.Common;
using AirQ.Model.Http;

namespace AirQ.Model.Dto
{

    public class NoiseDto
    {
        public DateTime SampleTime { get; set; }
        public double LAeq { get; set; }
        public double LAmax { get; set; }
        public double LA90 { get; set; }
        public double LA10 { get; set; }
        public double LCeq { get; set; }
        public double LCmax { get; set; }
        public double LC90 { get; set; }
        public double LC10 { get; set; }


        public NoiseDto(SampleResponse sample)
        {
            SampleTime = DateTimeUtil.ToUtc((DateTime)sample.Utc!);
            LAeq = sample.GetFieldValue("LAeq(T)");
            LAmax = sample.GetFieldValue("LAmax(T)");
            LA90 = sample.GetFieldValue("LA90(T)");
            LA10 = sample.GetFieldValue("LA10(T)");
            LCeq = sample.GetFieldValue("LCeq(T)");
            LCmax = sample.GetFieldValue("LCmax(T)");
            LC90 = sample.GetFieldValue("LC90(T)");
            LC10 = sample.GetFieldValue("LC10(T)");
        }

        public NoiseDto(DateTime sampleTime, double lAeq, double lAmax, double lA90,
                        double lA10, double lCeq, double lCmax, double lC90, double lC10)
        {
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


        public override string ToString()
        {
            return new StringBuilder("Noise Dto").
                Append(" SampleTime=").Append(SampleTime).
                Append(" LAeq=").Append(LAeq).
                Append(" LAmax=").Append(LAmax).
                Append(" LA90=").Append(LAeq).
                Append(" LA10=").Append(LA10).
                Append(" LCeq=").Append(LCeq).
                Append(" LCmax=").Append(LCmax).
                Append(" LC90=").Append(LCeq).
                Append(" LC10=").Append(LC10).ToString();
        }


    }
}

using MyAtm.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtm.Model.Dto
{
    public record DustDto
    {
        public string SerialId { get; }
        public int Avrg { get; }

        public DateTime SampleTime { get; }
        public Double? Pm1 { get; }
        public Double? Pm2_5 { get; }
        public Double? Pm10 { get; }
        public Double? PmTotal { get; }
        public Double? Weather_t { get; }
        public Double? Weather_p { get; }
        public Double? Weather_rh { get; }

        public DustDto(string serialId, int avrg, DateTime sampleTime, Double? pm1, Double? pm2_5, Double? pm10, Double? pmTotal,
                       Double? weather_t, Double? weather_p, Double? weather_rh)
        {
            SerialId = serialId;
            Avrg = avrg;
            SampleTime = sampleTime;
            Pm1 = pm1;
            Pm2_5 = pm2_5;
            Pm10 = pm10;
            PmTotal = pmTotal;
            Weather_t = weather_t;
            Weather_p = weather_p;
            Weather_rh = weather_rh;
        }

        public DustDto(string serialNumber, BaseDeviceMeasurement measurement)
        {

            SerialId = serialNumber;
            Avrg = measurement.Avrg;
            SampleTime = measurement.Timestamp;
            if (measurement is DeviceMeasurement)
            {
                var m = (DeviceMeasurement)measurement;
                Pm1 = m.Pm1;
                Pm2_5 = m.Pm2_5;
                Pm10 = m.Pm10;
                PmTotal = m.PmTotal;
                Weather_t = m.Weather_t;
                Weather_p = m.Weather_p;
                Weather_rh = m.Weather_rh;


            }
            else if (measurement is AvgDeviceMeasurement)
            {

                var m = (AvgDeviceMeasurement)measurement;
                Pm1 = m.Pm1?.Avg;
                Pm2_5 = m.Pm2_5?.Avg;
                Pm10 = m.Pm10?.Avg;
                PmTotal = m.PmTotal?.Avg;
                Weather_t = m.Weather_t?.Avg;
                Weather_p = m.Weather_p?.Avg;
                Weather_rh = m.Weather_rh?.Avg;
            }
            else
            {
                throw AdapterException.Of("Unhandled BaseDeviceMeasurement type");
            }
        }

        public override string ToString()
        {
            return string.Format("DustDto SerialNumber={0} Avrg={1} Pm1={2} Pm2_5={3} Pm10={4} PmTota={5}"
                , SerialId!, Avrg, Pm1, Pm2_5, Pm10, PmTotal);
        }
    }
}

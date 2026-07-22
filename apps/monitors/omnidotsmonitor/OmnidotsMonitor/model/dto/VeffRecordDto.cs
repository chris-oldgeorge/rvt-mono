using Omnidots.Model.Json;

namespace Omnidots.Model.Dto
{

    public class VeffRecordDto
    {

        public double? X { get; }
        public double? Y { get; }
        public double? Z { get; }

        public int MeasurementDuration { get; }
        public DateTime SampleTime { get; set; }


        public VeffRecordDto(VeffSample sample) :
            this(x: sample.X, y: sample.Y, z: sample.Z, epocMillis: sample.Timestamp)
        {

        }

        public VeffRecordDto(double x, double y, double z, double epocMillis)
        {
            X = x;
            Y = y;
            Z = z;
            var offset = DateTimeOffset.FromUnixTimeMilliseconds((long)epocMillis);
            SampleTime = offset.DateTime;
        }
    }
}


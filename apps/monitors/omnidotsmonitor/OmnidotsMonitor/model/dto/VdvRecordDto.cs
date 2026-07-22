using Omnidots.Model.Json;

namespace Omnidots.Model.Dto
{

    public class VdvRecordDto
    {

        public double? X { get; }
        public double? Y { get; }
        public double? Z { get; }

        public string? VdvX { get; }
        public string? VdvY { get; }
        public string? VdvZ { get; }

        public int MeasurementDuration { get; }
        public DateTime SampleTime { get; set; }


        public VdvRecordDto(VdvSample sample) :
            this(x: sample.X,
                 y: sample.Y,
                 z: sample.Z,
                 epocMillis: sample.Timestamp,
                 vdvX: sample.VdvX,
                 vdvY: sample.VdvY,
                 vdvZ: sample.VdvZ)
        {

        }

        public VdvRecordDto(double x, double y, double z, double epocMillis,
            string? vdvX, string? vdvY, string? vdvZ)
        {
            X = x;
            Y = y;
            Z = z;
            var offset = DateTimeOffset.FromUnixTimeMilliseconds((long)epocMillis);
            SampleTime = offset.DateTime;
            VdvX = vdvX;
            VdvY = vdvY;
            VdvZ = vdvZ;

        }
    }
}

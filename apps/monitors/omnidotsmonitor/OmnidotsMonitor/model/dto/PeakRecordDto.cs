using Omnidots.Model.Json;

namespace Omnidots.Model.Dto
{

    public class PeakRecordDto
    {

        public FDomVtopOverflow? X { get; }
        public FDomVtopOverflow? Y { get; }
        public FDomVtopOverflow? Z { get; }

        public int MeasurementDuration { get; }
        public DateTime SampleTime { get; set; }


        public PeakRecordDto(PeakSample sample) :
            this(x: sample.X, y: sample.Y, z: sample.Z, epocMillis: sample.Timestamp)
        {

        }

        public PeakRecordDto(FDomVtopOverflow? x, FDomVtopOverflow? y, FDomVtopOverflow? z, double epocMillis)
        {
            X = x;
            Y = y;
            Z = z;
            var offset = DateTimeOffset.FromUnixTimeMilliseconds((long)epocMillis);
            SampleTime = offset.DateTime;
        }
    }
}


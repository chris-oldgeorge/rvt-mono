using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class MyAtmDustLevel8hourAvg
{
    public string SerialId { get; set; } = null!;

    public DateTime SampleTime { get; set; }

    public double? Pm1 { get; set; }

    public double? Pm25 { get; set; }

    public double? Pm10 { get; set; }

    public double? PmTotal { get; set; }
}

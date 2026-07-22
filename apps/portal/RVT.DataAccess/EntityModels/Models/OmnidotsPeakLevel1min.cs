using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsPeakLevel1min
{
    public string SerialId { get; set; } = null!;

    public DateTime SampleTime { get; set; }

    public double? Xvtop { get; set; }

    public double? Yvtop { get; set; }

    public double? Zvtop { get; set; }
}

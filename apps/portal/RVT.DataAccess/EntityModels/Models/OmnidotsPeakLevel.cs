using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsPeakLevel
{
    public string SerialId { get; set; } = null!;

    public DateTime SampleTime { get; set; }

    public double? Xfdom { get; set; }

    public double? Xvtop { get; set; }

    public double? XvtopOverflow { get; set; }

    public double? Yfdom { get; set; }

    public double? Yvtop { get; set; }

    public double? YvtopOverflow { get; set; }

    public double? Zfdom { get; set; }

    public double? Zvtop { get; set; }

    public double? ZvtopOverflow { get; set; }
}

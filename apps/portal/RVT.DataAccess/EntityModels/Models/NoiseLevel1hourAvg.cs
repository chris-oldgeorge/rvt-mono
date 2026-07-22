using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class NoiseLevel1hourAvg
{
    public string SerialId { get; set; } = null!;

    public DateTime SampleTime { get; set; }

    public double? Laeq { get; set; }

    public double? Lamax { get; set; }

    public double? La90 { get; set; }

    public double? La10 { get; set; }

    public double? Lceq { get; set; }

    public double? Lcmax { get; set; }

    public double? Lc90 { get; set; }

    public double? Lc10 { get; set; }
}

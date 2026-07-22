using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class MonitorMeasurementRemovalImpact
{
    public string SerialId { get; set; } = null!;

    public long MeasurementTableCount { get; set; }

    public long MeasurementRowCount { get; set; }
}

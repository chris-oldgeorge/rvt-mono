using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsTrace
{
    public Guid? TraceId { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Z { get; set; }

    public virtual OmnidotsTracesIndex? Trace { get; set; }
}

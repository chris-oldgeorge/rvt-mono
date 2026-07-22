using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsTraces
{
    public Guid TraceId { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Z { get; set; }
}

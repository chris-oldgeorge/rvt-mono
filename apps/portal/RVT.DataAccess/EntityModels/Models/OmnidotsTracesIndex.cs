using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsTracesIndex
{
    public Guid Id { get; set; }

    public string? SerialId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}

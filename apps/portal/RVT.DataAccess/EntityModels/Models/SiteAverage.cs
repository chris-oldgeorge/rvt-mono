using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class SiteAverage
{
    public Guid Id { get; set; }

    public Guid SiteId { get; set; }

    public Guid MonitorId { get; set; }

    public string Field { get; set; } = null!;

    public double Level { get; set; }

    public DateTime CollectionTime { get; set; }
}

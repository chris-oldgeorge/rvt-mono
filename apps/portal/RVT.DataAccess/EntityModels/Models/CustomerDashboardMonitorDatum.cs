using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class CustomerDashboardMonitorDatum
{
    public int? Nr { get; set; }

    public string MonitorState { get; set; } = null!;

    public Guid UserId { get; set; }
}

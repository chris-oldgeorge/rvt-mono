using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class AdminDashboardDatum
{
    public int? Nr { get; set; }

    public string MonitorState { get; set; } = null!;
}

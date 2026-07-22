using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class SvantekMonitorStatus
{
    public int ProjectId { get; set; }

    public int PointId { get; set; }

    public string SerialId { get; set; } = null!;

    public int ErrorCount { get; set; }

    public string? Type { get; set; }

    public string? Active { get; set; }

    public string? Lastlogin { get; set; }

    public string? Lastlogout { get; set; }

    public string? Isonline { get; set; }

    public decimal? Meterfirmware { get; set; }

    public string? Laststatustimestamp { get; set; }

    public int? Batterycharge { get; set; }

    public int? Batterytimetoempty { get; set; }

    public string? Powersource { get; set; }

    public string? Isbatterycharging { get; set; }

    public int? Gsmsignalquality { get; set; }

    public string? Measurementstate { get; set; }
}

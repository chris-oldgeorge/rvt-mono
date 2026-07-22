using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class MonitorUserSearch
{
    public Guid Id { get; set; }

    public bool? Active { get; set; }

    public Guid? DeploymentId { get; set; }

    public string? FleetNr { get; set; }

    public string SerialId { get; set; } = null!;

    public int TypeOfMonitor { get; set; }

    public bool? OffLine { get; set; }

    public bool? Battery { get; set; }

    public bool? Alerts { get; set; }

    public bool? Cautions { get; set; }

    public int? LocationId { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? What3words { get; set; }

    public string? ContractNumber { get; set; }

    public Guid? SiteiD { get; set; }

    public string? SiteName { get; set; }

    public Guid UserId { get; set; }

    public DateTime? LastDataTime { get; set; }
}

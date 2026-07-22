using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class NotificationUserSearch
{
    public Guid Id { get; set; }

    public string? FleetNr { get; set; }

    public Guid? MonitorId { get; set; }

    public string? SerialId { get; set; }

    public int? TypeOfMonitor { get; set; }

    public int AlertType { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string AlertField { get; set; } = null!;

    public double LimitOn { get; set; }

    public double Level { get; set; }

    public DateTime NotificationTime { get; set; }

    public Guid? ContractId { get; set; }

    public string? ContractNumber { get; set; }

    public Guid? SiteiD { get; set; }

    public string? SiteName { get; set; }

    public Guid UserId { get; set; }

    public DateTime? LastDataTime { get; set; }
}

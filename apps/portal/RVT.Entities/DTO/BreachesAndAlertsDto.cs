// File summary: Defines transport data used by API, repository, and monitoring workflows.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities.DTO;

public class BreachesAndAlertsDto
{
    public string? SerialID { get; set; }

    public string? FleetNr { get; set; }

    public Guid? MonitorId { get; set; }
    public DateTime? SampleTime { get; set; }
    public Guid? NotificationId { get; set; }

    public DateTime? NotificationTime { get; set; }
    public double? XVtop { get; set; }

    public double? YVtop { get; set; }

    public double? ZVtop { get; set; }

}

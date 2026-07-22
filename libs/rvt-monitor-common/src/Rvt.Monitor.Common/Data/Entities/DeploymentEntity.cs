namespace Rvt.Monitor.Common.Data.Entities;

public sealed class DeploymentEntity
{
    public Guid Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double Lng { get; set; }
    public double Lat { get; set; }
    public string? What2words { get; set; }
    public string? What3Words { get; set; }
    public string? PictureLink { get; set; }
    public Guid ContractId { get; set; }
    public Guid MonitorId { get; set; }
}

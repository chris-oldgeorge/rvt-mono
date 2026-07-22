namespace Rvt.Monitor.Common.Data.Entities;

public sealed class ContractEntity
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime OnHireDate { get; set; }
    public DateTime? OffHireDate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SiteId { get; set; }
}

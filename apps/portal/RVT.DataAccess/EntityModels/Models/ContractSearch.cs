using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ContractSearch
{
    public Guid Id { get; set; }

    public string ContractNumber { get; set; } = null!;

    public DateTime? OffHireDate { get; set; }

    public DateTime OnHireDate { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? SiteiD { get; set; }

    public string? CompanyName { get; set; }

    public string? SiteName { get; set; }

    public string? SiteAddress { get; set; }
}

using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportUserSearch
{
    public Guid Id { get; set; }

    public string SiteName { get; set; } = null!;

    public Guid SiteId { get; set; }

    public DateTime ReportDate { get; set; }

    public DateTime ReportFrom { get; set; }

    public DateTime ReportTo { get; set; }

    public string ReportLink { get; set; } = null!;

    public Guid ReportRuleId { get; set; }

    public int Frequency { get; set; }

    public string? ReportName { get; set; }

    public bool Deleted { get; set; }

    public Guid UserId { get; set; }
}

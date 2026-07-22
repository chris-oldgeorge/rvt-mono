using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportRuleUserSearch
{
    public Guid Id { get; set; }

    public string SiteName { get; set; } = null!;

    public Guid SiteId { get; set; }

    public int Frequency { get; set; }

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public string? ReportName { get; set; }

    public DateTime? LastGenerated { get; set; }

    public Guid UserId { get; set; }
}

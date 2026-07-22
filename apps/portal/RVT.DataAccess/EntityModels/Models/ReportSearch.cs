using System;
using RVT.Entities;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportSearch
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

    public string? Contracts { get; set; }

    public string FrequencyStr
    {
        get
        {
            return (ReportFrequencyType)Frequency == ReportFrequencyType.WeeklyAndMonthly ? "Weekly and Monthly" : ((ReportFrequencyType)Frequency).ToString();
        }
    }
}

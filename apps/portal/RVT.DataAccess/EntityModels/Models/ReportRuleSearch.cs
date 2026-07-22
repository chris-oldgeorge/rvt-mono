using System;
using RVT.Entities;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportRuleSearch
{
    public Guid Id { get; set; }

    public string SiteName { get; set; } = null!;

    public Guid SiteId { get; set; }

    public ReportFrequencyType Frequency { get; set; } // This will be changed to if you refresh against DB. Need to be readded manually

    public DayOfWeek? DayOfWeek { get; set; } // This will be changed to if you refresh against DB. Need to be readded manually

    public int? DayOfMonth { get; set; }

    public string? ReportName { get; set; }

    public DateTime? LastGenerated { get; set; }
    public string FrequencyStr // This will be changed to if you refresh against DB. Need to be readded manually
    {
        get
        {
            return Frequency == ReportFrequencyType.WeeklyAndMonthly ? "Weekly and Monthly" : Frequency.ToString();
        }
    }
}

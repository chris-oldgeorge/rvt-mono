using System;
using RVT.Entities;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportRule
{
    public Guid Id { get; set; } = Guid.NewGuid(); // This will be changed to if you refresh against DB. Need to be readded manually
    public Guid SiteId { get; set; }

    public Guid UserId { get; set; }

    public ReportFrequencyType Frequency { get; set; } // This will be changed to if you refresh against DB. Need to be readded manually

    public DateTime? LastGenerated { get; set; }

    public string? ReportName { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public bool Deleted { get; set; }
}

// File summary: Defines per-day operating hours for an RVT site.
// Major updates:
// - 2026-06-08 pending Added daily site operating-hours entity for SPA site scheduling.

using System;

namespace RVT.Entities
{
    public class SiteOperatingHours : BaseEntity
    {
        public Guid SiteId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool IsClosed { get; set; }
        public Site? Site { get; set; }
    }
}

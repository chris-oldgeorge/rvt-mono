// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-08 pending Added daily operating-hours navigation for per-day site schedules.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public class Site : BaseEntity
    {

        public string SiteName { get; set; } = string.Empty;
        public DateTime CreateDate { get; set; }
        [StringLength(100)]
        public string? AddressLine1 { get; set; }
        [StringLength(100)]
        public string? AddressLine2 { get; set; }
        [StringLength(10)]
        public string? Postcode { get; set; }
        [StringLength(30)]
        public string? City { get; set; }
        [StringLength(30)]
        public string? County { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public TimeSpan? SatStartTime { get; set; }
        public TimeSpan? SatEndTime { get; set; }
        public TimeSpan? SunStartTime { get; set; }
        public TimeSpan? SunEndTime { get; set; }
        public bool Archived { get; set; }
        public List<Contract> Contracts { get; set; } = [];
        public List<SiteOperatingHours> OperatingHours { get; set; } = [];
    }
}

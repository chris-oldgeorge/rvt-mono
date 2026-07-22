// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-06-10 pending Removed stale commented-out notification properties for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public class Notification : BaseEntity
    {
        public DateTime NotificationTime { get; set; }

        //Alert trigger value
        public double LimitOn { get; set; }
        public int AveragingPeriod { get; set; }
        //The actual value of breach
        public double Level { get; set; }
        public DateTime? ClosedTime { get; set; }
        public Guid? ClosedByUser { get; set; }
        [StringLength(255)]
        public string? ClosedNote { get; set; }

        [ForeignKey("MonitorId")]
        public Guid MonitorId { get; set; }
        public virtual Monitor Monitor { get; set; } = null!;

        [StringLength(32)]
        public string AlertField { get; set; } = null!;

        public AlertTypeEnum AlertType { get; set; }

        [StringLength(255)]
        public string? RecordingLink { get; set; }
    }
}

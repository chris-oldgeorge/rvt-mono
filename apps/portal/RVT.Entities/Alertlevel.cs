// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-06-10 pending Removed stale commented-out alert level properties for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public enum AlertTypeEnum
    {
        Alert = 0,
        Caution = 1,
        Offline = 2,
        BatteryAlert = 4,
        BatteryCaution = 5
    }
    public enum BatteryAlertTypeEnum
    {
        Off = 0,
        BatteryAlert = 1,
        BatteryCaution = 2
    }

    public enum AveragingPeriodsDustEnum
    {
        [Display(Name = "By Alpha")]
        _1_min = 60,
        _15_min = 900,
        _1_hour = 3600,
        _8_hour = 28800,
        _1_day = 86400
    }

    public enum AveragingPeriodsNoiseEnum
    {
        [Display(Name = "By Alpha")]
        _15_min = 900,
        _1_hour = 3600,
        _1_day = 86400,
        _Site_hours = 0
    }

    public enum AveragingPeriodsVibrationEnum
    {
        [Display(Name = "By Alpha")] _1_min = 60
    }

    public class Alertlevel : BaseEntity
    {
        [StringLength(32)]
        public string SerialId { get; set; } = null!;

        [StringLength(32)]
        public string AlertField { get; set; } = null!;

        public double LimitOn { get; set; }

        public double LimitOff { get; set; }

        public AlertTypeEnum AlertType { get; set; }

        public bool IsActive { get; set; }

        public int AveragingPeriod { get; set; }

        public bool Weekdays { get; set; }

        public bool Saturdays { get; set; }

        public bool Sundays { get; set; }

        public TimeSpan? StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }
        public bool IsDeleted { get; set; }


        [ForeignKey("MonitorId")]
        public Guid MonitorId { get; set; }
        public virtual Monitor Monitor { get; set; } = null!;

    }
}

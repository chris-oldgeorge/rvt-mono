// File summary: Defines transport data used by API, repository, and monitoring workflows.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities.DTO
{
    public class SvantekBatteryStatus
    {
        public string SerialId { get; set; } = null!;

        public DateTime Lastseen { get; set; }

        public int BatteryCharge { get; set; }
        public int? Batterytimetoempty { get; set; }

        public string? Powersource { get; set; }
    }
}

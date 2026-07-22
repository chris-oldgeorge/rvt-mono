// File summary: Defines transport data used by API, repository, and monitoring workflows.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities.DTO
{
    public class MonitorStatusForMonthDto
    {
        public int Day { get; set; }
        public AlertTypeEnum Status { get; set; }
    }
}

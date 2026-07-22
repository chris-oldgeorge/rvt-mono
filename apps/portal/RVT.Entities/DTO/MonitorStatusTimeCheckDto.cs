// File summary: Defines transport data used by API, repository, and monitoring workflows.
// Major updates:
// - 2026-06-10 pending Removed stale commented-out DTO property for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVT.Entities.DTO
{
    public class MonitorStatusTimeCheckDto
    {
        public DateTime MonitorDate { get; set; }
        public DateTime UtcDate { get; set; }
    }
}

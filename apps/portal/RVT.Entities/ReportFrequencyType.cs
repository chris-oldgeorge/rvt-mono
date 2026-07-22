// File summary: Report scheduling frequency shared across the domain, adapters, and application layers.
// Major updates:
// - 2026-07-10 pending Moved ReportFrequencyType into the domain so BusinessLogic no longer depends on the EF entity models.

namespace RVT.Entities
{
    public enum ReportFrequencyType
    {
        Off = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        WeeklyAndMonthly = 4,
    }
}

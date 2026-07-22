// File summary: Provides data access operations for omnidots breaches and alerts repository entities and search projections.
// Major updates:
// - 2026-07-09 pending Read canonical PostgreSQL routine result aliases with legacy fallback.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.
// - 2026-06-09 pending Documented PostgreSQL canonical routine-name mapping for breach/alert calls.

using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities.DTO;

namespace RVT.DataAccess
{
    // This is a pure stored-routine adapter: it owns no DbSet, so it does not derive from GenericRepository.
    // It previously declared GenericRepository<OmnidotsBreachesAndAlertsRepository> - passing itself as the
    // entity type - which only ever worked because EF resolves Set<TEntity>() lazily and the set was unused.
    public class OmnidotsBreachesAndAlertsRepository : IOmnidotsBreachesAndAlertsRepository
    {
        private readonly IRvtStoredRoutineExecutor routineExecutor;

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public OmnidotsBreachesAndAlertsRepository(IRvtStoredRoutineExecutor routineExecutor)
        {
            this.routineExecutor = routineExecutor;
        }

        // Function summary: Handles the breaches and alerts for date workflow for this module.
        public async Task<List<BreachesAndAlertsDto>> BreachesAndAlertsForDate(DateTime date)
        {
            // PostgreSQL routine execution maps this legacy name to public.peak_record_breach_and_alerts.
            var rows = await routineExecutor.QueryAsync(
                "PeakRecordBreachAndAlerts",
                new[] { new RvtRoutineParameter("Date", date) },
                reader => new BreachesAndAlertsDto
                {
                    SerialID = reader.GetNullableString("serial_id", "SerialId"),
                    FleetNr = reader.GetNullableString("fleet_nr", "FleetNr"),
                    MonitorId = reader.GetNullableValue<Guid>("monitor_id", "Monitor Id"),
                    SampleTime = reader.GetNullableValue<DateTime>("sample_time", "SampleTime"),
                    NotificationId = reader.GetNullableValue<Guid>("notification_id", "Notification Id"),
                    NotificationTime = reader.GetNullableValue<DateTime>("notification_time", "NotificationTime"),
                    XVtop = reader.GetNullableValue<double>("x_vtop", "XVtop"),
                    YVtop = reader.GetNullableValue<double>("y_vtop", "YVtop"),
                    ZVtop = reader.GetNullableValue<double>("z_vtop", "ZVtop")
                });

            return rows.ToList();
        }
    }
}

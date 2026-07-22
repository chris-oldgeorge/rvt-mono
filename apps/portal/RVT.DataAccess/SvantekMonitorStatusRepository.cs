// File summary: Provides data access operations for svantek monitor status repository entities and search projections.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities.DTO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.DataAccess
{
    public class SvantekMonitorStatusRepository : GenericRepository<SvantekMonitorStatus>, ISvantekMonitorStatusRepository
    {
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public SvantekMonitorStatusRepository(RVTSearchContext ContextDB)
            : base(ContextDB)
        {
        }

        // Function summary: Retrieves battery level data for callers.
        public async Task<SvantekBatteryStatus?> ReadBatteryLevelAsync(string SerialId)
        {
            var details = await DbSet
                .AsNoTracking()
                .Where(W => W.SerialId == SerialId)
                .FirstOrDefaultAsync();
            if (details is null)
                return null;

            return new SvantekBatteryStatus
            {
                SerialId = details.SerialId,
                // Timestamps here are UTC. DateTime.Now produced a local "last seen", and DateTime.Parse without
                // styles produced Kind=Unspecified - which Npgsql refuses to write to a timestamptz column.
                Lastseen = string.IsNullOrWhiteSpace(details.Laststatustimestamp)
                    ? DateTime.UtcNow
                    : ParseUtc(details.Laststatustimestamp), //should always have a value
                BatteryCharge = details.Batterycharge ?? 0,
                Powersource = details.Powersource,
                Batterytimetoempty = details.Batterytimetoempty //Expressed as hours*10+(minutes / 6). Value equal 100 or greater than 10000 denotes no correct reading (temporary).
            };
        }

        // Function summary: Parses a stored status timestamp as UTC regardless of whether it carries an offset.
        private static DateTime ParseUtc(string timestamp)
        {
            return DateTime.Parse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}

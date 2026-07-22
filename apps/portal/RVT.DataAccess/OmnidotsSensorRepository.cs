// File summary: Provides data access operations for omnidots sensor repository entities and search projections.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-25 pending Resolved legacy nullable reference warnings.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.DataAccess
{
    public class OmnidotsSensorRepository : GenericRepository<OmnidotsSensor>, IOmnidotsSensorRepository
    {
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public OmnidotsSensorRepository(RVTSearchContext ContextDB)
            : base(ContextDB)
        {
        }

        // Function summary: Retrieves battery level data for callers.
        public async Task<BatteryLevel?> ReadBatteryLevelAsync(string SerialId)
        {
            var details = await DbSet.Where(W => W.SerialId == SerialId).FirstOrDefaultAsync();
            if (details is null)
                return null;

            return new BatteryLevel { SerialId = details.SerialId, Lastseen = details.Lastseen, BatteryCharge = details.BatteryCharge };
        }
    }
}

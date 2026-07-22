// File summary: Driven (outbound) persistence port for Omnidots breach/alert reads, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the breaches-and-alerts repository contract out of the EF adapter into the core ports.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVT.Entities.DTO;

namespace RVT.Entities.Ports.Persistence
{
    public interface IOmnidotsBreachesAndAlertsRepository
    {
        Task<List<BreachesAndAlertsDto>> BreachesAndAlertsForDate(DateTime date);
    }
}

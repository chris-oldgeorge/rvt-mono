// File summary: Driven (outbound) persistence port for Svantek monitor-status battery reads, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the Svantek monitor-status repository contract out of the EF adapter into the core ports.

using System.Threading.Tasks;
using RVT.Entities.DTO;

namespace RVT.Entities.Ports.Persistence
{
    public interface ISvantekMonitorStatusRepository
    {
        Task<SvantekBatteryStatus?> ReadBatteryLevelAsync(string SerialId);
    }
}

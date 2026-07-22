// File summary: Driven (outbound) persistence port for Omnidots sensor battery reads, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the Omnidots sensor repository contract out of the EF adapter into the core ports.

using System.Threading.Tasks;
using RVT.Entities.DTO;

namespace RVT.Entities.Ports.Persistence
{
    public interface IOmnidotsSensorRepository
    {
        Task<BatteryLevel?> ReadBatteryLevelAsync(string SerialId);
    }
}

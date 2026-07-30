// File summary: Driven (outbound) persistence port for monitor access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the monitor repository contract out of the EF adapter into the core ports.

namespace RVT.Entities.Ports.Persistence;

public interface IMonitorRepository
{
    Task<Monitor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

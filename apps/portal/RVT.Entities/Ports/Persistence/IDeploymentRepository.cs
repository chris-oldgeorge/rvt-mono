// File summary: Driven (outbound) persistence port for deployment access, owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Moved the deployment repository contract out of the EF adapter into the core ports.

using System;
using System.Threading.Tasks;

namespace RVT.Entities.Ports.Persistence
{
    public interface IDeploymentRepository
    {
        Task<Deployment?> GetByIdAsync(Guid Id);
    }
}

// File summary: Provides data access operations for deployment repository entities and search projections.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using RVT.DataAccess.Context;
using RVT.Entities;

namespace RVT.DataAccess;

public class DeploymentRepository : GenericRepository<Deployment>, IDeploymentRepository
{
    // Function summary: Handles the deployment repository workflow for this module.
    public DeploymentRepository(RVTDbContext contextDB)
        : base(contextDB)
    {
    }

}

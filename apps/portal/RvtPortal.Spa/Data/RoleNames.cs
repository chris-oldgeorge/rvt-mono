// File summary: Defines ASP.NET Identity and seed-data infrastructure for the portal host.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

namespace RvtPortal.Spa.Data;

public static class RoleNames
{
    public const string RVTMasterAdmin = "RVTMasterAdmin";
    public const string RVTAdmin = "RVTAdmin";
    public const string RVTInstaller = "RVTInstaller";
    public const string CompanyUser = "CompanyUser";
}

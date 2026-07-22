// File summary: Defines ASP.NET Identity and seed-data infrastructure for the portal host.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
namespace RvtPortal.Spa.Data;

public static class RoleAuthorization
{
    public const string AdminRoles = RoleNames.RVTMasterAdmin + "," + RoleNames.RVTAdmin;
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class AuthorizeRolesAttribute : AuthorizeAttribute
{
    // Function summary: Handles the authorize roles attribute workflow for this module.
    public AuthorizeRolesAttribute(params string[] roles)
    {
        Roles = string.Join(",", roles);
    }
}

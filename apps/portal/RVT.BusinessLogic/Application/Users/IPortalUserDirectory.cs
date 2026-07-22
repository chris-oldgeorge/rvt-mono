// File summary: Defines the business-layer user directory port used without depending on ASP.NET Identity.
// Major updates:
// - 2026-07-05 pending Added portal user directory abstractions for report-recipient business workflows.

namespace RVT.BusinessLogic.Application.Users;

public static class PortalRoleNames
{
    public const string RVTMasterAdmin = "RVTMasterAdmin";
    public const string RVTAdmin = "RVTAdmin";
    public const string RVTInstaller = "RVTInstaller";
    public const string CompanyUser = "CompanyUser";
}

public sealed record PortalUserProfile(
    Guid UserId,
    string UserIdText,
    Guid? CompanyId,
    bool IsDisabled,
    string? Name,
    string Email,
    string? PhoneNumber,
    string? CompanyRole,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles)
{
    public string PrimaryRole => Roles.Count > 0 ? Roles[0] : "";

    // Function summary: Checks role membership using the same exact role names persisted by Identity.
    public bool IsInRole(string role)
    {
        return Roles.Contains(role, StringComparer.Ordinal);
    }
}

public interface IPortalUserDirectory
{
    // Function summary: Lists portal users with their role facts for business-layer assignment decisions.
    Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(CancellationToken cancellationToken);

    // Function summary: Looks up one portal user by parsed Guid identity id.
    Task<PortalUserProfile?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}

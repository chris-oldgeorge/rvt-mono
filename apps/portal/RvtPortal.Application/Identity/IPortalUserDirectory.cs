namespace RvtPortal.Application.Identity;

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

    public bool IsInRole(string role) =>
        Roles.Contains(role, StringComparer.Ordinal);
}

public interface IPortalUserDirectory
{
    Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(
        CancellationToken cancellationToken);

    Task<PortalUserProfile?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

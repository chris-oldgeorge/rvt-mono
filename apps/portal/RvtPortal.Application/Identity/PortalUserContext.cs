namespace RvtPortal.Application.Identity;

public sealed record PortalUserContext(
    Guid? UserId,
    string? UserName,
    Guid? CompanyId,
    bool IsAdmin,
    bool IsInstaller,
    bool IsCompanyUser);

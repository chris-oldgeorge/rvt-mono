// File summary: Describes the authenticated portal user facts needed by business-layer workflows.
// Major updates:
// - 2026-07-05 pending Added transport-neutral user context for controller-to-business refactoring.

namespace RVT.BusinessLogic.Application;

public sealed record PortalUserContext(
    Guid? UserId,
    string? UserName,
    Guid? CompanyId,
    bool IsAdmin,
    bool IsInstaller,
    bool IsCompanyUser);

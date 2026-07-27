// File summary: Defines application-level authorization rules for published and administrative Help workflows.
// Major updates:
// - 2026-07-28 Added defense-in-depth Help authorization independent of ASP.NET Core roles.

using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Help;

public static class HelpAuthorizationPolicy
{
    public static bool CanReadPublished(PortalUserContext actor) =>
        actor.IsAdmin || actor.IsCompanyUser;

    public static bool CanManage(PortalUserContext actor) =>
        actor.IsAdmin;
}

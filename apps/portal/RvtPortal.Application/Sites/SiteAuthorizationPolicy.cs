using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Sites;

public static class SiteAuthorizationPolicy
{
    public static SiteAccessScope ReadScope(PortalUserContext user, DateTime nowUtc)
    {
        if (user.IsAdmin)
        {
            return SiteAccessScope.All(nowUtc);
        }

        return user.IsCompanyUser && user.UserId.HasValue
            ? SiteAccessScope.Assigned(user.UserId.Value, nowUtc)
            : SiteAccessScope.None(nowUtc);
    }

    public static bool CanManage(PortalUserContext user) => user.IsAdmin;

    public static bool CanUpdateNotificationSetting(
        PortalUserContext user,
        Guid targetUserId) =>
        user.IsAdmin || (user.IsCompanyUser && user.UserId == targetUserId);
}

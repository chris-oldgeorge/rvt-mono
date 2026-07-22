// File summary: Defines the shared inclusive time-window predicate for site-assignment authorization queries.
// Major updates:
// - 2026-07-22 pending Centralized current site-assignment authorization semantics.

using System.Linq.Expressions;
using RVT.Entities;

namespace RvtPortal.Spa.Application.Sites;

public static class ActiveSiteAssignment
{
    // Function summary: Matches one user's assignments whose inclusive start/end window contains the supplied UTC instant.
    public static Expression<Func<SiteUsers, bool>> ForUser(Guid userId, DateTime nowUtc)
    {
        return siteUser =>
            siteUser.UserId == userId &&
            siteUser.StartDate <= nowUtc &&
            (!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= nowUtc);
    }
}

// File summary: Defines the shared inclusive time-window predicate for site-assignment authorization queries.
// Major updates:
// - 2026-07-31 pending Exposed the window-only predicate so the report-rule reads stop restating it.
// - 2026-07-22 pending Centralized current site-assignment authorization semantics.

using System.Linq.Expressions;
using RVT.Entities;

namespace RvtPortal.Spa.UseCases.Sites;

public static class ActiveSiteAssignment
{
    /// <summary>
    /// Matches assignments whose inclusive start/end window contains <paramref name="nowUtc"/> - the one
    /// definition of "this assignment is active right now". Compose it with whatever else a query needs
    /// (a site, a set of users); EF ANDs successive <c>Where</c> calls.
    /// <para>
    /// Seven report-rule reads used to restate this as <c>EndDate == null</c> alone, which ignores
    /// <c>StartDate</c> and treats any future end date as already inactive. Nothing writes
    /// <c>SiteUsers.EndDate</c> today, so the two forms agree - they would start dropping report recipients on
    /// the day soft-delete arrives.
    /// </para>
    /// </summary>
    public static Expression<Func<SiteUsers, bool>> At(DateTime nowUtc)
    {
        return siteUser =>
            siteUser.StartDate <= nowUtc &&
            (!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= nowUtc);
    }

    /// <summary>
    /// The user-scoped form of <see cref="At"/>. The window clause is spelled out again rather than composed
    /// because an expression tree cannot invoke another one and still translate; the two are pinned as
    /// equivalent by <c>ActiveSiteAssignmentTests</c>.
    /// </summary>
    public static Expression<Func<SiteUsers, bool>> ForUser(Guid userId, DateTime nowUtc)
    {
        return siteUser =>
            siteUser.UserId == userId &&
            siteUser.StartDate <= nowUtc &&
            (!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= nowUtc);
    }
}

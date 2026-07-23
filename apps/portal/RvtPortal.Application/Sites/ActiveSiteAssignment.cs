namespace RvtPortal.Application.Sites;

public sealed record SiteAssignmentWindow(
    Guid UserId,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);

public static class ActiveSiteAssignment
{
    public static bool IsActive(
        SiteAssignmentWindow assignment,
        Guid userId,
        DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Assignment comparison time must be UTC.", nameof(nowUtc));
        }

        return assignment.UserId == userId
            && assignment.StartDateUtc <= nowUtc
            && (!assignment.EndDateUtc.HasValue || assignment.EndDateUtc.Value >= nowUtc);
    }
}

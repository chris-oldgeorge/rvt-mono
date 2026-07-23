namespace RvtPortal.Application.Sites;

public enum SiteAccessScopeKind
{
    None,
    All,
    Assigned
}

public sealed record SiteAccessScope(
    SiteAccessScopeKind Kind,
    Guid? UserId,
    DateTime NowUtc)
{
    public static SiteAccessScope All(DateTime nowUtc) =>
        new(SiteAccessScopeKind.All, null, RequireUtc(nowUtc));

    public static SiteAccessScope Assigned(Guid userId, DateTime nowUtc) =>
        new(SiteAccessScopeKind.Assigned, userId, RequireUtc(nowUtc));

    public static SiteAccessScope None(DateTime nowUtc) =>
        new(SiteAccessScopeKind.None, null, RequireUtc(nowUtc));

    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Site access time must be UTC.", nameof(value));
}

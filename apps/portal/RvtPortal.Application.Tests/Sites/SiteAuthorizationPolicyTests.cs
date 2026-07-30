using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteAuthorizationPolicyTests
{
    private static readonly DateTime _nowUtc =
        new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReadScope_AdminCanReadAllSites()
    {
        PortalUserContext user = new(Guid.NewGuid(), "admin", null, true, false, false);

        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(user, _nowUtc);

        Assert.Equal(SiteAccessScopeKind.All, scope.Kind);
    }

    [Fact]
    public void ReadScope_CompanyUserCarriesUserAndUtcInstant()
    {
        Guid userId = Guid.NewGuid();
        PortalUserContext user = new(userId, "user", Guid.NewGuid(), false, false, true);

        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(user, _nowUtc);

        Assert.Equal(SiteAccessScopeKind.Assigned, scope.Kind);
        Assert.Equal(userId, scope.UserId);
        Assert.Equal(_nowUtc, scope.NowUtc);
    }

    [Fact]
    public void ReadScope_RejectsNonUtcClockValues()
    {
        PortalUserContext user = new(Guid.NewGuid(), "user", null, false, false, true);

        Assert.Throws<ArgumentException>(() =>
            SiteAuthorizationPolicy.ReadScope(
                user,
                DateTime.SpecifyKind(_nowUtc, DateTimeKind.Unspecified)));
    }
}

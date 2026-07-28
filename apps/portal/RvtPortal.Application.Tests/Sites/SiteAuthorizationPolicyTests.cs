using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteAuthorizationPolicyTests
{
    private static readonly DateTime NowUtc =
        new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReadScope_AdminCanReadAllSites()
    {
        PortalUserContext user = new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false);

        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(user, NowUtc);

        Assert.Equal(SiteAccessScopeKind.All, scope.Kind);
    }

    [Fact]
    public void ReadScope_CompanyUserCarriesUserAndUtcInstant()
    {
        Guid userId = Guid.NewGuid();
        PortalUserContext user = new PortalUserContext(userId, "user", Guid.NewGuid(), false, false, true);

        SiteAccessScope scope = SiteAuthorizationPolicy.ReadScope(user, NowUtc);

        Assert.Equal(SiteAccessScopeKind.Assigned, scope.Kind);
        Assert.Equal(userId, scope.UserId);
        Assert.Equal(NowUtc, scope.NowUtc);
    }

    [Fact]
    public void AssignmentWindow_IsInclusiveAtBothBounds()
    {
        Guid userId = Guid.NewGuid();
        SiteAssignmentWindow assignment = new SiteAssignmentWindow(userId, NowUtc, NowUtc);

        Assert.True(ActiveSiteAssignment.IsActive(assignment, userId, NowUtc));
        Assert.False(ActiveSiteAssignment.IsActive(
            assignment,
            userId,
            NowUtc.AddTicks(1)));
    }

    [Fact]
    public void ReadScope_RejectsNonUtcClockValues()
    {
        PortalUserContext user = new PortalUserContext(Guid.NewGuid(), "user", null, false, false, true);

        Assert.Throws<ArgumentException>(() =>
            SiteAuthorizationPolicy.ReadScope(
                user,
                DateTime.SpecifyKind(NowUtc, DateTimeKind.Unspecified)));
    }
}

// File summary: Pins the shared active-site-assignment predicates against each other and against the window rule.
// Major updates:
// - 2026-07-31 pending Added when the seven report-rule reads stopped restating the window as EndDate == null.

using RVT.Entities;
using RvtPortal.Spa.UseCases.Sites;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// <see cref="ActiveSiteAssignment"/> holds two predicates that must agree: <c>At</c> (window only, composed
/// with a site or a user set by the report-rule reads) and <c>ForUser</c> (window plus one user). An expression
/// tree cannot invoke another and still translate to SQL, so the window clause is spelled out twice; these
/// tests are what keeps the two copies honest.
/// <para>
/// The cases are the ones the discarded <c>EndDate == null</c> form got wrong: an assignment that has not
/// started yet, and one whose end date is in the future. Nothing writes <c>SiteUsers.EndDate</c> today, which
/// is exactly why a divergence here would go unnoticed until soft-delete arrives.
/// </para>
/// </summary>
public sealed class ActiveSiteAssignmentTests
{
    private static readonly DateTime _now = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _userId = Guid.Parse("2f1d5f2c-0f3f-4a3e-9a8b-6c1d0d2b7a11");

    public static TheoryData<string, DateTime, DateTime?, bool> Windows() => new()
    {
        { "started yesterday, open ended", _now.AddDays(-1), null, true },
        { "starts exactly now", _now, null, true },
        { "starts tomorrow", _now.AddDays(1), null, false },
        { "ends tomorrow", _now.AddDays(-1), _now.AddDays(1), true },
        { "ends exactly now", _now.AddDays(-1), _now, true },
        { "ended yesterday", _now.AddDays(-10), _now.AddDays(-1), false }
    };

    [Theory]
    [MemberData(nameof(Windows))]
    // Function summary: Verifies the window-only predicate implements the inclusive start/end rule.
    public void At_MatchesTheInclusiveWindow(string scenario, DateTime startDate, DateTime? endDate, bool expected)
    {
        SiteUsers assignment = Assignment(_userId, startDate, endDate);

        Assert.Equal(expected, ActiveSiteAssignment.At(_now).Compile()(assignment));
        Assert.NotEmpty(scenario);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    // Function summary: Verifies the user-scoped predicate agrees with the window-only one for the matching user.
    public void ForUser_AgreesWithAtForTheSameUser(string scenario, DateTime startDate, DateTime? endDate, bool expected)
    {
        SiteUsers assignment = Assignment(_userId, startDate, endDate);

        Assert.Equal(expected, ActiveSiteAssignment.ForUser(_userId, _now).Compile()(assignment));
        Assert.NotEmpty(scenario);
    }

    [Fact]
    // Function summary: Verifies the user-scoped predicate still rejects another user's assignment.
    public void ForUser_RejectsAnotherUsersActiveAssignment()
    {
        SiteUsers assignment = Assignment(Guid.NewGuid(), _now.AddDays(-1), null);

        Assert.True(ActiveSiteAssignment.At(_now).Compile()(assignment));
        Assert.False(ActiveSiteAssignment.ForUser(_userId, _now).Compile()(assignment));
    }

    [Fact]
    /// <summary>
    /// The form the report-rule reads used to carry. It disagrees with the window rule in both directions: it
    /// accepts an assignment that has not started, and rejects one whose end date has not arrived.
    /// </summary>
    public void DiscardedEndDateOnlyForm_DisagreesWithTheWindowRule()
    {
        SiteUsers notStarted = Assignment(_userId, _now.AddDays(1), null);
        SiteUsers endsTomorrow = Assignment(_userId, _now.AddDays(-1), _now.AddDays(1));

        Assert.Null(notStarted.EndDate);
        Assert.False(ActiveSiteAssignment.At(_now).Compile()(notStarted));
        Assert.NotNull(endsTomorrow.EndDate);
        Assert.True(ActiveSiteAssignment.At(_now).Compile()(endsTomorrow));
    }

    // Function summary: Creates one site assignment with the supplied window.
    private static SiteUsers Assignment(Guid userId, DateTime startDate, DateTime? endDate)
    {
        return new SiteUsers
        {
            Id = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            UserId = userId,
            StartDate = startDate,
            EndDate = endDate
        };
    }
}

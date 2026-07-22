using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Core.Tests.Reports;

/// <summary>
/// Covers the user-initiated one-time report guardrails required by the SPA/API plan.
/// Major updates: 2026-06-24 initial one-time report validation coverage.
/// </summary>
public sealed class OneTimeReportValidatorTests
{
    [Fact]
    public void Validate_AllowsThirtyOneDayPeriod()
    {
        var request = ValidRequest() with { ToUtc = ValidRequest().FromUtc.AddDays(31) };

        var errors = OneTimeReportValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsPeriodLongerThanThirtyOneDays()
    {
        var request = ValidRequest() with { ToUtc = ValidRequest().FromUtc.AddDays(31).AddSeconds(1) };

        var errors = OneTimeReportValidator.Validate(request);

        Assert.Contains(errors, error => error.Field == nameof(OneTimeReportRequest.ToUtc));
    }

    [Fact]
    public void Validate_RejectsStartAfterEnd()
    {
        var request = ValidRequest() with { FromUtc = ValidRequest().ToUtc };

        var errors = OneTimeReportValidator.Validate(request);

        Assert.Contains(errors, error => error.Field == nameof(OneTimeReportRequest.FromUtc));
    }

    [Fact]
    public void Validate_RejectsMissingSite()
    {
        var request = ValidRequest() with { SiteId = Guid.Empty };

        var errors = OneTimeReportValidator.Validate(request);

        Assert.Contains(errors, error => error.Field == nameof(OneTimeReportRequest.SiteId));
    }

    [Fact]
    public void Validate_RejectsInvalidRecipientEmail()
    {
        var request = ValidRequest() with { RecipientEmails = ["not an email"] };

        var errors = OneTimeReportValidator.Validate(request);

        Assert.Contains(errors, error => error.Field == nameof(OneTimeReportRequest.RecipientEmails));
    }

    private static OneTimeReportRequest ValidRequest() => new()
    {
        SiteId = Guid.NewGuid(),
        RequestedByUserId = Guid.NewGuid(),
        FromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        ToUtc = new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.Zero),
        RecipientEmails = ["ops@example.com"]
    };
}

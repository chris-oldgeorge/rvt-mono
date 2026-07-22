using System.ComponentModel.DataAnnotations;

namespace Rvt.Reporting.Core.Reports;

/// <summary>
/// Request and response contracts for user-initiated one-time report generation.
/// Major updates: 2026-06-24 added hidden-rule based one-time reporting contract.
/// </summary>
public sealed record OneTimeReportRequest
{
    public Guid SiteId { get; init; }
    public string ReportName { get; init; } = "One-time report";
    public DateTimeOffset FromUtc { get; init; }
    public DateTimeOffset ToUtc { get; init; }
    public Guid RequestedByUserId { get; init; }
    public IReadOnlyList<string> RecipientEmails { get; init; } = [];
}

public sealed record OneTimeReportResponse(Guid ReportId, Guid ReportRuleId, Uri ReportUri, DateTimeOffset FromUtc, DateTimeOffset ToUtc);

public sealed record ValidationError(string Field, string Message);

public sealed class OneTimeReportValidator
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static IReadOnlyList<ValidationError> Validate(OneTimeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (request.SiteId == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(request.SiteId), "Site is required."));
        }

        if (request.RequestedByUserId == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(request.RequestedByUserId), "Requesting user is required."));
        }

        if (request.FromUtc >= request.ToUtc)
        {
            errors.Add(new ValidationError(nameof(request.FromUtc), "Start time must be earlier than end time."));
        }

        if (request.ToUtc - request.FromUtc > TimeSpan.FromDays(31))
        {
            errors.Add(new ValidationError(nameof(request.ToUtc), "One-time reports cannot cover more than 31 days."));
        }

        if (request.RecipientEmails.Count == 0)
        {
            errors.Add(new ValidationError(nameof(request.RecipientEmails), "At least one recipient is required."));
        }

        foreach (var email in request.RecipientEmails)
        {
            if (!EmailValidator.IsValid(email))
            {
                errors.Add(new ValidationError(nameof(request.RecipientEmails), $"Invalid recipient email: {email}"));
            }
        }

        return errors;
    }
}

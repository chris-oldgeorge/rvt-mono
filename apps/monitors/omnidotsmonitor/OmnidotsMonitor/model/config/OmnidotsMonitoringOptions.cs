using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsMonitoringOptions
{
    public const string SectionName = "Omnidots:Monitoring";

    public string Recipient { get; set; } = string.Empty;
    public string TimeZoneId { get; init; } = "Europe/London";
    public TimeSpan WindowStart { get; init; } = new(8, 30, 0);
    public TimeSpan WindowEnd { get; init; } = new(18, 0, 0);
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromHours(1);

    public void Validate()
    {
        var failures = GetValidationFailures();
        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(OmnidotsMonitoringOptions), failures);
        }
    }

    internal IReadOnlyList<string> GetValidationFailures()
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(Recipient) || !MailAddress.TryCreate(Recipient, out _))
        {
            failures.Add(
                "RVT__OMNIDOTS_MONITORING_ALERT_TO or Omnidots:Monitoring:Recipient must be a valid email address.");
        }

        if (!IsValidTimeZone(TimeZoneId))
        {
            failures.Add("TimeZoneId must identify a valid timezone.");
        }

        if (WindowStart < TimeSpan.Zero || WindowStart >= TimeSpan.FromDays(1)
            || WindowEnd <= TimeSpan.Zero || WindowEnd > TimeSpan.FromDays(1))
        {
            failures.Add("Monitoring window values must be within one day.");
        }
        else if (WindowStart >= WindowEnd)
        {
            failures.Add("WindowStart must be earlier than WindowEnd.");
        }

        if (StaleAfter <= TimeSpan.Zero)
        {
            failures.Add("StaleAfter must be positive.");
        }

        return failures;
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

using Microsoft.Extensions.Options;

namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertOptionsValidator : IValidateOptions<DurableAlertOptions>
{
    public ValidateOptionsResult Validate(string? name, DurableAlertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        RequirePositive(options.BatchSize, nameof(options.BatchSize), failures);
        RequirePositive(options.LeaseSeconds, nameof(options.LeaseSeconds), failures);
        RequirePositive(options.DeliveryTimeoutSeconds, nameof(options.DeliveryTimeoutSeconds), failures);
        RequirePositive(options.InitialRetrySeconds, nameof(options.InitialRetrySeconds), failures);
        RequirePositive(options.MaxRetrySeconds, nameof(options.MaxRetrySeconds), failures);
        RequirePositive(options.MaxAttempts, nameof(options.MaxAttempts), failures);
        RequirePositive(options.PollIntervalSeconds, nameof(options.PollIntervalSeconds), failures);
        RequirePositive(options.CompletedRetentionDays, nameof(options.CompletedRetentionDays), failures);

        if (options.DeliveryTimeoutSeconds >= options.LeaseSeconds)
        {
            failures.Add("DeliveryTimeoutSeconds must be shorter than LeaseSeconds.");
        }

        if (options.MaxRetrySeconds < options.InitialRetrySeconds)
        {
            failures.Add("MaxRetrySeconds must be greater than or equal to InitialRetrySeconds.");
        }

        if (string.IsNullOrWhiteSpace(options.PortalBaseUrl) ||
            !Uri.TryCreate(options.PortalBaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("PortalBaseUrl must be an absolute URL.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(int value, string propertyName, ICollection<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{propertyName} must be positive.");
        }
    }
}

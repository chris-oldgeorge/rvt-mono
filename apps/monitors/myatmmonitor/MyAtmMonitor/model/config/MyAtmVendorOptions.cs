using Microsoft.Extensions.Options;

namespace MyAtm.Model.Config;

public sealed class MyAtmVendorOptions
{
    public const string SectionName = "MyAtmVendor";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxResponseBytes { get; set; } = 4 * 1024 * 1024;
    public int MaximumAttempts { get; set; } = 5;
    public int MinimumRequestIntervalMilliseconds { get; set; } = 500;
    public int FallbackRetryCapSeconds { get; set; } = 30;
    public int MaximumRetryDelaySeconds { get; set; } = 30;

    public void Validate()
    {
        var failures = new List<string>();
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            failures.Add("BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            failures.Add("ApiKey is required.");
        }

        if (MaxResponseBytes <= 0)
        {
            failures.Add("MaxResponseBytes must be positive.");
        }

        if (MaximumAttempts <= 0)
        {
            failures.Add("MaximumAttempts must be positive.");
        }

        if (MinimumRequestIntervalMilliseconds <= 0)
        {
            failures.Add("MinimumRequestIntervalMilliseconds must be positive.");
        }

        if (FallbackRetryCapSeconds <= 0)
        {
            failures.Add("FallbackRetryCapSeconds must be positive.");
        }

        if (MaximumRetryDelaySeconds <= 0)
        {
            failures.Add("MaximumRetryDelaySeconds must be positive.");
        }

        if (FallbackRetryCapSeconds > MaximumRetryDelaySeconds)
        {
            failures.Add("FallbackRetryCapSeconds cannot exceed MaximumRetryDelaySeconds.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(MyAtmVendorOptions), failures);
        }
    }
}

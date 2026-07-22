namespace Rvt.Monitor.Common.Delivery;

public sealed record MonitorDeliveryOptions
{
    public string Producer { get; init; } = string.Empty;
    public string InsertTopic { get; init; } = string.Empty;
    public string AlertTopic { get; init; } = string.Empty;
    public string PortalBaseUrl { get; init; } = string.Empty;
    public MonitorDeliveryFailureMode FailureMode { get; init; } = MonitorDeliveryFailureMode.DeadLetterOnly;
    public int BatchSize { get; init; } = 50;
    public TimeSpan DeliveryTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryCap { get; init; } = TimeSpan.FromMinutes(30);
    public int MaxAttempts { get; init; } = 8;

    public void Validate()
    {
        var failures = new List<string>();

        if (!MonitorDeliveryProducers.IsKnown(Producer))
        {
            failures.Add("Producer must be 'MyAtm' or 'Svantek'.");
        }

        if (string.IsNullOrWhiteSpace(InsertTopic))
        {
            failures.Add("InsertTopic is required.");
        }

        if (string.IsNullOrWhiteSpace(AlertTopic))
        {
            failures.Add("AlertTopic is required.");
        }

        if (BatchSize <= 0)
        {
            failures.Add("BatchSize must be positive.");
        }

        if (DeliveryTimeout <= TimeSpan.Zero)
        {
            failures.Add("DeliveryTimeout must be positive.");
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            failures.Add("LeaseDuration must be positive.");
        }

        if (InitialRetryDelay <= TimeSpan.Zero)
        {
            failures.Add("InitialRetryDelay must be positive.");
        }

        if (RetryCap <= TimeSpan.Zero)
        {
            failures.Add("RetryCap must be positive.");
        }

        if (RetryCap < InitialRetryDelay)
        {
            failures.Add("RetryCap must be greater than or equal to InitialRetryDelay.");
        }

        if (LeaseDuration <= DeliveryTimeout)
        {
            failures.Add("LeaseDuration must be longer than DeliveryTimeout.");
        }

        if (MaxAttempts <= 0)
        {
            failures.Add("MaxAttempts must be positive.");
        }

        if (!IsAbsoluteHttpUrl(PortalBaseUrl))
        {
            failures.Add("PortalBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", failures));
        }
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}

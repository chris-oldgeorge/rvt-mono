namespace Rvt.Monitor.Common.Alerts;

public sealed class DurableAlertOptions
{
    public const string SectionName = "Alerts:DurableDelivery";

    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 120;
    public int DeliveryTimeoutSeconds { get; set; } = 90;
    public int InitialRetrySeconds { get; set; } = 30;
    public int MaxRetrySeconds { get; set; } = 1800;
    public int MaxAttempts { get; set; } = 8;
    public int PollIntervalSeconds { get; set; } = 60;
    public int CompletedRetentionDays { get; set; } = 90;
    public string PortalBaseUrl { get; set; } = "https://www.rvtcloud.com/";
}

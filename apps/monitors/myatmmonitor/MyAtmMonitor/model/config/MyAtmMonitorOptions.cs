using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Model.Config;

public sealed class MyAtmMonitorOptions
{
    public const string SectionName = "MyAtmMonitor";

    public int CustomerId { get; init; } = 9;
    public int DevicePageSize { get; init; } = 100;
    public int MaxDevicePagesPerRun { get; init; } = 100;
    public int MeasurementPageSize { get; init; } = 1000;
    public int AccessoryPageSize { get; init; } = 1000;
    public int MaxPagesPerMonitorPerRun { get; init; } = 10;
    public int OutboxBatchSize { get; init; } = 50;
    public int OutboxDeliveryTimeoutSeconds { get; init; } = 90;
    public int OutboxLeaseSeconds { get; init; } = 120;
    public int OutboxRetrySeconds { get; init; } = 30;
    public int OutboxMaxAttempts { get; init; } = 8;
    public string PortalBaseUrl { get; init; } = "https://www.rvtcloud.com/";

    public MonitorDeliveryOptions ToDeliveryOptions(string insertTopic, string alertTopic)
    {
        Validate();
        var deliveryOptions = new MonitorDeliveryOptions
        {
            Producer = MonitorDeliveryProducers.MyAtm,
            InsertTopic = insertTopic,
            AlertTopic = alertTopic,
            PortalBaseUrl = PortalBaseUrl,
            FailureMode = MonitorDeliveryFailureMode.DeadLetterOnly,
            BatchSize = OutboxBatchSize,
            DeliveryTimeout = TimeSpan.FromSeconds(OutboxDeliveryTimeoutSeconds),
            LeaseDuration = TimeSpan.FromSeconds(OutboxLeaseSeconds),
            InitialRetryDelay = TimeSpan.FromSeconds(OutboxRetrySeconds),
            RetryCap = TimeSpan.FromMinutes(30),
            MaxAttempts = OutboxMaxAttempts
        };
        deliveryOptions.Validate();
        return deliveryOptions;
    }

    public void Validate()
    {
        var failures = new List<string>();
        if (CustomerId <= 0)
        {
            failures.Add("CustomerId must be positive.");
        }

        if (DevicePageSize <= 0)
        {
            failures.Add("DevicePageSize must be positive.");
        }

        if (MaxDevicePagesPerRun <= 0)
        {
            failures.Add("MaxDevicePagesPerRun must be positive.");
        }

        if (MeasurementPageSize <= 0)
        {
            failures.Add("MeasurementPageSize must be positive.");
        }

        if (AccessoryPageSize <= 0)
        {
            failures.Add("AccessoryPageSize must be positive.");
        }

        if (MaxPagesPerMonitorPerRun <= 0)
        {
            failures.Add("MaxPagesPerMonitorPerRun must be positive.");
        }

        if (OutboxBatchSize <= 0)
        {
            failures.Add("OutboxBatchSize must be positive.");
        }

        if (OutboxLeaseSeconds <= 0)
        {
            failures.Add("OutboxLeaseSeconds must be positive.");
        }

        if (OutboxDeliveryTimeoutSeconds <= 0 || OutboxDeliveryTimeoutSeconds >= OutboxLeaseSeconds)
        {
            failures.Add("OutboxDeliveryTimeoutSeconds must be positive and shorter than OutboxLeaseSeconds.");
        }

        if (OutboxRetrySeconds <= 0)
        {
            failures.Add("OutboxRetrySeconds must be positive.");
        }

        if (OutboxMaxAttempts <= 0)
        {
            failures.Add("OutboxMaxAttempts must be positive.");
        }

        if (string.IsNullOrWhiteSpace(PortalBaseUrl))
        {
            failures.Add("PortalBaseUrl is required.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(SectionName, typeof(MyAtmMonitorOptions), failures);
        }
    }
}

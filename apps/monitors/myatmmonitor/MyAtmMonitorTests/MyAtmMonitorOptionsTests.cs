using Microsoft.Extensions.Options;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Delivery;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmMonitorOptionsTests
{
    [TestMethod]
    public void Validate_UsesBoundedCataloguePageBudgetByDefault()
    {
        var options = new MyAtmMonitorOptions();

        options.Validate();

        Assert.AreEqual(100, options.MaxDevicePagesPerRun);
    }

    [TestMethod]
    public void Validate_RejectsNonPositiveCataloguePageBudget()
    {
        var options = new MyAtmMonitorOptions { MaxDevicePagesPerRun = 0 };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void Validate_UsesDeliveryTimeoutShorterThanLeaseByDefault()
    {
        var options = new MyAtmMonitorOptions();

        options.Validate();

        Assert.AreEqual(90, options.OutboxDeliveryTimeoutSeconds);
        Assert.AreEqual(120, options.OutboxLeaseSeconds);
    }

    [DataTestMethod]
    [DataRow(0, 120)]
    [DataRow(120, 120)]
    [DataRow(121, 120)]
    public void Validate_RejectsInvalidDeliveryTimeoutAndLeaseCombinations(int timeout, int lease)
    {
        var options = new MyAtmMonitorOptions
        {
            OutboxDeliveryTimeoutSeconds = timeout,
            OutboxLeaseSeconds = lease
        };

        Assert.ThrowsExactly<OptionsValidationException>(options.Validate);
    }

    [TestMethod]
    public void ToDeliveryOptions_MapsEveryMyAtmOutboxSettingAndValidatesTheCommonOptions()
    {
        var options = new MyAtmMonitorOptions
        {
            OutboxBatchSize = 17,
            OutboxDeliveryTimeoutSeconds = 90,
            OutboxLeaseSeconds = 150,
            OutboxRetrySeconds = 45,
            OutboxMaxAttempts = 6,
            PortalBaseUrl = "https://portal.example.test/root/"
        };

        var deliveryOptions = options.ToDeliveryOptions("myatm/inserted", "myatm/alerts");

        Assert.AreEqual(MonitorDeliveryProducers.MyAtm, deliveryOptions.Producer);
        Assert.AreEqual(MonitorDeliveryFailureMode.DeadLetterOnly, deliveryOptions.FailureMode);
        Assert.AreEqual("myatm/inserted", deliveryOptions.InsertTopic);
        Assert.AreEqual("myatm/alerts", deliveryOptions.AlertTopic);
        Assert.AreEqual(options.PortalBaseUrl, deliveryOptions.PortalBaseUrl);
        Assert.AreEqual(options.OutboxBatchSize, deliveryOptions.BatchSize);
        Assert.AreEqual(TimeSpan.FromSeconds(options.OutboxDeliveryTimeoutSeconds), deliveryOptions.DeliveryTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(options.OutboxLeaseSeconds), deliveryOptions.LeaseDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(options.OutboxRetrySeconds), deliveryOptions.InitialRetryDelay);
        Assert.AreEqual(TimeSpan.FromMinutes(30), deliveryOptions.RetryCap);
        Assert.AreEqual(options.OutboxMaxAttempts, deliveryOptions.MaxAttempts);
        deliveryOptions.Validate();
    }
}

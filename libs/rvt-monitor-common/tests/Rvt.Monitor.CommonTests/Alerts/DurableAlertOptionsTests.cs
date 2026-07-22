using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class DurableAlertOptionsTests
{
    [TestMethod]
    public void Defaults_AreTheApprovedDurableDeliveryValues()
    {
        var options = new DurableAlertOptions();

        Assert.AreEqual("Alerts:DurableDelivery", DurableAlertOptions.SectionName);
        Assert.AreEqual(50, options.BatchSize);
        Assert.AreEqual(120, options.LeaseSeconds);
        Assert.AreEqual(90, options.DeliveryTimeoutSeconds);
        Assert.AreEqual(30, options.InitialRetrySeconds);
        Assert.AreEqual(1800, options.MaxRetrySeconds);
        Assert.AreEqual(8, options.MaxAttempts);
        Assert.AreEqual(60, options.PollIntervalSeconds);
        Assert.AreEqual(90, options.CompletedRetentionDays);
        Assert.AreEqual("https://www.rvtcloud.com/", options.PortalBaseUrl);
    }

    [DataTestMethod]
    [DataRow(nameof(DurableAlertOptions.BatchSize))]
    [DataRow(nameof(DurableAlertOptions.LeaseSeconds))]
    [DataRow(nameof(DurableAlertOptions.DeliveryTimeoutSeconds))]
    [DataRow(nameof(DurableAlertOptions.InitialRetrySeconds))]
    [DataRow(nameof(DurableAlertOptions.MaxRetrySeconds))]
    [DataRow(nameof(DurableAlertOptions.MaxAttempts))]
    [DataRow(nameof(DurableAlertOptions.PollIntervalSeconds))]
    [DataRow(nameof(DurableAlertOptions.CompletedRetentionDays))]
    public void Validator_RejectsNonpositiveNumericValues(string propertyName)
    {
        var options = new DurableAlertOptions();
        typeof(DurableAlertOptions).GetProperty(propertyName)!.SetValue(options, 0);

        var result = new DurableAlertOptionsValidator().Validate(null, options);

        Assert.IsTrue(result.Failed);
        Assert.IsTrue(result.Failures.Any(failure => failure.Contains(propertyName, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validator_RejectsNegativeNumericValues()
    {
        var propertyNames = new[]
        {
            nameof(DurableAlertOptions.BatchSize),
            nameof(DurableAlertOptions.LeaseSeconds),
            nameof(DurableAlertOptions.DeliveryTimeoutSeconds),
            nameof(DurableAlertOptions.InitialRetrySeconds),
            nameof(DurableAlertOptions.MaxRetrySeconds),
            nameof(DurableAlertOptions.MaxAttempts),
            nameof(DurableAlertOptions.PollIntervalSeconds),
            nameof(DurableAlertOptions.CompletedRetentionDays)
        };

        foreach (var propertyName in propertyNames)
        {
            var options = new DurableAlertOptions();
            typeof(DurableAlertOptions).GetProperty(propertyName)!.SetValue(options, -1);

            var result = new DurableAlertOptionsValidator().Validate(null, options);

            Assert.IsTrue(result.Failed, propertyName);
        }
    }

    [DataTestMethod]
    [DataRow(120, 120)]
    [DataRow(121, 120)]
    public void Validator_RejectsTimeoutThatIsNotShorterThanLease(int timeout, int lease)
    {
        var options = new DurableAlertOptions
        {
            DeliveryTimeoutSeconds = timeout,
            LeaseSeconds = lease
        };

        Assert.IsTrue(new DurableAlertOptionsValidator().Validate(null, options).Failed);
    }

    [TestMethod]
    public void Validator_RejectsRetryCapBelowInitialDelay()
    {
        var options = new DurableAlertOptions
        {
            InitialRetrySeconds = 31,
            MaxRetrySeconds = 30
        };

        Assert.IsTrue(new DurableAlertOptionsValidator().Validate(null, options).Failed);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("relative/path")]
    public void Validator_RejectsMissingOrNonAbsolutePortalBaseUrl(string portalBaseUrl)
    {
        var options = new DurableAlertOptions { PortalBaseUrl = portalBaseUrl };

        Assert.IsTrue(new DurableAlertOptionsValidator().Validate(null, options).Failed);
    }

    [TestMethod]
    public void AddDurableAlerts_BindsValidatesAndRegistersRuntimeWithoutContextFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddDurableAlerts<TestMonitorContext>();

        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IValidateOptions<DurableAlertOptions>)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IAlertAcceptancePolicy)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IAlertCommitStore)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IAlertOutboxStore)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IAlertIngressPort)));
        Assert.AreEqual(3, services.Count(service => service.ServiceType == typeof(IAlertDeliveryAdapter)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(DurableAlertDispatcher)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(DurableAlertCleanupService)));
        Assert.IsTrue(services.Any(service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(DurableAlertBackgroundService)));
        Assert.IsFalse(services.Any(service =>
            service.ServiceType == typeof(IMonitorDbContextFactory<TestMonitorContext>)));
    }

    private sealed class TestMonitorContext(
        DbContextOptions<TestMonitorContext> options,
        MonitorDbOptions monitorOptions)
        : MonitorDbContextBase(options, monitorOptions);
}

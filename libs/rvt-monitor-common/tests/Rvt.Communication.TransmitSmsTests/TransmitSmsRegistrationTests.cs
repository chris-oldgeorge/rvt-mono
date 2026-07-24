using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;
using Rvt.Communication.TransmitSms;

namespace Rvt.Communication.TransmitSmsTests;

[TestClass]
public sealed class TransmitSmsRegistrationTests
{
    [TestMethod]
    public void AddTransmitSms_RegistersOneSmsPortOptionsAndValidationService()
    {
        var services = new ServiceCollection();
        var options = new TransmitSmsOptions { Enabled = false };

        services.AddTransmitSms(options);

        using var provider = services.BuildServiceProvider();
        Assert.IsInstanceOfType<TransmitSmsAdapter>(provider.GetRequiredService<ISmsDeliveryPort>());
        Assert.AreSame(options, provider.GetRequiredService<TransmitSmsOptions>());
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(ISmsDeliveryPort)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(TransmitSmsOptions)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void AddTransmitSms_LoadsProviderOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:SMS_ENABLED"] = "true",
                ["RVT:SMS_API_KEY"] = "api-key",
                ["RVT:SMS_API_SECRET"] = "api-secret"
            })
            .Build();

        services.AddTransmitSms(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<TransmitSmsOptions>();
        Assert.IsTrue(options.Enabled);
        Assert.AreEqual("api-key", options.ApiKey);
        Assert.AreEqual("api-secret", options.ApiSecret);
        Assert.IsInstanceOfType<TransmitSmsAdapter>(provider.GetRequiredService<ISmsDeliveryPort>());
    }

    [TestMethod]
    public void AddTransmitSms_RejectsAnExistingSmsDeliveryProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISmsDeliveryPort, ExistingSmsDeliveryPort>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddTransmitSms(new TransmitSmsOptions { Enabled = false }));

        Assert.AreEqual("An SMS delivery provider is already registered.", exception.Message);
    }

    private sealed class ExistingSmsDeliveryPort : ISmsDeliveryPort
    {
        public Task SendAsync(SmsDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;
using Rvt.Communication.SendGridMail;

namespace Rvt.Communication.SendGridMailTests;

[TestClass]
public sealed class SendGridMailRegistrationTests
{
    [TestMethod]
    public void AddSendGridMail_RegistersOneEmailPortFactoryOptionsAndValidationService()
    {
        var services = new ServiceCollection();
        var options = new SendGridMailOptions
        {
            Enabled = false,
            ApiKey = "api-key"
        };

        services.AddSendGridMail(options);

        using var provider = services.BuildServiceProvider();
        Assert.IsInstanceOfType<SendGridEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
        Assert.IsInstanceOfType<SendGridClientFactory>(provider.GetRequiredService<ISendGridClientFactory>());
        Assert.AreSame(options, provider.GetRequiredService<SendGridMailOptions>());
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IEmailDeliveryPort)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(ISendGridClientFactory)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(SendGridMailOptions)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void AddSendGridMail_LoadsProviderOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:SENDGRID_API_KEY"] = "api-key"
            })
            .Build();

        services.AddSendGridMail(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.AreEqual("api-key", provider.GetRequiredService<SendGridMailOptions>().ApiKey);
        Assert.IsInstanceOfType<SendGridEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
    }

    [TestMethod]
    public void AddSendGridMail_RejectsAnExistingEmailDeliveryProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmailDeliveryPort, ExistingEmailDeliveryPort>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddSendGridMail(new SendGridMailOptions { Enabled = false }));

        Assert.AreEqual("An email delivery provider is already registered.", exception.Message);
    }

    private sealed class ExistingEmailDeliveryPort : IEmailDeliveryPort
    {
        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

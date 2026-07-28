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
        ServiceCollection services = new();
        SendGridMailOptions options = new()
        {
            Enabled = false,
            ApiKey = "api-key"
        };

        services.AddSendGridMail(options);

        using ServiceProvider provider = services.BuildServiceProvider();
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
        ServiceCollection services = new();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:SENDGRID_API_KEY"] = "api-key"
            })
            .Build();

        services.AddSendGridMail(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.AreEqual("api-key", provider.GetRequiredService<SendGridMailOptions>().ApiKey);
        Assert.IsInstanceOfType<SendGridEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
    }

    [TestMethod]
    public void AddSendGridMail_RejectsAnExistingEmailDeliveryProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEmailDeliveryPort, ExistingEmailDeliveryPort>();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddSendGridMail(new SendGridMailOptions { Enabled = false }));

        Assert.AreEqual("An email delivery provider is already registered.", exception.Message);
    }

    private sealed class ExistingEmailDeliveryPort : IEmailDeliveryPort
    {
        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

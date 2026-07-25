using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Communication.Abstractions;
using Rvt.Communication.MicrosoftGraphMail;

namespace Rvt.Communication.MicrosoftGraphMailTests;

[TestClass]
public sealed class MicrosoftGraphMailRegistrationTests
{
    [TestMethod]
    public void AddMicrosoftGraphMail_RegistersOneGraphPortTokenProviderOptionsAndValidationService()
    {
        var services = new ServiceCollection();
        var options = new MicrosoftGraphMailOptions { Enabled = false };

        services.AddMicrosoftGraphMail(options);

        using var provider = services.BuildServiceProvider();
        Assert.IsInstanceOfType<MicrosoftGraphEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
        Assert.IsInstanceOfType<AzureIdentityGraphAccessTokenProvider>(
            provider.GetRequiredService<IMicrosoftGraphAccessTokenProvider>());
        Assert.AreSame(options, provider.GetRequiredService<MicrosoftGraphMailOptions>());
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IEmailDeliveryPort)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IMicrosoftGraphAccessTokenProvider)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(MicrosoftGraphMailOptions)));
        Assert.AreEqual(1, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void AddMicrosoftGraphMail_LoadsProviderOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:MICROSOFT_TENANT_ID"] = "tenant-id",
                ["RVT:MICROSOFT_CLIENT_ID"] = "client-id",
                ["RVT:MICROSOFT_CLIENT_SECRET"] = "client-secret",
                ["RVT:MICROSOFT_SENDER_ADDRESS"] = "sender@example.test"
            })
            .Build();

        services.AddMicrosoftGraphMail(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.AreEqual("tenant-id", provider.GetRequiredService<MicrosoftGraphMailOptions>().TenantId);
        Assert.IsInstanceOfType<MicrosoftGraphEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
    }

    [TestMethod]
    public void AddMicrosoftGraphMail_RejectsAnExistingEmailDeliveryProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmailDeliveryPort, ExistingEmailDeliveryPort>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddMicrosoftGraphMail(new MicrosoftGraphMailOptions { Enabled = false }));

        Assert.AreEqual("An email delivery provider is already registered.", exception.Message);
    }

    [TestMethod]
    public async Task AddMicrosoftGraphMail_SingletonPortUsesFactoryManagedClientPerDelivery()
    {
        var services = new ServiceCollection();
        var clientFactory = new RecordingHttpClientFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted));
        services.AddMicrosoftGraphMail(new MicrosoftGraphMailOptions
        {
            Enabled = true,
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            SenderAddress = "sender@example.test"
        });
        services.AddSingleton<IHttpClientFactory>(clientFactory);
        services.AddSingleton<IMicrosoftGraphAccessTokenProvider>(
            new StaticTokenProvider());

        using var provider = services.BuildServiceProvider();
        var port = provider.GetRequiredService<IEmailDeliveryPort>();
        var request = new EmailDeliveryRequest(
            "ops@example.test", "subject", "plain", "<p>html</p>", []);

        await port.SendAsync(request);
        await port.SendAsync(request);

        Assert.AreSame(port, provider.GetRequiredService<IEmailDeliveryPort>());
        CollectionAssert.AreEqual(new[] { 1, 2 }, clientFactory.RequestClientIds);
    }

    private sealed class ExistingEmailDeliveryPort : IEmailDeliveryPort
    {
        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StaticTokenProvider : IMicrosoftGraphAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult("token");
    }

    private sealed class RecordingHttpClientFactory(
        Func<int, HttpResponseMessage> responseFactory) : IHttpClientFactory
    {
        private int clientId;

        internal List<int> RequestClientIds { get; } = [];

        public HttpClient CreateClient(string name)
        {
            var currentClientId = ++clientId;
            return new HttpClient(new RecordingHandler(
                currentClientId,
                RequestClientIds,
                responseFactory));
        }

        private sealed class RecordingHandler(
            int clientId,
            List<int> requestClientIds,
            Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                requestClientIds.Add(clientId);
                return Task.FromResult(responseFactory(clientId));
            }
        }
    }
}

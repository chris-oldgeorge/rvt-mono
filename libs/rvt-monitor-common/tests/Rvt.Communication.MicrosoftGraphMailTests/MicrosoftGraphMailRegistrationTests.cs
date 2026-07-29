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
        ServiceCollection services = new();
        MicrosoftGraphMailOptions options = new() { Enabled = false };

        services.AddMicrosoftGraphMail(options);

        using ServiceProvider provider = services.BuildServiceProvider();
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
        ServiceCollection services = new();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RVT:MICROSOFT_TENANT_ID"] = "tenant-id",
                ["RVT:MICROSOFT_CLIENT_ID"] = "client-id",
                ["RVT:MICROSOFT_CLIENT_SECRET"] = "client-secret",
                ["RVT:MICROSOFT_SENDER_ADDRESS"] = "sender@example.test"
            })
            .Build();

        services.AddMicrosoftGraphMail(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.AreEqual("tenant-id", provider.GetRequiredService<MicrosoftGraphMailOptions>().TenantId);
        Assert.IsInstanceOfType<MicrosoftGraphEmailAdapter>(provider.GetRequiredService<IEmailDeliveryPort>());
    }

    [TestMethod]
    public void AddMicrosoftGraphMail_RejectsAnExistingEmailDeliveryProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEmailDeliveryPort, ExistingEmailDeliveryPort>();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddMicrosoftGraphMail(new MicrosoftGraphMailOptions { Enabled = false }));

        Assert.AreEqual("An email delivery provider is already registered.", exception.Message);
    }

    [TestMethod]
    public async Task AddMicrosoftGraphMail_SingletonPortUsesFactoryManagedClientPerDelivery()
    {
        ServiceCollection services = new();
        RecordingHttpClientFactory clientFactory = new(_ =>
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

        using ServiceProvider provider = services.BuildServiceProvider();
        IEmailDeliveryPort port = provider.GetRequiredService<IEmailDeliveryPort>();
        EmailDeliveryRequest request = new(
            "ops@example.test", "subject", "plain", "<p>html</p>", []);

        await port.SendAsync(request, TestContext.CancellationToken);
        await port.SendAsync(request, TestContext.CancellationToken);

        Assert.AreSame(port, provider.GetRequiredService<IEmailDeliveryPort>());
        CollectionAssert.AreEqual(_expected, clientFactory.RequestClientIds);
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
        private int _clientId;

        internal List<int> RequestClientIds { get; } = [];

        public HttpClient CreateClient(string name)
        {
            int currentClientId = ++_clientId;
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

    public TestContext TestContext { get; set; } = null!;

    private static readonly int[] _expected = [1, 2];
}

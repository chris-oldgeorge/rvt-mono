using Microsoft.Extensions.Hosting;

namespace Rvt.Storage.Local;

public sealed class LocalStorageStartupValidationHostedService : IHostedService
{
    private readonly IObjectStorageClientFactory factory;
    private readonly string resourceName;

    public LocalStorageStartupValidationHostedService(
        IObjectStorageClientFactory factory,
        string resourceName)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException("Object storage resource name cannot be blank.", nameof(resourceName));
        }

        this.resourceName = resourceName;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        factory.GetRequiredClient(resourceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

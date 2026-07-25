using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Rvt.Storage.AzureBlob;

public static class AzureBlobStorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtAzureBlobStorage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, AzureBlobStorageOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException(
                "Object storage resource name cannot be blank.",
                nameof(resourceName));
        }

        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.TryAddSingleton<IObjectStorageClientFactory, ObjectStorageClientFactory>();
        services.AddKeyedSingleton<AzureBlobObjectStorageClient>(
            resourceName,
            (provider, _) => new AzureBlobObjectStorageClient(
                resourceName,
                optionsFactory(provider.GetRequiredService<IConfiguration>())));
        services.AddSingleton(provider => new ObjectStorageClientRegistration(
            resourceName,
            provider.GetRequiredKeyedService<AzureBlobObjectStorageClient>(resourceName)));
        services.AddSingleton<IHostedService>(provider =>
            new AzureBlobStorageStartupValidationHostedService(
                provider.GetRequiredService<IObjectStorageClientFactory>(),
                resourceName));
        return services;
    }

    public static IServiceCollection AddRvtAzureBlobStorage(
        this IServiceCollection services,
        string resourceName,
        AzureBlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddRvtAzureBlobStorage(resourceName, _ => options);
    }
}

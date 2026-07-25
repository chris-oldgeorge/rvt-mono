using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Rvt.Storage.S3;

public static class S3StorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtS3Storage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, S3StorageOptions> optionsFactory)
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
        services.AddKeyedSingleton<S3ObjectStorageClient>(
            resourceName,
            (provider, _) => new S3ObjectStorageClient(
                resourceName,
                optionsFactory(provider.GetRequiredService<IConfiguration>())));
        services.AddSingleton(provider => new ObjectStorageClientRegistration(
            resourceName,
            provider.GetRequiredKeyedService<S3ObjectStorageClient>(resourceName)));
        services.AddSingleton<IHostedService>(provider =>
            new S3StorageStartupValidationHostedService(
                provider.GetRequiredService<IObjectStorageClientFactory>(),
                resourceName));
        return services;
    }

    public static IServiceCollection AddRvtS3Storage(
        this IServiceCollection services,
        string resourceName,
        S3StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddRvtS3Storage(resourceName, _ => options);
    }
}

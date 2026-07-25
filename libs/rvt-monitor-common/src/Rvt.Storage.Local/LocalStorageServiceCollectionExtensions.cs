using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Rvt.Storage.Local;

public static class LocalStorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName) =>
        services.AddRvtLocalStorage(
            resourceName,
            configuration => LocalStorageOptions.Bind(configuration));

    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, LocalStorageOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException("Object storage resource name cannot be blank.", nameof(resourceName));
        }

        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.TryAddSingleton<IObjectStorageClientFactory, ObjectStorageClientFactory>();
        services.AddKeyedSingleton<LocalObjectStorageClient>(
            resourceName,
            (provider, _) => new LocalObjectStorageClient(
                resourceName,
                optionsFactory(provider.GetRequiredService<IConfiguration>())));
        services.AddSingleton(provider => new ObjectStorageClientRegistration(
            resourceName,
            provider.GetRequiredKeyedService<LocalObjectStorageClient>(resourceName)));
        services.AddSingleton<IHostedService>(provider =>
            new LocalStorageStartupValidationHostedService(
                provider.GetRequiredService<IObjectStorageClientFactory>(),
                resourceName));
        return services;
    }

    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName,
        LocalStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddRvtLocalStorage(resourceName, _ => options);
    }
}

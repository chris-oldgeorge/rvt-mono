using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rvt.Monitor.Common.Storage;

public static class BlobStorageServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorBlobStorage(this IServiceCollection services) =>
        services.AddMonitorBlobStorage(static configuration => BlobStorageOptions.Bind(configuration));

    public static IServiceCollection AddMonitorBlobStorage(
        this IServiceCollection services,
        Func<IConfiguration, BlobStorageOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.AddSingleton(provider =>
            optionsFactory(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IBlobStorageService>(CreateStorageService);
        services.AddSingleton<IHostedService, BlobStorageStartupValidationHostedService>();

        return services;
    }

    private static IBlobStorageService CreateStorageService(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<BlobStorageOptions>();
        return options.Provider switch
        {
            BlobStorageProvider.Local => new LocalFileBlobStorageService(options),
            BlobStorageProvider.AzureBlob => new AzureBlobStorageService(options),
            BlobStorageProvider.S3 => new S3BlobStorageService(options),
            _ => throw new InvalidOperationException(
                $"Unsupported blob storage provider '{options.Provider}'.")
        };
    }
}

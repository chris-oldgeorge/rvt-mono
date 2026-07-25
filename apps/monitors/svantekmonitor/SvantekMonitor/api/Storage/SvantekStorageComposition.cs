using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rvt.Storage.AzureBlob;
using Rvt.Storage.Local;
using Rvt.Storage.S3;

namespace Svantek.Api.Storage;

internal static class SvantekStorageComposition
{
    internal const string SoundRecordingsResource = "svantek-sound-recordings";

    internal static IServiceCollection AddSvantekStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredProvider = new[]
        {
            configuration["BlobStorage:Provider"],
            configuration["RVT:BLOB_PROVIDER"],
            configuration["RVT__BLOB_PROVIDER"],
            "Local",
        }.First(value => !string.IsNullOrWhiteSpace(value))!;

        if (string.Equals(configuredProvider.Trim(), "Local", StringComparison.OrdinalIgnoreCase))
        {
            return services.AddRvtLocalStorage(SoundRecordingsResource);
        }

        if (string.Equals(configuredProvider.Trim(), "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return services.AddRvtAzureBlobStorage(
                SoundRecordingsResource,
                providerConfiguration => AzureBlobStorageOptions.Bind(providerConfiguration));
        }

        if (string.Equals(configuredProvider.Trim(), "S3", StringComparison.OrdinalIgnoreCase))
        {
            return services.AddRvtS3Storage(
                SoundRecordingsResource,
                providerConfiguration => S3StorageOptions.Bind(providerConfiguration));
        }

        throw new InvalidOperationException(
            $"Unsupported blob storage provider '{configuredProvider}'. Allowed values are 'Local', 'AzureBlob', and 'S3'.");
    }
}

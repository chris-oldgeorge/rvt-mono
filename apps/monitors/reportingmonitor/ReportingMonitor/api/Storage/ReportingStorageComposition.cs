using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rvt.Reporting.Storage;
using Rvt.Storage.AzureBlob;
using Rvt.Storage.Local;
using Rvt.Storage.S3;

namespace ReportingMonitor.Api.Storage;

internal static class ReportingStorageComposition
{
    internal static IServiceCollection AddReportingStorage(
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
            services.AddRvtLocalStorage(
                ReportingStorageResourceNames.Reports,
                providerConfiguration => LocalStorageOptions.Bind(
                    providerConfiguration,
                    defaultContainer: "pdfreports",
                    defaultPrefix: "rvtreports",
                    legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME"));
            services.AddSingleton<IReportObjectUriResolver>(provider =>
                new ConfiguredReportObjectUriResolver(
                    provider
                        .GetRequiredKeyedService<LocalObjectStorageClient>(
                            ReportingStorageResourceNames.Reports)
                        .GetObjectUri));
            return services;
        }

        if (string.Equals(configuredProvider.Trim(), "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddRvtAzureBlobStorage(
                ReportingStorageResourceNames.Reports,
                providerConfiguration => AzureBlobStorageOptions.Bind(
                    providerConfiguration,
                    defaultContainer: "pdfreports",
                    defaultPrefix: "rvtreports",
                    legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME"));
            services.AddSingleton<IReportObjectUriResolver>(provider =>
                new ConfiguredReportObjectUriResolver(
                    provider
                        .GetRequiredKeyedService<AzureBlobObjectStorageClient>(
                            ReportingStorageResourceNames.Reports)
                        .GetObjectUri));
            return services;
        }

        if (string.Equals(configuredProvider.Trim(), "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddRvtS3Storage(
                ReportingStorageResourceNames.Reports,
                providerConfiguration => S3StorageOptions.Bind(
                    providerConfiguration,
                    defaultPrefix: "rvtreports"));
            services.AddSingleton<IReportObjectUriResolver>(provider =>
                new ConfiguredReportObjectUriResolver(
                    provider
                        .GetRequiredKeyedService<S3ObjectStorageClient>(
                            ReportingStorageResourceNames.Reports)
                        .GetObjectUri));
            return services;
        }

        throw new InvalidOperationException(
            $"Unsupported blob storage provider '{configuredProvider}'. Allowed values are 'Local', 'AzureBlob', and 'S3'.");
    }
}

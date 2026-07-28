using Microsoft.Extensions.Configuration;

namespace Rvt.Storage.AzureBlob;

public sealed record AzureBlobStorageOptions
{
    public string Container { get; init; } = "audiofiles";

    public string Prefix { get; init; } = string.Empty;

    public string ConnectionString { get; init; } = string.Empty;

    public string ServiceUri { get; init; } = string.Empty;

    public static AzureBlobStorageOptions Bind(
        IConfiguration configuration,
        string defaultContainer = "audiofiles",
        string defaultPrefix = "",
        string? legacyContainerEnvironmentKey = "AUDIO_FOLDER")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AzureBlobStorageOptions
        {
            Container = FirstConfigured(
                configuration,
                "BlobStorage:Container",
                "BLOB_CONTAINER",
                legacyContainerEnvironmentKey,
                defaultValue: defaultContainer),
            Prefix = FirstConfigured(
                configuration,
                "BlobStorage:Prefix",
                "BLOB_PREFIX",
                defaultValue: defaultPrefix),
            ConnectionString = FirstConfigured(
                configuration,
                "BlobStorage:AzureConnectionString",
                "BLOB_CONNECTION_STRING"),
            ServiceUri = FirstConfigured(
                configuration,
                "BlobStorage:AzureServiceUri",
                "BLOB_SERVICE_URI"),
        };
    }

    private static string FirstConfigured(
        IConfiguration configuration,
        string providerNeutralKey,
        string environmentKey,
        string? legacyEnvironmentKey = null,
        string defaultValue = "")
    {
        string?[] values = new[]
        {
            configuration[providerNeutralKey],
            configuration[$"RVT:{environmentKey}"],
            configuration[$"RVT__{environmentKey}"],
            legacyEnvironmentKey is null ? null : configuration[$"RVT:{legacyEnvironmentKey}"],
            legacyEnvironmentKey is null ? null : configuration[$"RVT__{legacyEnvironmentKey}"],
            defaultValue,
        };

        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}

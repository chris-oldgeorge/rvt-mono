using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Storage;

public sealed record BlobStorageOptions
{
    public BlobStorageProvider Provider { get; init; } = BlobStorageProvider.Local;
    public string Container { get; init; } = "audiofiles";
    public string Prefix { get; init; } = string.Empty;
    public string LocalRoot { get; init; } = "/data/rvt/blobs";
    public string AzureConnectionString { get; init; } = string.Empty;
    public string AzureServiceUri { get; init; } = string.Empty;
    public string S3Bucket { get; init; } = string.Empty;
    public string S3Region { get; init; } = string.Empty;
    public string S3ServiceUrl { get; init; } = string.Empty;
    public bool S3ForcePathStyle { get; init; }

    public static BlobStorageOptions Bind(
        IConfiguration configuration,
        string defaultContainer = "audiofiles",
        string defaultPrefix = "",
        string? legacyContainerEnvironmentKey = "AUDIO_FOLDER")
    {
        var providerText = FirstConfigured(configuration, "BlobStorage:Provider", "BLOB_PROVIDER");
        var provider = ResolveProvider(providerText);

        return new BlobStorageOptions
        {
            Provider = provider,
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
            LocalRoot = FirstConfigured(
                configuration,
                "BlobStorage:LocalRoot",
                "BLOB_LOCAL_ROOT",
                defaultValue: "/data/rvt/blobs"),
            AzureConnectionString = FirstConfigured(
                configuration,
                "BlobStorage:AzureConnectionString",
                "BLOB_CONNECTION_STRING"),
            AzureServiceUri = FirstConfigured(configuration, "BlobStorage:AzureServiceUri", "BLOB_SERVICE_URI"),
            S3Bucket = FirstConfigured(configuration, "BlobStorage:S3Bucket", "S3_BUCKET"),
            S3Region = FirstConfigured(configuration, "BlobStorage:S3Region", "S3_REGION"),
            S3ServiceUrl = FirstConfigured(configuration, "BlobStorage:S3ServiceUrl", "S3_SERVICE_URL"),
            S3ForcePathStyle = GetBool(configuration, "BlobStorage:S3ForcePathStyle", "S3_FORCE_PATH_STYLE")
        };
    }

    private static BlobStorageProvider ResolveProvider(string? providerText)
    {
        if (string.IsNullOrWhiteSpace(providerText))
        {
            return BlobStorageProvider.Local;
        }

        if (Enum.TryParse<BlobStorageProvider>(providerText, ignoreCase: true, out var provider)
            && Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"Unsupported blob storage provider '{providerText}'. Allowed values are 'Local', 'AzureBlob', and 'S3'.");
    }

    private static bool GetBool(IConfiguration configuration, string providerNeutralKey, string environmentKey)
    {
        return bool.TryParse(FirstConfigured(configuration, providerNeutralKey, environmentKey), out var value)
            && value;
    }

    private static string FirstConfigured(
        IConfiguration configuration,
        string providerNeutralKey,
        string environmentKey,
        string? legacyEnvironmentKey = null,
        string defaultValue = "")
    {
        var values = new[]
        {
            configuration[providerNeutralKey],
            configuration[$"RVT:{environmentKey}"],
            configuration[$"RVT__{environmentKey}"],
            legacyEnvironmentKey is null ? null : configuration[$"RVT:{legacyEnvironmentKey}"],
            legacyEnvironmentKey is null ? null : configuration[$"RVT__{legacyEnvironmentKey}"],
            defaultValue
        };

        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}

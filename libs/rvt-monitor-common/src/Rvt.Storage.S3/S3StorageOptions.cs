using Microsoft.Extensions.Configuration;

namespace Rvt.Storage.S3;

public sealed record S3StorageOptions
{
    public string Bucket { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string ServiceUrl { get; init; } = string.Empty;

    public bool ForcePathStyle { get; init; }

    public static S3StorageOptions Bind(
        IConfiguration configuration,
        string defaultPrefix = "")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new S3StorageOptions
        {
            Bucket = FirstConfigured(
                configuration,
                "BlobStorage:S3Bucket",
                "S3_BUCKET"),
            Prefix = FirstConfigured(
                configuration,
                "BlobStorage:Prefix",
                "BLOB_PREFIX",
                defaultValue: defaultPrefix),
            Region = FirstConfigured(
                configuration,
                "BlobStorage:S3Region",
                "S3_REGION"),
            ServiceUrl = FirstConfigured(
                configuration,
                "BlobStorage:S3ServiceUrl",
                "S3_SERVICE_URL"),
            ForcePathStyle = bool.TryParse(
                FirstConfigured(
                    configuration,
                    "BlobStorage:S3ForcePathStyle",
                    "S3_FORCE_PATH_STYLE"),
                out bool forcePathStyle)
                && forcePathStyle,
        };
    }

    private static string FirstConfigured(
        IConfiguration configuration,
        string providerNeutralKey,
        string environmentKey,
        string defaultValue = "")
    {
        string?[] values =
        [
            configuration[providerNeutralKey],
            configuration[$"RVT:{environmentKey}"],
            configuration[$"RVT__{environmentKey}"],
            defaultValue,
        ];

        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }
}

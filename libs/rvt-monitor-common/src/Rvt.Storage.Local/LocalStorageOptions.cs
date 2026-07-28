using Microsoft.Extensions.Configuration;

namespace Rvt.Storage.Local;

public sealed record LocalStorageOptions
{
    public string RootPath { get; init; } = "/data/rvt/blobs";

    public string Container { get; init; } = "audiofiles";

    public string Prefix { get; init; } = string.Empty;

    public static LocalStorageOptions Bind(
        IConfiguration configuration,
        string defaultContainer = "audiofiles",
        string defaultPrefix = "",
        string? legacyContainerEnvironmentKey = "AUDIO_FOLDER")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new LocalStorageOptions
        {
            RootPath = FirstConfigured(
                configuration,
                "BlobStorage:LocalRoot",
                "BLOB_LOCAL_ROOT",
                defaultValue: "/data/rvt/blobs"),
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

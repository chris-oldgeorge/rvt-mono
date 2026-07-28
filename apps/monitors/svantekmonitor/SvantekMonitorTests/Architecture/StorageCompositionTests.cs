using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rvt.Storage;
using Rvt.Storage.AzureBlob;
using Rvt.Storage.Local;
using Rvt.Storage.S3;
using Svantek.Api;
using Svantek.Api.Storage;

namespace SvantekMonitorTests.Architecture;

[TestClass]
public sealed class StorageCompositionTests
{
    [TestMethod]
    public void AddSvantekStorage_MissingProvider_DefaultsToLocalWithAudioFolderDefaults()
    {
        using ServiceProvider provider = CreateProvider();

        IObjectStorageClient client = provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient(SvantekStorageComposition.SoundRecordingsResource);
        LocalObjectStorageClient local = Assert.IsInstanceOfType<LocalObjectStorageClient>(client);

        Assert.AreEqual(
            Path.GetFullPath("/data/rvt/blobs/audiofiles/probe.wav"),
            local.GetObjectUri(StorageObjectKey.Parse("probe.wav")).LocalPath);
    }

    [TestMethod]
    public void AddSvantekStorage_ProviderNeutralKey_SelectsLocal()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["BlobStorage:Provider"] = "Local",
        });

        Assert.IsInstanceOfType<LocalObjectStorageClient>(GetSoundRecordingsClient(provider));
    }

    [TestMethod]
    public void AddSvantekStorage_RvtKey_SelectsAzureBlob()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT:BLOB_PROVIDER"] = "AzureBlob",
            ["BlobStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
        });

        Assert.IsInstanceOfType<AzureBlobObjectStorageClient>(GetSoundRecordingsClient(provider));
    }

    [TestMethod]
    public void AddSvantekStorage_LiteralRvtKey_SelectsS3()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["RVT__BLOB_PROVIDER"] = "S3",
            ["BlobStorage:S3Bucket"] = "sound-recordings",
            ["BlobStorage:S3Region"] = "us-east-1",
        });

        Assert.IsInstanceOfType<S3ObjectStorageClient>(GetSoundRecordingsClient(provider));
    }

    [TestMethod]
    public void AddSvantekStorage_ProviderNeutralKey_HasPrecedenceOverRvtAliases()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["BlobStorage:Provider"] = "Local",
            ["RVT:BLOB_PROVIDER"] = "AzureBlob",
            ["RVT__BLOB_PROVIDER"] = "S3",
        });

        Assert.IsInstanceOfType<LocalObjectStorageClient>(GetSoundRecordingsClient(provider));
    }

    [TestMethod]
    public void AddSvantekStorage_BlankHigherPriorityKey_UsesNextConfiguredAlias()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["BlobStorage:Provider"] = " ",
            ["RVT:BLOB_PROVIDER"] = "AzureBlob",
            ["RVT__BLOB_PROVIDER"] = "S3",
            ["BlobStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
        });

        Assert.IsInstanceOfType<AzureBlobObjectStorageClient>(GetSoundRecordingsClient(provider));
    }

    [TestMethod]
    public void AddSvantekStorage_UnsupportedProvider_ThrowsExactSafeMessage()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BlobStorage:Provider"] = "GoogleCloud",
        });
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => services.AddSvantekStorage(configuration));

        Assert.AreEqual(
            "Unsupported blob storage provider 'GoogleCloud'. Allowed values are 'Local', 'AzureBlob', and 'S3'.",
            exception.Message);
    }

    private static ServiceProvider CreateProvider(
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        IConfiguration configuration = BuildConfiguration(settings);
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSvantekMonitor(configuration);
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?>? settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

    private static IObjectStorageClient GetSoundRecordingsClient(IServiceProvider provider) =>
        provider
            .GetRequiredService<IObjectStorageClientFactory>()
            .GetRequiredClient(SvantekStorageComposition.SoundRecordingsResource);
}

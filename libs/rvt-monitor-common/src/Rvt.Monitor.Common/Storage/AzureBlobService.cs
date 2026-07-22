using Rvt.Monitor.Common.Configuration;

namespace Rvt.Monitor.Common.Storage;

[Obsolete("Use Rvt.Monitor.Common.Storage.IBlobStorageService instead.")]
public sealed class AzureBlobService
{
    private readonly AzureBlobStorageService adapter;

    public AzureBlobService()
    {
        adapter = new AzureBlobStorageService(new BlobStorageOptions
        {
            Provider = BlobStorageProvider.AzureBlob,
            Container = RvtConfig.AudioFolder,
            AzureConnectionString = RvtConfig.BlobConnectionString,
            AzureServiceUri = RvtConfig.BlobServiceUri
        });
    }

    public async Task<string> UploadBytes(byte[] fileBytes, string blobName)
    {
        var result = await adapter.WriteAsync(new BlobStorageWriteRequest(blobName, fileBytes));
        return result.ObjectName;
    }

    public Task Delete(string blobName)
    {
        return adapter.DeleteAsync(blobName);
    }
}

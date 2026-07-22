namespace Rvt.Monitor.Common.Storage;

public interface IBlobStorageService
{
    Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken = default);
}

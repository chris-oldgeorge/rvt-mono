namespace Rvt.Storage;

public interface IObjectStorageClientFactory
{
    IObjectStorageClient GetRequiredClient(string resourceName);
}

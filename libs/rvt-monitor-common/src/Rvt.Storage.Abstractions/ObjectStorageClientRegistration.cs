namespace Rvt.Storage;

public sealed record ObjectStorageClientRegistration(
    string ResourceName,
    IObjectStorageClient Client);

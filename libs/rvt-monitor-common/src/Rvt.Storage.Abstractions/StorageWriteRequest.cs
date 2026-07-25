namespace Rvt.Storage;

public sealed record StorageWriteRequest(
    StorageObjectKey Key,
    Stream Content,
    string? ContentType = null);

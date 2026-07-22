namespace Rvt.Monitor.Common.Storage;

public sealed record BlobStorageWriteRequest(
    string ObjectName,
    byte[] Content,
    string? ContentType = null);

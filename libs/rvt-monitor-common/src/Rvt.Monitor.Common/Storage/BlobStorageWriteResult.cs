namespace Rvt.Monitor.Common.Storage;

public sealed record BlobStorageWriteResult(
    string ObjectName,
    string? Uri = null);

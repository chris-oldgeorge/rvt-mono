namespace Rvt.Storage;

/// <summary>
/// Provider-neutral object storage port.
/// </summary>
/// <remarks>
/// Every adapter reports operational failures as
/// <see cref="ObjectStorageException"/> carrying a <see cref="StorageFailureKind"/>,
/// so callers can classify a failure without knowing which provider produced
/// it. Argument validation still surfaces as <see cref="ArgumentException"/>,
/// and caller cancellation still surfaces as
/// <see cref="OperationCanceledException"/>; neither is a storage fault.
/// </remarks>
public interface IObjectStorageClient
{
    Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the absolute provider URI for a stored object.
    /// </summary>
    /// <remarks>
    /// Every adapter already implemented this identically; consumers had to
    /// bind to the concrete adapter type to reach it, which defeated the port.
    /// Schemes stay provider-specific by design — local <c>file:</c>, Azure
    /// HTTPS, S3 <c>s3:</c> — because persisted links carry those forms.
    /// </remarks>
    Uri GetObjectUri(StorageObjectKey key);
}

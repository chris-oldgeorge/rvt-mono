namespace Rvt.Storage;

public sealed class ObjectStorageException(
    StorageFailureKind kind,
    string resourceName,
    StorageObjectKey? key,
    Exception? innerException = null) : Exception(BuildMessage(kind, resourceName, key), innerException)
{
    public StorageFailureKind Kind { get; } = kind;

    public string ResourceName { get; } = resourceName;

    public StorageObjectKey? Key { get; } = key;

    private static string BuildMessage(
        StorageFailureKind kind,
        string resourceName,
        StorageObjectKey? key)
    {
        string keyDescription = key is null ? string.Empty : $" and key '{key}'";
        return $"Object storage operation failed for resource '{resourceName}'{keyDescription} ({kind}).";
    }
}

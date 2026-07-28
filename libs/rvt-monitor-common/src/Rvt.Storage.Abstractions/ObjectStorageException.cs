namespace Rvt.Storage;

public sealed class ObjectStorageException : Exception
{
    public ObjectStorageException(
        StorageFailureKind kind,
        string resourceName,
        StorageObjectKey? key,
        Exception? innerException = null)
        : base(BuildMessage(kind, resourceName, key), innerException)
    {
        Kind = kind;
        ResourceName = resourceName;
        Key = key;
    }

    public StorageFailureKind Kind { get; }

    public string ResourceName { get; }

    public StorageObjectKey? Key { get; }

    private static string BuildMessage(
        StorageFailureKind kind,
        string resourceName,
        StorageObjectKey? key)
    {
        string keyDescription = key is null ? string.Empty : $" and key '{key}'";
        return $"Object storage operation failed for resource '{resourceName}'{keyDescription} ({kind}).";
    }
}

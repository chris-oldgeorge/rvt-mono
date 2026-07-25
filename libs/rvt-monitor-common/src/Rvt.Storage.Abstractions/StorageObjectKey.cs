namespace Rvt.Storage;

public sealed record StorageObjectKey
{
    private StorageObjectKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static StorageObjectKey Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        if (trimmed.Length == 0
            || trimmed.StartsWith('/')
            || trimmed.StartsWith('\\')
            || IsWindowsDriveRooted(trimmed))
        {
            throw new ArgumentException("Object storage key must be a safe relative object name.", nameof(value));
        }

        var segments = trimmed
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Object storage key must be a safe relative object name.", nameof(value));
        }

        return new StorageObjectKey(string.Join('/', segments));
    }

    public override string ToString() => Value;

    private static bool IsWindowsDriveRooted(string value)
    {
        return value.Length >= 3
            && char.IsLetter(value[0])
            && value[1] == ':'
            && value[2] is '/' or '\\';
    }
}

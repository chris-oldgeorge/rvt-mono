namespace Rvt.Monitor.Common.Storage;

public static class BlobObjectName
{
    public static string Normalize(string objectName)
    {
        var normalized = objectName?.Trim().Replace('\\', '/') ?? string.Empty;

        if (string.IsNullOrEmpty(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized)
            || IsWindowsDriveRooted(normalized))
        {
            throw new ArgumentException("The blob object name must be a non-rooted path.", nameof(objectName));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new ArgumentException("The blob object name cannot contain traversal segments.", nameof(objectName));
        }

        return string.Join('/', segments);
    }

    private static bool IsWindowsDriveRooted(string normalized)
    {
        return normalized.Length >= 3
            && ((normalized[0] >= 'A' && normalized[0] <= 'Z')
                || (normalized[0] >= 'a' && normalized[0] <= 'z'))
            && normalized[1] == ':'
            && normalized[2] == '/';
    }
}

using System.Text;

namespace Rvt.Storage.Local;

public sealed class LocalObjectStorageClient : IObjectStorageClient
{
    private const int FileBufferSize = 81920;

    private readonly string resourceName;
    private readonly LocalStorageOptions options;

    public LocalObjectStorageClient(
        string resourceName,
        LocalStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException("Object storage resource name cannot be blank.", nameof(resourceName));
        }

        ArgumentNullException.ThrowIfNull(options);
        this.resourceName = resourceName;
        this.options = options;
    }

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Key);
        ArgumentNullException.ThrowIfNull(request.Content);

        cancellationToken.ThrowIfCancellationRequested();
        var localRoot = GetLocalRootPath();
        var targetPath = GetTargetPath(localRoot, request.Key);
        var metadataPath = GetContentTypeMetadataPath(targetPath);
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException(
                $"The local object target directory for resource '{resourceName}' could not be determined.");

        EnsureNoExistingReparsePoints(localRoot, targetPath);
        EnsureNoExistingReparsePoints(localRoot, metadataPath);
        Directory.CreateDirectory(targetDirectory);
        EnsureNoExistingReparsePoints(localRoot, targetPath);
        EnsureNoExistingReparsePoints(localRoot, metadataPath);

        string? objectTemporaryPath = null;
        string? metadataTemporaryPath = null;
        try
        {
            objectTemporaryPath = await WriteTemporaryFileAsync(
                targetPath,
                request.Content,
                cancellationToken);

            if (request.ContentType is not null)
            {
                await using var metadataContent = new MemoryStream(
                    Encoding.UTF8.GetBytes(request.ContentType),
                    writable: false);
                metadataTemporaryPath = await WriteTemporaryFileAsync(
                    metadataPath,
                    metadataContent,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoExistingReparsePoints(localRoot, targetPath);
            EnsureNoExistingReparsePoints(localRoot, metadataPath);

            File.Move(objectTemporaryPath, targetPath, overwrite: true);
            objectTemporaryPath = null;

            if (metadataTemporaryPath is not null)
            {
                File.Move(metadataTemporaryPath, metadataPath, overwrite: true);
                metadataTemporaryPath = null;
            }
            else
            {
                File.Delete(metadataPath);
            }
        }
        finally
        {
            DeleteTemporaryFile(objectTemporaryPath);
            DeleteTemporaryFile(metadataTemporaryPath);
        }

        return new StorageWriteResult(request.Key);
    }

    public async Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var localRoot = GetLocalRootPath();
        var targetPath = GetTargetPath(localRoot, key);
        var metadataPath = GetContentTypeMetadataPath(targetPath);
        EnsureNoExistingReparsePoints(localRoot, targetPath);
        EnsureNoExistingReparsePoints(localRoot, metadataPath);

        if (!File.Exists(targetPath))
        {
            return null;
        }

        FileStream? content = null;
        try
        {
            content = new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var contentType = File.Exists(metadataPath)
                ? await File.ReadAllTextAsync(metadataPath, cancellationToken)
                : null;
            return new StorageReadResult(content, contentType, content.Length);
        }
        catch
        {
            if (content is not null)
            {
                await content.DisposeAsync();
            }

            throw;
        }
    }

    public Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var localRoot = GetLocalRootPath();
        var targetPath = GetTargetPath(localRoot, key);
        var metadataPath = GetContentTypeMetadataPath(targetPath);
        EnsureNoExistingReparsePoints(localRoot, targetPath);
        EnsureNoExistingReparsePoints(localRoot, metadataPath);

        var existed = File.Exists(targetPath);
        if (existed)
        {
            File.Delete(targetPath);
        }

        File.Delete(metadataPath);
        return Task.FromResult(existed);
    }

    public Uri GetObjectUri(StorageObjectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var targetPath = GetTargetPath(GetLocalRootPath(), key);
        return new Uri(targetPath);
    }

    private string GetLocalRootPath()
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new ArgumentException(
                "The local object storage root must be a non-empty path.",
                nameof(options));
        }

        return Path.GetFullPath(options.RootPath);
    }

    private string GetTargetPath(
        string localRoot,
        StorageObjectKey key)
    {
        var container = NormalizeConfiguredPath(
            options.Container,
            nameof(options.Container),
            required: true);
        var prefix = NormalizeConfiguredPath(
            options.Prefix,
            nameof(options.Prefix),
            required: false);
        var relativeObjectPath = key.Value.Replace('/', Path.DirectorySeparatorChar);
        var targetPath = Path.GetFullPath(
            Path.Combine(localRoot, container, prefix, relativeObjectPath));
        var relativeTargetPath = Path.GetRelativePath(localRoot, targetPath);

        if (Path.IsPathRooted(relativeTargetPath)
            || relativeTargetPath == ".."
            || relativeTargetPath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || relativeTargetPath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The local object storage target path must remain under RootPath.",
                nameof(options));
        }

        return targetPath;
    }

    private static string GetContentTypeMetadataPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException(
                "The local object content-type metadata directory could not be determined.");
        return Path.Combine(directory, $".{Path.GetFileName(targetPath)}.content-type");
    }

    private static async Task<string> WriteTemporaryFileAsync(
        string targetPath,
        Stream content,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException(
                "The local object temporary directory could not be determined.");
        var temporaryPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await content.CopyToAsync(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return temporaryPath;
        }
        catch
        {
            DeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    private static void DeleteTemporaryFile(string? temporaryPath)
    {
        if (temporaryPath is not null && File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }

    private static void EnsureNoExistingReparsePoints(
        string localRoot,
        string targetPath)
    {
        var relativeTargetPath = Path.GetRelativePath(localRoot, targetPath);
        var pathComponent = localRoot;

        foreach (var segment in relativeTargetPath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            pathComponent = Path.Combine(pathComponent, segment);

            try
            {
                if ((File.GetAttributes(pathComponent) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        "The local object storage target path cannot contain reparse points.");
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
        }
    }

    private static string NormalizeConfiguredPath(
        string? value,
        string parameterName,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ArgumentException(
                    "The local object storage container must be a non-empty path.",
                    parameterName);
            }

            return string.Empty;
        }

        try
        {
            return StorageObjectKey.Parse(value).Value;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "The configured local object storage path must be a safe relative path.",
                parameterName,
                exception);
        }
    }
}

namespace Rvt.Monitor.Common.Storage;

public sealed class LocalFileBlobStorageService : IBlobStorageService
{
    private readonly BlobStorageOptions options;

    public LocalFileBlobStorageService(BlobStorageOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var objectName = BlobObjectName.Normalize(request.ObjectName);
        var localRoot = GetLocalRootPath();
        var targetPath = GetTargetPath(localRoot, objectName);
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("The local blob target directory could not be determined.");

        cancellationToken.ThrowIfCancellationRequested();
        EnsureNoExistingReparsePoints(localRoot, targetPath);
        Directory.CreateDirectory(targetDirectory);
        EnsureNoExistingReparsePoints(localRoot, targetPath);

        var temporaryPath = Path.Combine(targetDirectory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                await stream.WriteAsync(request.Content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new BlobStorageWriteResult(objectName, new Uri(targetPath).AbsoluteUri);
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localRoot = GetLocalRootPath();
        var targetPath = GetTargetPath(localRoot, BlobObjectName.Normalize(objectName));
        EnsureNoExistingReparsePoints(localRoot, targetPath);
        File.Delete(targetPath);
        return Task.CompletedTask;
    }

    private string GetLocalRootPath()
    {
        return Path.GetFullPath(options.LocalRoot);
    }

    private string GetTargetPath(string localRoot, string objectName)
    {
        var container = NormalizeConfiguredPath(options.Container, nameof(options.Container), required: true);
        var prefix = NormalizeConfiguredPath(options.Prefix, nameof(options.Prefix), required: false);
        var relativeObjectPath = objectName.Replace('/', Path.DirectorySeparatorChar);
        var targetPath = Path.GetFullPath(Path.Combine(localRoot, container, prefix, relativeObjectPath));
        var relativeTargetPath = Path.GetRelativePath(localRoot, targetPath);

        if (Path.IsPathRooted(relativeTargetPath)
            || relativeTargetPath == ".."
            || relativeTargetPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativeTargetPath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("The local blob target path must remain under LocalRoot.", nameof(options));
        }

        return targetPath;
    }

    private static void EnsureNoExistingReparsePoints(string localRoot, string targetPath)
    {
        var relativeTargetPath = Path.GetRelativePath(localRoot, targetPath);
        var pathComponent = localRoot;

        foreach (var segment in relativeTargetPath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            pathComponent = Path.Combine(pathComponent, segment);

            try
            {
                if ((File.GetAttributes(pathComponent) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("The local blob target path cannot contain reparse points.");
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

    private static string NormalizeConfiguredPath(string? value, string parameterName, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ArgumentException("The blob container must be a non-empty path.", parameterName);
            }

            return string.Empty;
        }

        return BlobObjectName.Normalize(value);
    }
}

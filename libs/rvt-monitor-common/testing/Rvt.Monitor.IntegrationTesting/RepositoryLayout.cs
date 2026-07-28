using System.Runtime.CompilerServices;

namespace Rvt.Monitor.IntegrationTesting;

public static class RepositoryLayout
{
    private static readonly Lazy<string> _repositoryRoot = new(
        () => FindRepositoryRoot(AppContext.BaseDirectory, SourceFilePath()));

    public static string Root => _repositoryRoot.Value;

    public static string GetPath(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Path.Combine([Root, .. segments]);
    }

    internal static string FindRepositoryRoot(
        string outputDirectory,
        string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        string sourceDirectory = Path.GetDirectoryName(sourceFilePath) ??
            throw new ArgumentException(
                "The source file path must include a directory.",
                nameof(sourceFilePath));
        IEnumerable<string> startDirectories = new[] { outputDirectory, sourceDirectory }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal);

        foreach (string startDirectory in startDirectories)
        {
            DirectoryInfo? directory = new(startDirectory);
            while (directory is not null)
            {
                if (IsRepositoryRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find the RVT monorepository root from output '{outputDirectory}' " +
            $"or source '{sourceFilePath}'.");
    }

    private static bool IsRepositoryRoot(string path)
    {
        string gitPath = Path.Combine(path, ".git");
        return File.Exists(Path.Combine(path, "Rvt.Mono.slnx")) &&
            (Directory.Exists(gitPath) || File.Exists(gitPath));
    }

    private static string SourceFilePath(
        [CallerFilePath] string sourceFilePath = "") =>
        sourceFilePath;
}

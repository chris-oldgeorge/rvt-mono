using System.Runtime.CompilerServices;

namespace Rvt.Monitor.IntegrationTesting;

public static class RepositoryLayout
{
    private const string _repositoryRootEnvironmentVariable = "RVT_MONOREPO_ROOT";

    private static readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static readonly Lazy<string> _repositoryRoot = new(
        () => FindRepositoryRoot(
            AppContext.BaseDirectory,
            SourceFilePath(),
            Directory.GetCurrentDirectory(),
            Environment.GetEnvironmentVariable(_repositoryRootEnvironmentVariable)));

    public static string Root => _repositoryRoot.Value;

    public static string GetPath(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0)
        {
            throw new ArgumentException(
                "At least one repository-relative path segment is required.",
                nameof(segments));
        }

        foreach (string? segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) ||
                Path.IsPathRooted(segment) ||
                segment is "." or ".." ||
                segment.Contains('/') ||
                segment.Contains('\\'))
            {
                throw new ArgumentException(
                    $"Repository path segment '{segment}' must be a non-empty, " +
                    "non-rooted name without separators or traversal.",
                    nameof(segments));
            }
        }

        string repositoryRoot = Root;
        string combinedPath = Path.GetFullPath(
            Path.Combine([repositoryRoot, .. segments]));
        string rootPrefix = Path.EndsInDirectorySeparator(repositoryRoot)
            ? repositoryRoot
            : repositoryRoot + Path.DirectorySeparatorChar;
        if (!combinedPath.StartsWith(
                rootPrefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Repository path '{combinedPath}' must remain below root '{repositoryRoot}'.",
                nameof(segments));
        }

        return combinedPath;
    }

    internal static string FindRepositoryRoot(
        string outputDirectory,
        string sourceFilePath,
        string currentDirectory,
        string? configuredRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        string sourceDirectory = Path.GetDirectoryName(sourceFilePath) ??
            throw new ArgumentException(
                "The source file path must include a directory.",
                nameof(sourceFilePath));

        List<RepositoryCandidate> candidates = [];
        if (configuredRoot is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);
            string normalizedConfiguredRoot = Path.GetFullPath(configuredRoot);
            if (!IsRepositoryRoot(normalizedConfiguredRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Configured {_repositoryRootEnvironmentVariable} root " +
                    $"'{configuredRoot}' must contain Rvt.Mono.slnx and a .git file or directory.");
            }

            candidates.Add(new RepositoryCandidate(
                _repositoryRootEnvironmentVariable,
                CanonicalizeExistingDirectory(normalizedConfiguredRoot)));
        }

        AddCandidate(candidates, "output", outputDirectory);
        AddCandidate(candidates, "source", sourceDirectory);
        AddCandidate(candidates, "current directory", currentDirectory);

        Dictionary<string, List<string>> rootsByCanonicalPath = new(_pathComparer);
        foreach (RepositoryCandidate candidate in candidates)
        {
            if (!rootsByCanonicalPath.TryGetValue(candidate.Root, out List<string>? labels))
            {
                labels = [];
                rootsByCanonicalPath.Add(candidate.Root, labels);
            }

            labels.Add(candidate.Label);
        }

        if (rootsByCanonicalPath.Count == 1)
        {
            return rootsByCanonicalPath.Keys.Single();
        }

        if (rootsByCanonicalPath.Count > 1)
        {
            string resolvedCandidates = string.Join(
                "; ",
                rootsByCanonicalPath.Select(
                    pair => $"{string.Join(", ", pair.Value)} => '{pair.Key}'"));
            throw new InvalidOperationException(
                "Repository root discovery is ambiguous across distinct checkouts: " +
                resolvedCandidates);
        }

        throw new DirectoryNotFoundException(
            $"Could not find the RVT monorepository root from output '{outputDirectory}' " +
            $"source '{sourceFilePath}', or current directory '{currentDirectory}'. " +
            $"Set {_repositoryRootEnvironmentVariable} to the validated repository root " +
            "when the test host relocates both build output and its working directory.");
    }

    private static void AddCandidate(
        ICollection<RepositoryCandidate> candidates,
        string label,
        string startDirectory)
    {
        string? repositoryRoot = FindRepositoryRootFrom(startDirectory);
        if (repositoryRoot is not null)
        {
            candidates.Add(new RepositoryCandidate(label, repositoryRoot));
        }
    }

    private static string CanonicalizeExistingDirectory(string path)
    {
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string pathRoot = Path.GetPathRoot(fullPath) ??
            throw new ArgumentException(
                $"Path '{path}' must have a filesystem root.",
                nameof(path));
        string currentPath = pathRoot;
        string relativePath = Path.GetRelativePath(pathRoot, fullPath);

        foreach (string segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string nextPath = Path.Combine(currentPath, segment);
            DirectoryInfo directory = new(nextPath);
            FileSystemInfo? linkTarget = directory.ResolveLinkTarget(
                returnFinalTarget: true);
            currentPath = linkTarget is null
                ? nextPath
                : CanonicalizeExistingDirectory(linkTarget.FullName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath));
    }

    private static string? FindRepositoryRootFrom(string startDirectory)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (IsRepositoryRoot(directory.FullName))
            {
                return CanonicalizeExistingDirectory(directory.FullName);
            }

            directory = directory.Parent;
        }

        return null;
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

    private sealed record RepositoryCandidate(
        string Label,
        string Root);
}

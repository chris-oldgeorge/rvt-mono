// File summary: Resolves the root-native portal repository paths used by architecture and release tests.
// Major updates:
// - 2026-07-17 pending Added lazy root discovery for portable verification.

namespace RvtPortal.Spa.Tests.Support;

internal static class RepositoryLayout
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    public static string Root => RepositoryRoot.Value;
    public static string SolutionPath => Path.Combine(Root, "RvtPortal.Spa.sln");
    public static string ClientRoot => Path.Combine(Root, "RvtPortal.Client");
    public static string DataAccessRoot => Path.Combine(Root, "RVT.DataAccess");
    public static string SchemaDeployRoot => Path.Combine(Root, "RVT.SchemaDeploy");
    public static string DatabaseRoot => Path.Combine(Root, "database");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "RvtPortal.Spa.sln");
            var lockFile = Path.Combine(directory.FullName, "RvtPortal.Client", "package-lock.json");
            var nugetConfig = Path.Combine(directory.FullName, "NuGet.config");
            if (File.Exists(solution) && File.Exists(lockFile) && File.Exists(nugetConfig))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the refactor-native RVT portal repository root from the test output directory.");
    }
}

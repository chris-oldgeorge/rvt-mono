// File summary: Guards the rule that persistence adapters stage changes and never commit them themselves.
// Major updates:
// - 2026-07-14 pending Added guardrails after retiring the self-committing generic-repository write path.

using System.Reflection;
using RVT.DataAccess;
using RVT.Entities.Ports.Persistence;

namespace RvtPortal.Spa.Tests;

public sealed class DataAccessWriteBoundaryTests
{
    private static readonly string[] WriteMethodNames = ["AddAsync", "UpdateAsync", "DeleteAsync"];

    [Fact]
    // Function summary: Verifies no data-access type commits on its own, so writes stay inside one Unit of Work boundary.
    public void DataAccessSources_DoNotCallSaveChanges()
    {
        var dataAccessDirectory = Path.Combine(FindRepositoryRoot(), "RVT.DataAccess");

        var offenders = Directory
            .EnumerateFiles(dataAccessDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).StartsWith("._", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => CommitsChanges(File.ReadAllText(file)))
            .Select(file => Path.GetFileName(file))
            .ToArray();

        // Repositories used to call SaveChanges inside Add/Update/Delete, so a use case touching two of them
        // could half-commit. Persistence is now owned solely by EfCoreUnitOfWork / the transaction pipeline.
        Assert.Empty(offenders);
    }

    // Function summary: Reports whether a source file commits the DbContext (calls SaveChanges) itself.
    private static bool CommitsChanges(string source)
    {
        // A SaveChangesInterceptor hooks the save pipeline to inspect it; it does not commit. Ignore references
        // to that base type (e.g. UtcTimestampGuardInterceptor) so the guard flags only actual SaveChanges calls.
        return source.Replace("SaveChangesInterceptor", string.Empty, StringComparison.Ordinal)
            .Contains("SaveChanges", StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies the persistence ports expose reads only, keeping writes on the CQRS/DbContext path.
    public void PersistencePorts_DoNotExposeWriteOperations()
    {
        Type[] ports =
        [
            typeof(IMonitorRepository),
            typeof(ICompanyRepository),
            typeof(IAlertlevelRepository),
            typeof(IDeploymentRepository)
        ];

        var writeMembers = ports
            .SelectMany(port => port.GetMethods())
            .Where(method => WriteMethodNames.Contains(method.Name))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(writeMembers);
    }

    [Fact]
    // Function summary: Verifies the shared repository base offers no self-committing write helpers.
    public void GenericRepository_ExposesNoWriteHelpers()
    {
        var writeMembers = typeof(GenericRepository<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => WriteMethodNames.Contains(method.Name))
            .Select(method => method.Name)
            .ToArray();

        Assert.Empty(writeMembers);
    }

    // Function summary: Walks up from the test output directory to the solution root.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RvtPortal.Spa.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }
}

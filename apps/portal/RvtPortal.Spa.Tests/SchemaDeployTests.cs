// File summary: Guards the schema deploy tool against silently missing a script it is supposed to apply.
// Major updates:
// - 2026-07-14 pending Added with RVT.SchemaDeploy, which replaces the post-load half of RVT.DatabaseMigrator.

namespace RvtPortal.Spa.Tests;

public class SchemaDeployTests
{
    [Fact]
    // Function summary: Verifies every deployable script the tool must apply is copied next to its executable.
    public void DeployTool_ShipsEveryPostLoadScript()
    {
        // The tool reads its SQL from a `sql` folder next to the executable, so it works on a host with no
        // repository checked out. That copy is done by a glob in RVT.SchemaDeploy.csproj. If the glob is ever
        // narrowed to a hand-written list, a new post-load script would be added to the repository, never copied,
        // and silently never applied - the failure would be a missing view in production, not a build error.
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "RVT.SchemaDeploy", "RVT.SchemaDeploy.csproj"));

        Assert.Contains(@"post-load\*.sql", project, StringComparison.Ordinal);
        Assert.Contains("create_unmapped_schema.sql", project, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies the post-load scripts keep the numeric prefixes the tool sorts them by.
    public void PostLoadScripts_AreOrderedByNumericPrefix()
    {
        // ScriptRunner applies post-load scripts in ordinal filename order, and the order is a dependency order:
        // 02 converts tables to hypertables before 03 builds views over them. A script without a prefix would
        // sort somewhere arbitrary and run at the wrong time.
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "database", "postgres", "post-load");

        var scripts = Directory.GetFiles(directory, "*.sql")
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToArray();

        Assert.NotEmpty(scripts);

        foreach (var script in scripts)
        {
            Assert.True(
                script.Length > 2 && char.IsDigit(script[0]) && char.IsDigit(script[1]) && script[2] == '_',
                $"Post-load script '{script}' must start with a two-digit order prefix, e.g. 06_something.sql, " +
                "because RVT.SchemaDeploy applies them in filename order and that order is a dependency order.");
        }
    }

    // Function summary: Walks up from the test assembly to the repository root.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RvtPortal.Spa.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}

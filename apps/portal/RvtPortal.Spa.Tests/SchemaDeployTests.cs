// File summary: Guards the schema deploy tool against silently missing a script it is supposed to apply.
// Major updates:
// - 2026-07-14 pending Added with RVT.SchemaDeploy, which replaces the post-load half of RVT.DatabaseMigrator.

using System.Diagnostics;
using System.Xml.Linq;
using RVT.SchemaDeploy;

namespace RvtPortal.Spa.Tests;

[Collection(SchemaDeployCollection.Name)]
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
        var projectPath = Path.Combine(root, "RVT.SchemaDeploy", "RVT.SchemaDeploy.csproj");
        var project = XDocument.Load(projectPath);
        var publishedRepairScripts = project
            .Descendants("Content")
            .Where(element =>
                string.Equals(
                    (string?)element.Attribute("Include"),
                    @"..\database\postgres\restore_unmapped_column_defaults.sql",
                    StringComparison.Ordinal))
            .ToArray();

        var projectText = File.ReadAllText(projectPath);
        Assert.Contains(@"post-load\*.sql", projectText, StringComparison.Ordinal);
        Assert.Contains("create_unmapped_schema.sql", projectText, StringComparison.Ordinal);
        var repair = Assert.Single(publishedRepairScripts);
        Assert.Equal(@"sql\restore_unmapped_column_defaults.sql", (string?)repair.Attribute("Link"));
        Assert.Equal("PreserveNewest", (string?)repair.Attribute("CopyToOutputDirectory"));
    }

    [Fact]
    // Function summary: Verifies dry-run resolves every schema stage exactly once in dependency order.
    public async Task DryRun_ListsRepairExactlyOnceBetweenCreateAndPostLoadScripts()
    {
        using var fixture = TemporaryDirectory.Create();
        var postLoad = Directory.CreateDirectory(Path.Combine(fixture.Path, "post-load")).FullName;
        File.WriteAllText(Path.Combine(fixture.Path, "create_unmapped_schema.sql"), "-- create");
        File.WriteAllText(Path.Combine(fixture.Path, "restore_unmapped_column_defaults.sql"), "-- repair");
        File.WriteAllText(Path.Combine(postLoad, "02_second.sql"), "-- second");
        File.WriteAllText(Path.Combine(postLoad, "01_first.sql"), "-- first");

        var runner = new ScriptRunner(new DeployOptions
        {
            ConnectionString = "not-used-by-dry-run",
            ScriptRoot = fixture.Path,
            DryRun = true
        });

        var originalOutput = Console.Out;
        await using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var count = await runner.RunAsync();
            Assert.Equal(4, count);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var resolved = output.ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("would apply", StringComparison.Ordinal))
            .Select(line => line["would apply".Length..].Trim())
            .ToArray();

        Assert.Equal(
            [
                "create_unmapped_schema.sql",
                "restore_unmapped_column_defaults.sql",
                Path.Combine("post-load", "01_first.sql"),
                Path.Combine("post-load", "02_second.sql")
            ],
            resolved);
        Assert.Single(
            resolved,
            path => string.Equals(
                path,
                "restore_unmapped_column_defaults.sql",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Function summary: Verifies canonical deployment refuses to omit the required create stage.
    public async Task Run_WhenCreateScriptIsMissing_FailsBeforeDryRunOrConnection(bool dryRun)
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "restore_unmapped_column_defaults.sql"), "-- repair");
        var postLoad = Directory.CreateDirectory(Path.Combine(fixture.Path, "post-load")).FullName;
        File.WriteAllText(Path.Combine(postLoad, "01_first.sql"), "-- first");

        await AssertMissingStageAsync(fixture.Path, dryRun, "create_unmapped_schema.sql");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Function summary: Verifies canonical deployment refuses to omit the required forward-repair stage.
    public async Task Run_WhenRepairScriptIsMissing_FailsBeforeDryRunOrConnection(bool dryRun)
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "create_unmapped_schema.sql"), "-- create");
        var postLoad = Directory.CreateDirectory(Path.Combine(fixture.Path, "post-load")).FullName;
        File.WriteAllText(Path.Combine(postLoad, "01_first.sql"), "-- first");

        await AssertMissingStageAsync(fixture.Path, dryRun, "restore_unmapped_column_defaults.sql");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Function summary: Verifies AppleDouble sidecars cannot satisfy the required post-load stage.
    public async Task Run_WhenPostLoadHasOnlySidecars_FailsBeforeDryRunOrConnection(bool dryRun)
    {
        using var fixture = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(fixture.Path, "create_unmapped_schema.sql"), "-- create");
        File.WriteAllText(Path.Combine(fixture.Path, "restore_unmapped_column_defaults.sql"), "-- repair");
        var postLoad = Directory.CreateDirectory(Path.Combine(fixture.Path, "post-load")).FullName;
        File.WriteAllText(Path.Combine(postLoad, "._01_not_sql.sql"), "sidecar");

        await AssertMissingStageAsync(fixture.Path, dryRun, "post-load");
    }

    [Fact]
    // Function summary: Verifies a pg_restore failure is returned exactly and aborts all success verification.
    public async Task Restore_WhenPgRestoreFails_PreservesStatusAndStopsBeforeVerification()
    {
        using var fixture = TemporaryDirectory.Create();
        var result = await RunRestoreHarnessAsync(fixture, restoreStatus: 23, verificationCounts: "5|2");

        Assert.Equal(23, result.ExitCode);
        Assert.Contains("pg_restore failed with status 23", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("timescaledb_post_restore", result.DockerLog, StringComparison.Ordinal);
        Assert.DoesNotContain("pg_tables", result.DockerLog, StringComparison.Ordinal);
        Assert.DoesNotContain("Restore complete.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a restore with no application tables is rejected before success is reported.
    public async Task Restore_WhenVerificationCountsAreZero_DoesNotReportCompletion()
    {
        using var fixture = TemporaryDirectory.Create();
        var result = await RunRestoreHarnessAsync(fixture, restoreStatus: 0, verificationCounts: "0|0");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("no public tables", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("pg_tables", result.DockerLog, StringComparison.Ordinal);
        Assert.Contains("timescaledb_information.hypertables", result.DockerLog, StringComparison.Ordinal);
        Assert.DoesNotContain("Restore complete.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("5|0", "no TimescaleDB hypertables")]
    [InlineData("x|2", "invalid public table count")]
    [InlineData("5|x", "invalid hypertable count")]
    // Function summary: Verifies each invalid or empty restore-count branch independently blocks completion.
    public async Task Restore_WhenOneVerificationCountIsInvalid_DoesNotReportCompletion(
        string verificationCounts,
        string expectedError)
    {
        using var fixture = TemporaryDirectory.Create();
        var result = await RunRestoreHarnessAsync(fixture, restoreStatus: 0, verificationCounts);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expectedError, result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Restore complete.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies completion is printed only after both restored table counts are nonzero.
    public async Task Restore_WhenVerificationCountsAreNonzero_ReportsCompletion()
    {
        using var fixture = TemporaryDirectory.Create();
        var result = await RunRestoreHarnessAsync(fixture, restoreStatus: 0, verificationCounts: "5|2");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Restore complete.", result.StandardOutput, StringComparison.Ordinal);
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

    // Function summary: Exercises shared required-stage validation without opening a PostgreSQL connection.
    private static async Task AssertMissingStageAsync(
        string scriptRoot,
        bool dryRun,
        string expectedStage)
    {
        var runner = new ScriptRunner(new DeployOptions
        {
            ConnectionString = "Host=invalid.test;Database=not-used",
            ScriptRoot = scriptRoot,
            DryRun = dryRun
        });

        var exception = await Assert.ThrowsAsync<DeployException>(() => runner.RunAsync());
        Assert.Contains(expectedStage, exception.Message, StringComparison.Ordinal);
    }

    // Function summary: Runs restore against a fake Docker boundary and captures status, output, and invoked checks.
    private static async Task<RestoreHarnessResult> RunRestoreHarnessAsync(
        TemporaryDirectory fixture,
        int restoreStatus,
        string verificationCounts)
    {
        var dockerPath = Path.Combine(fixture.Path, "docker");
        var dockerLogPath = Path.Combine(fixture.Path, "docker.log");
        var dumpPath = Path.Combine(fixture.Path, "fixture.dump");
        File.WriteAllText(dumpPath, "fixture");
        File.WriteAllText(
            dockerPath,
            """
            #!/usr/bin/env bash
            printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"
            case "$*" in
                inspect*)
                    printf 'true\n'
                    ;;
                *"current_setting('server_version')"*)
                    printf '16.6|2.17.2\n'
                    ;;
                *pg_restore*)
                    exit "${FAKE_PG_RESTORE_STATUS:-0}"
                    ;;
                *pg_tables*"timescaledb_information.hypertables"*)
                    printf '%s\n' "${FAKE_VERIFY_COUNTS:-5|2}"
                    ;;
            esac
            exit 0
            """);
        File.SetUnixFileMode(
            dockerPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var script = Path.Combine(FindRepositoryRoot(), "docs", "deploy", "share-dev-database.sh");
        var startInfo = new ProcessStartInfo("/usr/bin/env")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("bash");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(dumpPath);
        startInfo.Environment["PATH"] =
            fixture.Path + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        startInfo.Environment["FAKE_DOCKER_LOG"] = dockerLogPath;
        startInfo.Environment["FAKE_PG_RESTORE_STATUS"] = restoreStatus.ToString();
        startInfo.Environment["FAKE_VERIFY_COUNTS"] = verificationCounts;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new RestoreHarnessResult(
            process.ExitCode,
            standardOutput,
            standardError,
            File.ReadAllText(dockerLogPath));
    }

    private sealed record RestoreHarnessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string DockerLog);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-schema-deploy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SchemaDeployCollection
{
    public const string Name = "Schema deployment";
}

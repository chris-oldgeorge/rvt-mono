// File summary: Parses and validates the command-line and environment inputs for the schema deploy tool.
// Major updates:
// - 2026-07-14 pending Added to replace the post-load half of the retired RVT.DatabaseMigrator.
// - 2026-07-30 pending Added the deploy lock timeout so one blocked lock cannot stall the fleet.

using System.Globalization;

namespace RVT.SchemaDeploy;

public sealed class DeployOptions
{
    public required string ConnectionString { get; init; }

    public required string ScriptRoot { get; init; }

    public required bool DryRun { get; init; }

    /// <summary>
    /// How long the deploy transaction waits for any lock before giving up. The deploy takes ACCESS EXCLUSIVE
    /// locks and holds them until commit, so an unbounded wait means one idle-in-transaction reader can stall
    /// the deploy while the deploy queues every application writer behind it. Zero disables the timeout, which
    /// is the PostgreSQL default and the pre-2026-07-30 behaviour.
    /// </summary>
    public int LockTimeoutMilliseconds { get; init; } = DefaultLockTimeoutMilliseconds;

    public const int DefaultLockTimeoutMilliseconds = 5000;

    // Function summary: Builds the options from arguments and environment, or returns null when they are unusable.
    public static DeployOptions? Parse(string[] args)
    {
        bool dryRun = false;
        string? connectionString = null;
        string? scriptRoot = null;
        int lockTimeoutMilliseconds = DefaultLockTimeoutMilliseconds;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--lock-timeout" when i + 1 < args.Length:
                    if (!int.TryParse(
                            args[++i],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out lockTimeoutMilliseconds))
                    {
                        Console.Error.WriteLine(
                            $"--lock-timeout must be a non-negative whole number of milliseconds: {args[i]}");
                        return null;
                    }

                    break;
                case "--connection" when i + 1 < args.Length:
                    connectionString = args[++i];
                    break;
                case "--scripts" when i + 1 < args.Length:
                    scriptRoot = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unrecognized argument: {args[i]}");
                    return null;
            }
        }

        // The connection string is never read from a file in the repository, for the same reason the EF
        // design-time factories do not read one: it would end up committed.
        connectionString ??= Environment.GetEnvironmentVariable("RVT_DEPLOY_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "No connection string. Pass --connection, or set RVT_DEPLOY_CONNECTION.");
            return null;
        }

        scriptRoot ??= Path.Combine(AppContext.BaseDirectory, "sql");
        if (!Directory.Exists(scriptRoot))
        {
            Console.Error.WriteLine($"Script directory not found: {scriptRoot}");
            return null;
        }

        return new DeployOptions
        {
            ConnectionString = connectionString,
            ScriptRoot = scriptRoot,
            DryRun = dryRun,
            LockTimeoutMilliseconds = lockTimeoutMilliseconds
        };
    }

    // Function summary: Prints how to invoke the tool.
    public static void PrintUsage()
    {
        Console.Error.WriteLine(
            """

            RVT.SchemaDeploy - applies the SQL that EF migrations cannot build.

              create_unmapped_schema.sql   tables and columns no EF model maps
              restore_unmapped_column_defaults.sql
                                           forward repair for columns that already exist
              post-load/*.sql              hypertables, continuous aggregates, views, routines

            Run it AFTER `dotnet ef database update` for all three contexts. Safe to re-run: it creates and
            replaces, and never drops a table or any data.

            Usage:
              dotnet run --project RVT.SchemaDeploy -- [options]

            Options:
              --connection <string>   PostgreSQL connection string (or set RVT_DEPLOY_CONNECTION)
              --scripts <dir>         Script directory (default: ./sql next to the executable)
              --dry-run               List what would run, in order, and execute nothing
              --lock-timeout <ms>     Milliseconds the deploy waits for any lock before rolling back
                                      (default: 5000; 0 waits forever, which can stall the application)
            """);
    }
}

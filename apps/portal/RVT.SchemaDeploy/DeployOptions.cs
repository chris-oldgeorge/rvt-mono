// File summary: Parses and validates the command-line and environment inputs for the schema deploy tool.
// Major updates:
// - 2026-07-14 pending Added to replace the post-load half of the retired RVT.DatabaseMigrator.

namespace RVT.SchemaDeploy;

public sealed class DeployOptions
{
    public required string ConnectionString { get; init; }

    public required string ScriptRoot { get; init; }

    public required bool DryRun { get; init; }

    // Function summary: Builds the options from arguments and environment, or returns null when they are unusable.
    public static DeployOptions? Parse(string[] args)
    {
        var dryRun = false;
        string? connectionString = null;
        string? scriptRoot = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dry-run":
                    dryRun = true;
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
            DryRun = dryRun
        };
    }

    // Function summary: Prints how to invoke the tool.
    public static void PrintUsage()
    {
        Console.Error.WriteLine(
            """

            RVT.SchemaDeploy - applies the SQL that EF migrations cannot build.

              create_unmapped_schema.sql   tables and columns no EF model maps
              post-load/*.sql              hypertables, continuous aggregates, views, routines

            Run it AFTER `dotnet ef database update` for all three contexts. Safe to re-run: it creates and
            replaces, and never drops a table or any data.

            Usage:
              dotnet run --project RVT.SchemaDeploy -- [options]

            Options:
              --connection <string>   PostgreSQL connection string (or set RVT_DEPLOY_CONNECTION)
              --scripts <dir>         Script directory (default: ./sql next to the executable)
              --dry-run               List what would run, in order, and execute nothing
            """);
    }
}

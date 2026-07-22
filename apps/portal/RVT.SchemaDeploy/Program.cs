// File summary: Applies the deployable SQL that EF migrations cannot build - the unmapped schema and post-load layer.
// Major updates:
// - 2026-07-14 pending Added to replace the post-load half of the retired RVT.DatabaseMigrator.

using RVT.SchemaDeploy;

// The database is built from three sources, in this order:
//
//   1. EF migrations, one chain per context   (dotnet ef database update --context ...)
//   2. create_unmapped_schema.sql             (tables and columns no EF model maps)
//   3. post-load/*.sql                        (hypertables, continuous aggregates, views, routines)
//
// This tool is steps 2 and 3. It exists because the only thing that ever ran them was RVT.DatabaseMigrator,
// which could not run them on their own: it required a live SQL Server source and always dropped the target
// schema and re-copied every row first. Once the cutover finished that made it unusable, so post-load changes
// were applied by hand - which is how the monitor_measurement_removal_impact view came to be dropped and never
// recreated. This tool does only the harmless half: it creates and replaces, and never drops a table or data.
//
// See docs/database/ef-migrations.md.

var options = DeployOptions.Parse(args);
if (options is null)
{
    DeployOptions.PrintUsage();
    return 2;
}

var runner = new ScriptRunner(options);

try
{
    var applied = await runner.RunAsync();
    Console.WriteLine();
    Console.WriteLine(options.DryRun
        ? $"Dry run: {applied} script(s) would be applied. Nothing was executed."
        : $"Done: {applied} script(s) applied.");
    return 0;
}
catch (DeployException exception)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"FAILED: {exception.Message}");
    if (exception.InnerException is not null)
    {
        Console.Error.WriteLine(exception.InnerException.Message);
    }

    return 1;
}

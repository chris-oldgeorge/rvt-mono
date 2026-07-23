// File summary: Resolves the deployable SQL scripts in dependency order and applies them to PostgreSQL.
// Major updates:
// - 2026-07-14 pending Added to replace the post-load half of the retired RVT.DatabaseMigrator.

using Npgsql;

namespace RVT.SchemaDeploy;

public sealed class ScriptRunner
{
    private readonly DeployOptions options;

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public ScriptRunner(DeployOptions options)
    {
        this.options = options;
    }

    // Function summary: Applies every script in order, returning how many were applied.
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var scripts = ResolveScripts();
        if (scripts.Count == 0)
        {
            throw new DeployException($"No SQL scripts found under {options.ScriptRoot}.");
        }

        if (options.DryRun)
        {
            foreach (var script in scripts)
            {
                Console.WriteLine($"  would apply  {Describe(script)}");
            }

            return scripts.Count;
        }

        await using var connection = new NpgsqlConnection(options.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            throw new DeployException($"Could not connect to the database: {exception.Message}", exception);
        }

        return await ApplyResolvedScriptsAsync(connection, scripts, cancellationToken);
    }

    /// <summary>
    /// Applies the resolved list through an already-open connection. The caller owns the connection and any active
    /// transaction, which lets provider verification exercise the real deploy twice and roll its fixture back.
    /// </summary>
    public async Task<int> RunAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var scripts = ResolveScripts();
        if (scripts.Count == 0)
        {
            throw new DeployException($"No SQL scripts found under {options.ScriptRoot}.");
        }

        if (options.DryRun)
        {
            foreach (var script in scripts)
            {
                Console.WriteLine($"  would apply  {Describe(script)}");
            }

            return scripts.Count;
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new DeployException("The supplied PostgreSQL connection must already be open.");
        }

        return await ApplyResolvedScriptsAsync(connection, scripts, cancellationToken);
    }

    // Function summary: Applies one already-resolved list to the supplied open PostgreSQL connection.
    private static async Task<int> ApplyResolvedScriptsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<string> scripts,
        CancellationToken cancellationToken)
    {
        await RequireTimescaleAsync(connection, cancellationToken);
        foreach (var script in scripts)
        {
            await ApplyAsync(connection, script, cancellationToken);
        }

        return scripts.Count;
    }

    /// <summary>
    /// The order is a dependency order, not a preference. create_unmapped_schema.sql adds monitor.offline, and
    /// post-load/03 creates views that select it - so post-load cannot run first. The forward repair restores
    /// defaults on columns the create script cannot change once they exist, so it runs between create and
    /// post-load. Within post-load the numeric prefixes carry the order (01 primary keys, 02 hypertables,
    /// 03 views, ...), so they sort by name.
    /// </summary>
    private List<string> ResolveScripts()
    {
        var scripts = new List<string>();

        var unmapped = Path.Combine(options.ScriptRoot, "create_unmapped_schema.sql");
        if (File.Exists(unmapped))
        {
            scripts.Add(unmapped);
        }

        var repair = Path.Combine(options.ScriptRoot, "restore_unmapped_column_defaults.sql");
        if (File.Exists(repair))
        {
            scripts.Add(repair);
        }

        var postLoad = Path.Combine(options.ScriptRoot, "post-load");
        if (Directory.Exists(postLoad))
        {
            scripts.AddRange(Directory.GetFiles(postLoad, "*.sql")
                .Where(IsRealScript)
                .OrderBy(path => path, StringComparer.Ordinal));
        }

        return scripts;
    }

    /// <summary>
    /// Skips macOS AppleDouble sidecars. A repository on an SMB share sprouts a `._01_pk_adjustments.sql` next
    /// to every file; it matches *.sql, its contents are binary, and executing one as SQL fails in a way that
    /// gives no hint what happened.
    /// </summary>
    private static bool IsRealScript(string path)
    {
        return !Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal);
    }

    /// <summary>
    /// post-load/02 calls create_hypertable, which does not exist without the extension. Creating the extension
    /// here is not an option - it needs privileges this tool should not assume - so the check fails early with
    /// the statement to run, rather than half-applying the schema and failing in the middle.
    /// </summary>
    private static async Task RequireTimescaleAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb')",
            connection);

        var installed = await command.ExecuteScalarAsync(cancellationToken) as bool? ?? false;
        if (!installed)
        {
            throw new DeployException(
                "The timescaledb extension is not installed in this database. The post-load scripts convert the " +
                "time-series tables into hypertables and cannot run without it. Run, as a user that may create " +
                "extensions:" + Environment.NewLine + Environment.NewLine +
                "    CREATE EXTENSION IF NOT EXISTS timescaledb;");
        }
    }

    /// <summary>
    /// Each file is sent as one command. PostgreSQL wraps a multi-statement simple query in an implicit
    /// transaction, so a script either applies whole or not at all - which matters most for post-load/03, where
    /// every view is dropped before it is recreated.
    /// </summary>
    private static async Task ApplyAsync(
        NpgsqlConnection connection,
        string path,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"  applying     {Describe(path)}");

        var sql = await File.ReadAllTextAsync(path, cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 0;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception)
        {
            throw new DeployException(
                $"{Path.GetFileName(path)} failed at line {exception.Line}: {exception.SqlState} {exception.MessageText}",
                exception);
        }
    }

    // Function summary: Renders a script path relative to the script root for logging.
    private static string Describe(string path)
    {
        var parent = Directory.GetParent(path);
        return parent is null || !string.Equals(parent.Name, "post-load", StringComparison.Ordinal)
            ? Path.GetFileName(path)
            : Path.Combine("post-load", Path.GetFileName(path));
    }
}

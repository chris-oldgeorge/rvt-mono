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
        List<string> scripts = ResolveScripts();

        if (options.DryRun)
        {
            foreach (string script in scripts)
            {
                Console.WriteLine($"  would apply  {Describe(script)}");
            }

            return scripts.Count;
        }

        await using NpgsqlConnection connection = new NpgsqlConnection(options.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            throw new DeployException($"Could not connect to the database: {exception.Message}", exception);
        }

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        int applied = await ApplyResolvedScriptsAsync(connection, transaction, scripts, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return applied;
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

        List<string> scripts = ResolveScripts();

        if (options.DryRun)
        {
            foreach (string script in scripts)
            {
                Console.WriteLine($"  would apply  {Describe(script)}");
            }

            return scripts.Count;
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new DeployException("The supplied PostgreSQL connection must already be open.");
        }

        return await ApplyResolvedScriptsAsync(connection, transaction: null, scripts, cancellationToken);
    }

    // Function summary: Applies one already-resolved list to the supplied open PostgreSQL connection.
    private static async Task<int> ApplyResolvedScriptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyList<string> scripts,
        CancellationToken cancellationToken)
    {
        await RequireTimescaleAsync(connection, transaction, cancellationToken);
        foreach (string script in scripts)
        {
            await ApplyAsync(connection, transaction, script, cancellationToken);
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
        List<string> scripts = new List<string>();

        string unmapped = Path.Combine(options.ScriptRoot, "create_unmapped_schema.sql");
        if (!File.Exists(unmapped))
        {
            throw new DeployException(
                $"Required SQL script is missing: create_unmapped_schema.sql (expected at {unmapped}).");
        }

        scripts.Add(unmapped);

        string repair = Path.Combine(options.ScriptRoot, "restore_unmapped_column_defaults.sql");
        if (!File.Exists(repair))
        {
            throw new DeployException(
                "Required SQL script is missing: restore_unmapped_column_defaults.sql " +
                $"(expected at {repair}).");
        }

        scripts.Add(repair);

        string postLoad = Path.Combine(options.ScriptRoot, "post-load");
        string[] postLoadScripts = Directory.Exists(postLoad)
            ? Directory.GetFiles(postLoad, "*.sql")
                .Where(IsRealScript)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray()
            : [];
        if (postLoadScripts.Length == 0)
        {
            throw new DeployException(
                $"Required post-load stage has no deployable SQL scripts under {postLoad}. " +
                "At least one non-sidecar *.sql script is required.");
        }

        scripts.AddRange(postLoadScripts);
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
    private static async Task RequireTimescaleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb')",
            connection,
            transaction);

        bool installed = await command.ExecuteScalarAsync(cancellationToken) as bool? ?? false;
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
    /// Each file is sent as one command inside the deploy transaction. The connection-owning entry point wraps
    /// the complete ordered list in one transaction, while the open-connection entry point participates in the
    /// caller's active transaction. This keeps the deployment atomic and supplies the transaction block required
    /// by post-load scripts that use LOCK TABLE.
    /// </summary>
    private static async Task ApplyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string path,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"  applying     {Describe(path)}");

        string sql = await File.ReadAllTextAsync(path, cancellationToken);

        await using NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction);
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
        DirectoryInfo? parent = Directory.GetParent(path);
        return parent is null || !string.Equals(parent.Name, "post-load", StringComparison.Ordinal)
            ? Path.GetFileName(path)
            : Path.Combine("post-load", Path.GetFileName(path));
    }
}

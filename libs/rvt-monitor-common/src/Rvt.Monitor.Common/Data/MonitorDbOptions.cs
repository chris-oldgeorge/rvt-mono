namespace Rvt.Monitor.Common.Data;

// Summary: Carries provider choice and monitor-specific identifier mappings into shared DB helpers.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced shared options for common monitor data access.
public sealed record MonitorDbOptions(
    MonitorDatabaseProvider Provider,
    IReadOnlyDictionary<string, string> IdentifierMap)
{
    public bool IsPostgreSql => Provider == MonitorDatabaseProvider.PostgreSql;

    public static MonitorDbOptions FromEnvironment(IReadOnlyDictionary<string, string> identifierMap)
    {
        var provider = MonitorDb.ResolveProvider(
            Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
            Environment.GetEnvironmentVariable("DatabaseProvider"));
        return new MonitorDbOptions(provider, identifierMap);
    }
}

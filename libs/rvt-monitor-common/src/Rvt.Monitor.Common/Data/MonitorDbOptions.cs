namespace Rvt.Monitor.Common.Data;

// Summary: Carries monitor-specific PostgreSQL identifier mappings into shared DB helpers.
// Major updates:
// - 2026-06-12 Monitor Migration: introduced shared options for common monitor data access.
public sealed record MonitorDbOptions(IReadOnlyDictionary<string, string> IdentifierMap)
{
    public static MonitorDbOptions FromEnvironment(IReadOnlyDictionary<string, string> identifierMap)
    {
        MonitorDb.ValidateLegacyProvider(
            Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
            Environment.GetEnvironmentVariable("DatabaseProvider"));
        return new MonitorDbOptions(identifierMap);
    }
}

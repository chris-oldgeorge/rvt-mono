namespace Rvt.Monitor.Common.Data;

// Summary: Validates monitor database provider configuration at data-client construction time.
// Major updates:
// - 2026-06-12 Monitor Migration: moved duplicated provider guard into common data access.
public static class MonitorDatabaseProviderGuard
{
    public static void EnsureSupported()
    {
        _ = MonitorDb.ResolveProvider(
            Environment.GetEnvironmentVariable("RVT__DATABASE_PROVIDER"),
            Environment.GetEnvironmentVariable("DatabaseProvider"));
    }
}

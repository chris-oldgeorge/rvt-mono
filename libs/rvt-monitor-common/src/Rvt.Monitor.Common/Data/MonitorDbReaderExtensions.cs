using System.Data.Common;

namespace Rvt.Monitor.Common.Data;

// Summary: Provides shared reader conveniences used by monitor data-access code.
// Major updates:
// - 2026-06-12 Monitor Migration: moved duplicated reader helper into common data access.
public static class MonitorDbReaderExtensions
{
    public static TimeSpan GetTimeSpan(this DbDataReader reader, int ordinal)
    {
        return reader.GetFieldValue<TimeSpan>(ordinal);
    }
}

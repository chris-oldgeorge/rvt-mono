using System.Data.Common;
using Npgsql;

namespace Rvt.Monitor.Common.Data;

// Summary: Adds Npgsql parameters for shared monitor commands.
// Major updates:
// - 2026-06-12 Monitor Migration: moved duplicated monitor parameter creation into common data access.
public static class MonitorDbParameterExtensions
{
    public static DbParameter AddWithValue(
        this DbParameterCollection parameters,
        string parameterName,
        object? value)
    {
        DbParameter parameter = new NpgsqlParameter(parameterName, value ?? DBNull.Value);
        parameters.Add(parameter);
        return parameter;
    }
}

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Rvt.Monitor.Common.Data;

// Summary: Adds provider-specific ADO.NET parameters for shared monitor commands.
// Major updates:
// - 2026-06-12 Monitor Migration: moved duplicated monitor parameter creation into common data access.
public static class MonitorDbParameterExtensions
{
    public static DbParameter AddWithValue(
        this DbParameterCollection parameters,
        string parameterName,
        object? value,
        MonitorDbOptions options)
    {
        DbParameter parameter = options.IsPostgreSql
            ? new NpgsqlParameter(parameterName, value ?? DBNull.Value)
            : new SqlParameter(parameterName, value ?? DBNull.Value);
        parameters.Add(parameter);
        return parameter;
    }
}

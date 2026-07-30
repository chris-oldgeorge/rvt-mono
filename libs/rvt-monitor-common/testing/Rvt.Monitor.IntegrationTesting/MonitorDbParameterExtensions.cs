using System.Data.Common;
using Npgsql;

namespace Rvt.Monitor.IntegrationTesting;

// Summary: Adds Npgsql parameters for monitor integration-test fixtures.
// Major updates:
// - 2026-07-30 L7: moved out of Rvt.Monitor.Common.Data - every call site is a test.
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

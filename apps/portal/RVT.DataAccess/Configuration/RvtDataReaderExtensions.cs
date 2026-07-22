// File summary: Configures provider-neutral SQL Server/PostgreSQL database access for repositories and EF Core contexts.
// Major updates:
// - 2026-07-09 pending Replaced exception-driven fallback lookup with explicit result-column scanning.
// - 2026-07-09 pending Added canonical routine-column lookup with legacy alias fallback.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.

using System.Data.Common;
using System.Globalization;

namespace RVT.DataAccess.Configuration;

internal static class RvtDataReaderExtensions
{
    // Function summary: Reads a non-null routine column, allowing legacy result aliases during database cutover.
    public static T GetRequiredValue<T>(this DbDataReader reader, string columnName, params string[] fallbackColumnNames)
    {
        var ordinal = GetOrdinal(reader, columnName, fallbackColumnNames);
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"Column '{columnName}' was null.");
        }

        return ConvertValue<T>(reader.GetValue(ordinal));
    }

    // Function summary: Reads a nullable routine value, allowing legacy result aliases during database cutover.
    public static T? GetNullableValue<T>(this DbDataReader reader, string columnName, params string[] fallbackColumnNames)
        where T : struct
    {
        var ordinal = GetOrdinal(reader, columnName, fallbackColumnNames);
        return reader.IsDBNull(ordinal) ? null : ConvertValue<T>(reader.GetValue(ordinal));
    }

    // Function summary: Retrieves nullable string data, allowing legacy result aliases during database cutover.
    public static string? GetNullableString(this DbDataReader reader, string columnName, params string[] fallbackColumnNames)
    {
        var ordinal = GetOrdinal(reader, columnName, fallbackColumnNames);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    // Function summary: Finds the first available canonical or legacy result-column alias.
    private static int GetOrdinal(DbDataReader reader, string columnName, IReadOnlyList<string> fallbackColumnNames)
    {
        var requestedColumnNames = new[] { columnName }.Concat(fallbackColumnNames);
        foreach (var requestedColumnName in requestedColumnNames)
        {
            var ordinal = FindOrdinal(reader, requestedColumnName, StringComparison.Ordinal);
            if (ordinal >= 0)
            {
                return ordinal;
            }
        }

        foreach (var requestedColumnName in requestedColumnNames)
        {
            var ordinal = FindOrdinal(reader, requestedColumnName, StringComparison.OrdinalIgnoreCase);
            if (ordinal >= 0)
            {
                return ordinal;
            }
        }

        // Not IndexOutOfRangeException: that type is reserved for the runtime's own array-bounds failures (CA2201),
        // so throwing it here is indistinguishable from a genuine indexing bug. A routine returning none of the
        // expected columns is a result-shape problem - the routine and the reader disagree.
        throw new InvalidOperationException(
            $"None of the routine result columns were found: {string.Join(", ", requestedColumnNames)}.");
    }

    // Function summary: Locates a result-column ordinal using the requested name comparison.
    private static int FindOrdinal(DbDataReader reader, string columnName, StringComparison comparison)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), columnName, comparison))
            {
                return index;
            }
        }

        return -1;
    }

    // Function summary: Converts provider-specific raw database values into the requested .NET type.
    private static T ConvertValue<T>(object value)
    {
        var targetType = typeof(T);
        if (value is T typed)
        {
            return typed;
        }

        if (targetType == typeof(Guid))
        {
            return (T)(object)(value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!));
        }

        if (targetType.IsEnum)
        {
            return (T)Enum.ToObject(targetType, value);
        }

        return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}

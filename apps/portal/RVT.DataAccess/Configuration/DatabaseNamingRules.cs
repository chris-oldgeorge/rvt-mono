// File summary: Provides canonical database naming helpers for the lowercase snake_case refactor.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added reusable database naming convention checks for the canonical schema refactor.
// - 2026-06-08 pending Extended canonical checks to cover singular relations and data-type-like column names.
// - 2026-06-08 pending Added canonical relation/column conversion helpers for the opt-in EF mapping layer.
// - 2026-06-09 pending Added canonical routine-name conversion for PostgreSQL stored routine calls.
// - 2026-06-09 pending Removed unbounded regex replacements from canonical relation singularization.

using System.Text;
using System.Text.RegularExpressions;

namespace RVT.DataAccess.Configuration;

public static partial class DatabaseNamingRules
{
    private static readonly HashSet<string> BannedIdentifiers = new(StringComparer.Ordinal)
    {
        "lock",
        "table",
        "user"
    };

    private static readonly HashSet<string> DataTypeLikeIdentifiers = new(StringComparer.Ordinal)
    {
        "bool",
        "boolean",
        "date",
        "datetime",
        "decimal",
        "double",
        "float",
        "guid",
        "int",
        "integer",
        "string",
        "text",
        "time",
        "timestamp",
        "uuid"
    };

    // Function summary: Evaluates whether an identifier follows the canonical lowercase snake_case database rules.
    public static bool IsCanonicalIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        return SnakeCaseIdentifierRegex().IsMatch(identifier) && !BannedIdentifiers.Contains(identifier);
    }

    // Function summary: Evaluates whether a relation name follows lowercase snake_case, singular, noun-focused rules.
    public static bool IsCanonicalRelationName(string? relationName)
    {
        return IsCanonicalIdentifier(relationName) &&
            relationName is not null &&
            !IsLikelyPluralRelationName(relationName) &&
            !IsDataTypeLikeIdentifier(relationName);
    }

    // Function summary: Evaluates whether a column name follows lowercase snake_case and avoids type-only naming.
    public static bool IsCanonicalColumnName(string? columnName)
    {
        return IsCanonicalIdentifier(columnName) &&
            columnName is not null &&
            !IsDataTypeLikeIdentifier(columnName);
    }

    // Function summary: Builds the canonical column name for a foreign key to the referenced table and field.
    public static string BuildForeignKeyColumnName(string referencedTable, string referencedField)
    {
        var table = ToSnakeCase(referencedTable);
        var field = ToSnakeCase(referencedField);
        return $"{table}_{field}";
    }

    // Function summary: Converts a legacy relation identifier into the canonical singular lowercase snake_case name.
    public static string ToCanonicalRelationName(string identifier)
    {
        var value = ToSnakeCase(identifier);
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = value.Replace("monitors_list", "monitor", StringComparison.Ordinal);
        value = value.Replace("reports_", "report_", StringComparison.Ordinal);
        value = value.Replace("notifications_", "notification_", StringComparison.Ordinal);
        value = value.Replace("_error_messages", "_error_message", StringComparison.Ordinal);
        value = value.Replace("_noise_levels", "_noise_level", StringComparison.Ordinal);
        value = value.Replace("_peak_levels", "_peak_level", StringComparison.Ordinal);
        value = value.Replace("_vdv_levels", "_vdv_level", StringComparison.Ordinal);
        value = value.Replace("_veff_levels", "_veff_level", StringComparison.Ordinal);
        value = value.Replace("_dust_levels", "_dust_level", StringComparison.Ordinal);
        value = value.Replace("_traces_", "_trace_", StringComparison.Ordinal);
        value = value.Replace("_users", "_user", StringComparison.Ordinal);
        value = value.Replace("_roles", "_role", StringComparison.Ordinal);
        value = value.Replace("_claims", "_claim", StringComparison.Ordinal);
        value = value.Replace("_logins", "_login", StringComparison.Ordinal);
        value = value.Replace("_tokens", "_token", StringComparison.Ordinal);
        value = value.Replace("_sections", "_section", StringComparison.Ordinal);
        value = value.Replace("_articles", "_article", StringComparison.Ordinal);
        value = value.Replace("_assets", "_asset", StringComparison.Ordinal);
        value = value.Replace("_contracts", "_contract", StringComparison.Ordinal);
        value = value.Replace("_deployments", "_deployment", StringComparison.Ordinal);
        value = value.Replace("_companies", "_company", StringComparison.Ordinal);
        value = value.Replace("_sites", "_site", StringComparison.Ordinal);
        value = value.Replace("_averages", "_average", StringComparison.Ordinal);
        value = value.Replace("_hours", "_hour", StringComparison.Ordinal);
        value = value.Replace("_settings", "_setting", StringComparison.Ordinal);
        value = value.Replace("_rules", "_rule", StringComparison.Ordinal);
        value = value.Replace("_sensors", "_sensor", StringComparison.Ordinal);
        value = value.Replace("_actions_", "_action_", StringComparison.Ordinal);

        var finalWord = value.Split('_', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        if (finalWord.EndsWith("ies", StringComparison.Ordinal))
        {
            value = string.Concat(value.AsSpan(0, value.Length - 3), "y");
        }
        else if (finalWord.EndsWith('s') &&
            finalWord is not "status" and not "class" &&
            !finalWord.EndsWith("ss", StringComparison.Ordinal))
        {
            value = value[..^1];
        }

        return value;
    }

    /// <summary>
    /// Column names the deployed schema uses that plain snake_case would not produce, matched on the WHOLE
    /// snake_cased name.
    /// </summary>
    /// <remarks>
    /// These were previously applied as a chain of unanchored string.Replace calls, which rewrote the pattern
    /// anywhere it appeared. Replace("nr", "row_count") mangled Monitor.FleetNr into a column called
    /// "fleet_row_count", and would have silently mangled any future property whose name merely contained those
    /// two letters: IsUnread -> "is_urow_countead", EnrolledAt -> "erow_countolled_at".
    ///
    /// Names are matched whole now, so only deliberate exceptions are rewritten. The mangled columns themselves
    /// were corrected by the RenameMangledColumns migration (fleet_row_count -> fleet_nr, and the row_count /
    /// row_count_sites view aliases), so they no longer need entries here - FleetNr, Nr and NrSites now fall
    /// through to plain snake_case.
    /// </remarks>
    private static readonly Dictionary<string, string> ColumnNameOverrides = new(StringComparer.Ordinal)
    {
        // Deliberate renames.
        ["nr_users"] = "user_count",
        ["timestamtp"] = "logged_at",
        ["timestamp"] = "recorded_at",
        ["text"] = "content",
        ["date"] = "event_date",
        ["operating_volume_flow_timestamp"] = "operating_volume_flow_time",

        // Acoustic and particulate measures, which read better unsplit.
        ["l_aeq"] = "laeq",
        ["l_amax"] = "lamax",
        ["l_a_90"] = "la90",
        ["l_a_10"] = "la10",
        ["l_ceq"] = "lceq",
        ["l_cmax"] = "lcmax",
        ["l_c_90"] = "lc90",
        ["l_c_10"] = "lc10",
        ["p_m_10"] = "pm10",
        ["p_m_2_5"] = "pm2_5",
        ["t_mio"] = "tmio",
        ["p_mio"] = "pmio"
    };

    // Function summary: Converts a legacy column identifier into the canonical lowercase snake_case name.
    public static string ToCanonicalColumnName(string identifier, bool isSingleColumnPrimaryKey = false)
    {
        if (isSingleColumnPrimaryKey)
        {
            return "id";
        }

        var value = ToSnakeCase(identifier);
        return ColumnNameOverrides.TryGetValue(value, out var canonical) ? canonical : value;
    }

    // Function summary: Converts a legacy stored procedure/function identifier into a canonical routine name.
    public static string ToCanonicalRoutineName(string identifier)
    {
        var value = ToSnakeCase(identifier);
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Replace("date_time", "time", StringComparison.Ordinal);
    }

    // Function summary: Converts a legacy database identifier into a lowercase snake_case candidate.
    public static string ToSnakeCase(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(identifier.Length * 2);
        for (var index = 0; index < identifier.Length; index++)
        {
            var current = identifier[index];
            if (!char.IsLetterOrDigit(current))
            {
                AppendUnderscore(builder);
                continue;
            }

            if (index > 0 && ShouldSplit(identifier, index))
            {
                AppendUnderscore(builder);
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        var value = DuplicateUnderscoreRegex().Replace(builder.ToString().Trim('_'), "_");
        return value.Replace("sitei_d", "site_id", StringComparison.Ordinal);
    }

    // Function summary: Flags relation names that look plural under the project naming standard.
    private static bool IsLikelyPluralRelationName(string relationName)
    {
        var finalWord = relationName.Split('_', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? relationName;
        if (finalWord is "status" or "data")
        {
            return false;
        }

        return finalWord.EndsWith("ies", StringComparison.Ordinal) ||
            (finalWord.EndsWith('s') && !finalWord.EndsWith("ss", StringComparison.Ordinal));
    }

    // Function summary: Flags identifiers that are named after data types rather than domain concepts.
    private static bool IsDataTypeLikeIdentifier(string identifier)
    {
        if (DataTypeLikeIdentifiers.Contains(identifier))
        {
            return true;
        }

        return identifier.EndsWith("_text", StringComparison.Ordinal) ||
            identifier.EndsWith("_timestamp", StringComparison.Ordinal);
    }

    // Function summary: Evaluates whether the current character starts a new identifier word.
    private static bool ShouldSplit(string identifier, int index)
    {
        var current = identifier[index];
        var previous = identifier[index - 1];
        if (char.IsDigit(current) && char.IsLetter(previous))
        {
            return true;
        }

        if (char.IsLetter(current) && char.IsDigit(previous))
        {
            return true;
        }

        if (!char.IsUpper(current))
        {
            return false;
        }

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        return index + 1 < identifier.Length && char.IsLower(identifier[index + 1]);
    }

    // Function summary: Appends one separator without creating duplicate underscores.
    private static void AppendUnderscore(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '_')
        {
            builder.Append('_');
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.None, 250)]
    private static partial Regex SnakeCaseIdentifierRegex();

    [GeneratedRegex("_+", RegexOptions.None, 250)]
    private static partial Regex DuplicateUnderscoreRegex();
}

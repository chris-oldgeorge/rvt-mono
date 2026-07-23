// File summary: Defines the UTC application/plain-timestamp database boundary for portal telemetry searches.

namespace RvtPortal.Spa.Application.Monitors;

internal static class SearchTimestampPolicy
{
    // Function summary: Validates a UTC application instant and strips only its Kind for timestamp-without-zone queries.
    public static DateTime ToDatabase(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            : throw new ArgumentException(
                "Search timestamp bounds must be UTC.",
                nameof(value));

    // Function summary: Restores the UTC contract on a timestamp-without-zone value materialized by the database.
    public static DateTime? FromDatabase(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}

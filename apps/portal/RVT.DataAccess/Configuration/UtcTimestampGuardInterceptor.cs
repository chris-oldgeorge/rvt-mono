// File summary: Fails a write of a non-UTC DateTime to a PostgreSQL timestamptz column before it reaches the database.
// Major updates:
// - 2026-07-15 pending Added after DateTime.Now writes to timestamptz columns were found to throw on persistence.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RVT.DataAccess.Configuration;

/// <summary>
/// PostgreSQL <c>timestamp with time zone</c> columns accept only <c>Kind=Utc</c> DateTime values through Npgsql;
/// a Local or Unspecified value throws at write time. A value converter that coerced Kind to Utc would be worse
/// than the throw - it would relabel a local <c>14:00</c> as <c>14:00Z</c> and silently store the wrong instant.
/// So this interceptor fails the write loudly, naming the offending entity and property, at the moment the domain
/// context saves.
///
/// It guards only timestamptz columns, so it is inert on <c>timestamp without time zone</c> columns (which
/// require Unspecified and are written by the ingestion layer) and on SQL Server, where DateTime maps to
/// <c>datetime2</c> and Kind carries no meaning. The rule it enforces: the domain layer stores UTC.
/// </summary>
public sealed class UtcTimestampGuardInterceptor : SaveChangesInterceptor
{
    /// <summary>Stateless, so a single shared instance is registered on every context.</summary>
    public static readonly UtcTimestampGuardInterceptor Instance = new();

    private const string TimestamptzStoreType = "timestamp with time zone";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if any timestamptz column on an added or modified entity is
    /// about to be written a non-UTC DateTime. Exposed for direct testing: it reads only the change tracker, so a
    /// test can build a context, add an entity, and call this without opening a database connection.
    /// </summary>
    // Function summary: Rejects non-UTC DateTime values bound for timestamptz columns before they reach PostgreSQL.
    public static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        List<string>? violations = null;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (var property in entry.Properties)
            {
                // A Modified entity writes only the columns that actually changed.
                if (entry.State == EntityState.Modified && !property.IsModified)
                {
                    continue;
                }

                // Kind=Utc is the only value the column accepts; anything else (Local, Unspecified) is the defect.
                if (property.CurrentValue is not DateTime value || value.Kind == DateTimeKind.Utc)
                {
                    continue;
                }

                if (!IsTimestamptz(property.Metadata))
                {
                    continue;
                }

                (violations ??= []).Add(
                    $"{entry.Metadata.DisplayName()}.{property.Metadata.Name} (Kind={value.Kind})");
            }
        }

        if (violations is not null)
        {
            throw new InvalidOperationException(
                "Refusing to write a non-UTC DateTime to a 'timestamp with time zone' column: PostgreSQL/Npgsql " +
                "accepts only Kind=Utc there, and coercing the Kind would store the wrong instant. Use " +
                "DateTime.UtcNow or IRvtDateTimeProvider.UtcNow, or normalize an incoming value with " +
                $"DateTime.SpecifyKind(value, DateTimeKind.Utc). Offending: {string.Join(", ", violations)}.");
        }
    }

    // Function summary: Reports whether a property maps to a PostgreSQL timestamptz column.
    private static bool IsTimestamptz(IProperty property)
    {
        var storeType = property.GetColumnType() ?? property.FindRelationalTypeMapping()?.StoreType;
        return string.Equals(storeType, TimestamptzStoreType, StringComparison.OrdinalIgnoreCase);
    }
}

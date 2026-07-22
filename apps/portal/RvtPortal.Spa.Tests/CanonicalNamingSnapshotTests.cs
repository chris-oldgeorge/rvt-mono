// File summary: Pins every canonical table/column name the EF model produces so schema mapping cannot drift.
// Major updates:
// - 2026-07-14 pending Replaced the spot checks with a full approved snapshot ahead of the naming-map swap.
// - 2026-07-14 pending Added a snapshot of the canonical naming rules ahead of replacing the heuristics with a map.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// Every relation and column name the two EF models produce, pinned against a checked-in approved file.
///
/// The deployed schema already contains names nobody would choose - Monitor.FleetNr maps to a
/// "fleet_row_count" column - so the mapping cannot simply be "fixed"; it has to be preserved exactly. This
/// snapshot is what makes changing the naming implementation safe: the replacement is correct precisely when
/// this test still passes. It also catches any accidental rule change, which would otherwise surface only as an
/// "invalid column name" against the real database and never fail a test.
///
/// If a model change legitimately adds or renames a mapped member, regenerate CanonicalNames.approved.txt from
/// the failure output and review the diff - every line of it is a schema change.
/// </summary>
public sealed class CanonicalNamingSnapshotTests
{
    private const string ApprovedFileName = "CanonicalNames.approved.txt";

    [Fact]
    // Function summary: Verifies the model's relation and column names still match the approved schema mapping.
    public void CanonicalNames_MatchTheApprovedSnapshot()
    {
        var approved = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, ApprovedFileName))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var actual = BuildCanonicalNames();

        var missing = approved.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(approved, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0 && unexpected.Length == 0,
            $"Canonical schema mapping changed.\n\nNo longer produced ({missing.Length}):\n  " +
            $"{string.Join("\n  ", missing.Take(25))}\n\nNewly produced ({unexpected.Length}):\n  " +
            $"{string.Join("\n  ", unexpected.Take(25))}");
    }

    [Fact]
    // Function summary: Verifies a property name containing "nr" is no longer mangled by an unanchored replace.
    public void ColumnNames_AreNotMangledBySubstringReplacement()
    {
        // The old rules replaced "nr" anywhere it appeared, so a property like IsUnread would have mapped to
        // "is_urow_countead". Names are matched whole now, so only the intended exceptions are rewritten.
        Assert.Equal("is_unread", DatabaseNamingRulesProbe.Column("IsUnread"));
        Assert.Equal("enrolled_at", DatabaseNamingRulesProbe.Column("EnrolledAt"));
        Assert.Equal("unresolved", DatabaseNamingRulesProbe.Column("Unresolved"));

        // The names the mangling used to produce are now the plain snake_case ones the code always asked for.
        Assert.Equal("fleet_nr", DatabaseNamingRulesProbe.Column("FleetNr"));
        Assert.Equal("nr_sites", DatabaseNamingRulesProbe.Column("NrSites"));
        Assert.Equal("nr", DatabaseNamingRulesProbe.Column("Nr"));

        // NrUsers -> user_count remains a deliberate rename, not an artifact.
        Assert.Equal("user_count", DatabaseNamingRulesProbe.Column("NrUsers"));
    }

    // Function summary: Builds "Entity|Member|name" lines for every mapped relation and column.
    private static string[] BuildCanonicalNames()
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);

        // A relational provider is required to resolve table/column names; no connection is ever opened.
        using var domain = new RVTDbContext(new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;").Options);
        using var search = new RVTSearchContext(new DbContextOptionsBuilder<RVTSearchContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;").Options);

        foreach (var context in new DbContext[] { domain, search })
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                    ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
                if (store == null)
                {
                    continue;
                }

                lines.Add($"{entityType.ClrType.Name}|<relation>|{store.Value.Name}");
                foreach (var property in entityType.GetProperties())
                {
                    var column = property.GetColumnName(store.Value);
                    if (column != null)
                    {
                        lines.Add($"{entityType.ClrType.Name}|{property.Name}|{column}");
                    }
                }
            }
        }

        return [.. lines];
    }
}

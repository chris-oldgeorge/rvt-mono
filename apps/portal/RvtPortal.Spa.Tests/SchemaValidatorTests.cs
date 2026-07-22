// File summary: Verifies the model-vs-database comparison that guards against silent mapping drift.
// Major updates:
// - 2026-07-14 pending Added coverage for RvtSchemaValidator.Compare.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Tests;

public sealed class SchemaValidatorTests
{
    [Fact]
    // Function summary: Verifies a schema that has everything the model maps produces no complaints.
    public void Compare_MatchingSchema_ReportsNothing()
    {
        using var context = RelationalContext();

        var mismatches = RvtSchemaValidator.Compare(context.Model, SchemaFromModel(context));

        Assert.Empty(mismatches);
    }

    [Fact]
    // Function summary: Verifies a table the model maps but the database lacks is reported.
    public void Compare_MissingRelation_IsReported()
    {
        using var context = RelationalContext();
        var schema = SchemaFromModel(context);
        var dropped = RelationOf(context, typeof(RVT.Entities.Monitor));
        schema.Remove(dropped);

        var mismatches = RvtSchemaValidator.Compare(context.Model, schema);

        var mismatch = Assert.Single(mismatches, item => item.Relation == dropped);
        Assert.Null(mismatch.Column);
        Assert.Contains("missing from the database", mismatch.Problem, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a column the model maps but the database lacks is reported against its property.
    public void Compare_MissingColumn_IsReported()
    {
        using var context = RelationalContext();
        var schema = SchemaFromModel(context);
        var relation = RelationOf(context, typeof(RVT.Entities.Monitor));

        // Exactly the failure this guards: the model maps Monitor.FleetNr to fleet_nr, but the database still
        // has the old mangled column, so every query touching it would fail at runtime.
        var columns = new HashSet<string>(schema[relation], StringComparer.OrdinalIgnoreCase);
        columns.Remove("fleet_nr");
        columns.Add("fleet_row_count");
        schema[relation] = columns;

        var mismatches = RvtSchemaValidator.Compare(context.Model, schema);

        var mismatch = Assert.Single(mismatches, item => item.Column == "fleet_nr");
        Assert.Equal(relation, mismatch.Relation);
        Assert.Contains("Monitor.FleetNr", mismatch.Problem, StringComparison.Ordinal);
    }

    // Function summary: Builds the schema the model expects, as the shape the validator compares against.
    private static Dictionary<string, IReadOnlySet<string>> SchemaFromModel(DbContext context)
    {
        var schema = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (store == null)
            {
                continue;
            }

            if (!schema.TryGetValue(store.Value.Name, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                schema[store.Value.Name] = columns;
            }

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(store.Value);
                if (column != null)
                {
                    ((HashSet<string>)columns).Add(column);
                }
            }
        }

        return schema;
    }

    // Function summary: Returns the physical relation name the model maps a CLR type to.
    private static string RelationOf(DbContext context, Type clrType)
    {
        var entityType = context.Model.FindEntityType(clrType)!;
        var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
            ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
        return store!.Value.Name;
    }

    // Function summary: Builds a relational context; no connection is opened because nothing is executed.
    private static RVTDbContext RelationalContext()
    {
        return new RVTDbContext(new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;")
            .Options);
    }
}

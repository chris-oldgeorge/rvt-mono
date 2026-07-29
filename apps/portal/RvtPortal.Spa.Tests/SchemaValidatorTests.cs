// File summary: Verifies the model-vs-database comparison that guards against silent mapping drift.
// Major updates:
// - 2026-07-25 pending Switched provider-neutral relational metadata coverage to Npgsql.
// - 2026-07-14 pending Added coverage for RvtSchemaValidator.Compare.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

using RvtPortal.Spa.Tests.Support;
namespace RvtPortal.Spa.Tests;

public sealed class SchemaValidatorTests
{
    [Fact]
    // Function summary: Verifies a schema that has everything the model maps produces no complaints.
    public void Compare_MatchingSchema_ReportsNothing()
    {
        using RVTDbContext context = RelationalContext();

        IReadOnlyList<SchemaMismatch> mismatches = RvtSchemaValidator.Compare(context.Model, SchemaFromModel(context));

        Assert.Empty(mismatches);
    }

    [Fact]
    // Function summary: Verifies a table the model maps but the database lacks is reported.
    public void Compare_MissingRelation_IsReported()
    {
        using RVTDbContext context = RelationalContext();
        Dictionary<string, IReadOnlySet<string>> schema = SchemaFromModel(context);
        string dropped = RelationOf(context, typeof(RVT.Entities.Monitor));
        schema.Remove(dropped);

        IReadOnlyList<SchemaMismatch> mismatches = RvtSchemaValidator.Compare(context.Model, schema);

        SchemaMismatch mismatch = Assert.Single(mismatches, item => item.Relation == dropped);
        Assert.Null(mismatch.Column);
        Assert.Contains("missing from the database", mismatch.Problem, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a column the model maps but the database lacks is reported against its property.
    public void Compare_MissingColumn_IsReported()
    {
        using RVTDbContext context = RelationalContext();
        Dictionary<string, IReadOnlySet<string>> schema = SchemaFromModel(context);
        string relation = RelationOf(context, typeof(RVT.Entities.Monitor));

        // Exactly the failure this guards: the model maps Monitor.FleetNr to fleet_nr, but the database still
        // has the old mangled column, so every query touching it would fail at runtime.
        HashSet<string> columns = new(schema[relation], StringComparer.OrdinalIgnoreCase);
        columns.Remove("fleet_nr");
        columns.Add("fleet_row_count");
        schema[relation] = columns;

        IReadOnlyList<SchemaMismatch> mismatches = RvtSchemaValidator.Compare(context.Model, schema);

        SchemaMismatch mismatch = Assert.Single(mismatches, item => item.Column == "fleet_nr");
        Assert.Equal(relation, mismatch.Relation);
        Assert.Contains("Monitor.FleetNr", mismatch.Problem, StringComparison.Ordinal);
    }

    // Function summary: Builds the schema the model expects, as the shape the validator compares against.
    private static Dictionary<string, IReadOnlySet<string>> SchemaFromModel(DbContext context)
    {
        Dictionary<string, IReadOnlySet<string>> schema = new(StringComparer.OrdinalIgnoreCase);
        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            StoreObjectIdentifier? store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (store == null)
            {
                continue;
            }

            if (!schema.TryGetValue(store.Value.Name, out IReadOnlySet<string>? columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                schema[store.Value.Name] = columns;
            }

            foreach (IProperty property in entityType.GetProperties())
            {
                string? column = property.GetColumnName(store.Value);
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
        IEntityType entityType = context.Model.FindEntityType(clrType)!;
        StoreObjectIdentifier? store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
            ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
        return store!.Value.Name;
    }

    // Function summary: Builds a relational context; no connection is opened because nothing is executed.
    private static RVTDbContext RelationalContext()
    {
        return new RVTDbContext(TestDbContexts.ModelOnlyNpgsql<RVTDbContext>());
    }
}

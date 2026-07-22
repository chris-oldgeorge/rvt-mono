// File summary: Provides opt-in EF Core model conventions for the canonical database naming upgrade.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added opt-in canonical EF table/view/column mapping for the database naming refactor.
// - 2026-06-08 pending Excluded ASP.NET Identity entities from the canonical EF naming convention.
// - 2026-06-09 pending Canonicalized from configured store column names for scaffolded search-context acronyms.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RVT.DataAccess.Configuration;

public static class RvtCanonicalModelBuilderExtensions
{
    // Function summary: Applies canonical lowercase singular snake_case table/view and column names to an EF model.
    public static ModelBuilder ApplyRvtCanonicalDatabaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null || entityType.IsOwned() || IsFrameworkManagedIdentityEntity(entityType))
            {
                continue;
            }

            ApplyCanonicalRelationName(modelBuilder, entityType);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null || entityType.IsOwned() || IsFrameworkManagedIdentityEntity(entityType))
            {
                continue;
            }

            ApplyCanonicalColumnNames(modelBuilder, entityType);
        }

        // Constraint and index names have to be canonicalized too, and only after the tables and columns they
        // are derived from. The database has carried canonical names since the cutover
        // (database/{provider}/canonical_constraint_index_naming.sql renamed them), but the model never did - it
        // kept EF's PascalCase defaults (PK_monitor, FK_contract_company_company_id). That gap only stayed
        // invisible while EF migrations were unusable; the moment the tooling generates a migration it emits
        // constraint names the database does not have.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null || entityType.IsOwned() || IsFrameworkManagedIdentityEntity(entityType))
            {
                continue;
            }

            ApplyCanonicalConstraintNames(entityType);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Names keys, foreign keys and indexes the way the database does: pk_{table}, fk_{table}_{columns},
    /// ix_{table}_{columns} - all lowercase snake_case, matching canonical_constraint_index_naming.sql.
    /// </summary>
    private static void ApplyCanonicalConstraintNames(IMutableEntityType entityType)
    {
        // Views have no constraints to name.
        var table = entityType.GetTableName();
        if (string.IsNullOrWhiteSpace(table))
        {
            return;
        }

        foreach (var key in entityType.GetKeys())
        {
            key.SetName($"pk_{table}");
        }

        foreach (var foreignKey in entityType.GetForeignKeys())
        {
            foreignKey.SetConstraintName($"fk_{table}_{ColumnSuffix(foreignKey.Properties)}");
        }

        foreach (var index in entityType.GetIndexes())
        {
            index.SetDatabaseName($"ix_{table}_{ColumnSuffix(index.Properties)}");
        }
    }

    // Function summary: Joins the canonical column names of the properties behind a constraint or index.
    private static string ColumnSuffix(IReadOnlyList<IMutableProperty> properties)
    {
        return string.Join(
            '_',
            properties.Select(property => property.GetColumnName() ?? DatabaseNamingRules.ToCanonicalColumnName(property.Name)));
    }

    // Function summary: Identifies framework-managed ASP.NET Identity entities that must keep their default physical names.
    private static bool IsFrameworkManagedIdentityEntity(IMutableEntityType entityType)
    {
        var tableName = entityType.GetTableName() ?? entityType.ClrType.Name;
        return tableName.StartsWith("AspNet", StringComparison.Ordinal) ||
            (entityType.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) ?? false);
    }

    // Function summary: Applies the canonical relation name to the entity table or view mapping.
    private static void ApplyCanonicalRelationName(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        var entityBuilder = modelBuilder.Entity(entityType.ClrType);
        var viewName = entityType.GetViewName();
        if (!string.IsNullOrWhiteSpace(viewName))
        {
            entityBuilder.ToView(DatabaseNamingRules.ToCanonicalRelationName(viewName), entityType.GetViewSchema());
            return;
        }

        var tableName = entityType.GetTableName() ?? entityType.ClrType.Name;
        entityBuilder.ToTable(DatabaseNamingRules.ToCanonicalRelationName(tableName), entityType.GetSchema());
    }

    // Function summary: Applies canonical column names to scalar, primary-key, and foreign-key properties.
    private static void ApplyCanonicalColumnNames(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        var entityBuilder = modelBuilder.Entity(entityType.ClrType);
        var primaryKey = entityType.FindPrimaryKey();
        var foreignKeys = entityType.GetForeignKeys().ToArray();

        foreach (var property in entityType.GetProperties())
        {
            var isSingleColumnPrimaryKey = primaryKey is not null &&
                primaryKey.Properties.Count == 1 &&
                primaryKey.Properties.Contains(property);
            var sourceColumnName = property.GetColumnName() ?? property.Name;
            var columnName = isSingleColumnPrimaryKey
                ? DatabaseNamingRules.ToCanonicalColumnName(property.Name, true)
                : BuildCanonicalColumnName(property, foreignKeys, sourceColumnName);

            entityBuilder.Property(property.ClrType, property.Name).HasColumnName(columnName);
        }
    }

    // Function summary: Builds a canonical column name, using referenced relation names for foreign keys.
    private static string BuildCanonicalColumnName(
        IMutableProperty property,
        IReadOnlyCollection<IMutableForeignKey> foreignKeys,
        string sourceColumnName)
    {
        var foreignKey = foreignKeys.FirstOrDefault(candidate =>
            candidate.Properties.Contains(property) &&
            candidate.PrincipalKey.Properties.Count == 1);
        if (foreignKey is null)
        {
            return DatabaseNamingRules.ToCanonicalColumnName(sourceColumnName);
        }

        var principalRelation = DatabaseNamingRules.ToCanonicalRelationName(
            foreignKey.PrincipalEntityType.GetTableName() ?? foreignKey.PrincipalEntityType.ClrType.Name);
        var principalField = DatabaseNamingRules.ToCanonicalColumnName(foreignKey.PrincipalKey.Properties[0].Name, true);
        return DatabaseNamingRules.BuildForeignKeyColumnName(principalRelation, principalField);
    }
}

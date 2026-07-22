// File summary: Compares the EF model's relations and columns against the ones the database actually has.
// Major updates:
// - 2026-07-14 pending Added so mapping drift fails at startup instead of on the first query that touches it.

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RVT.DataAccess.Configuration
{
    public sealed record SchemaMismatch(string Relation, string? Column, string Problem)
    {
        public override string ToString()
        {
            return Column == null
                ? $"{Relation}: {Problem}"
                : $"{Relation}.{Column}: {Problem}";
        }
    }

    /// <summary>
    /// Checks that every relation and column the EF model maps to actually exists in the target database.
    ///
    /// The canonical naming rules derive physical names from CLR names, so a model change - or a change to the
    /// rules - can silently map an entity onto a table or column that is not there. Nothing catches that until a
    /// query touches it in production, and then it surfaces as an "invalid column name" from deep inside a
    /// request. CanonicalNamingSnapshotTests pins the names the code produces; this pins them against the
    /// schema that actually exists.
    /// </summary>
    public static class RvtSchemaValidator
    {
        // Function summary: Reads the live schema and reports every relation or column the model expects but lacks.
        public static async Task<IReadOnlyList<SchemaMismatch>> ValidateAsync(
            DbContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.Database.IsRelational())
            {
                return [];
            }

            var actual = await ReadSchemaAsync(context, cancellationToken);
            return Compare(context.Model, actual);
        }

        /// <summary>
        /// The comparison itself, separated from the database read so it can be tested without one.
        /// </summary>
        public static IReadOnlyList<SchemaMismatch> Compare(
            IModel model,
            IReadOnlyDictionary<string, IReadOnlySet<string>> actualColumnsByRelation)
        {
            var mismatches = new List<SchemaMismatch>();

            foreach (var entityType in model.GetEntityTypes())
            {
                var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                    ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
                if (store == null)
                {
                    continue;
                }

                var relation = store.Value.Name;
                if (!actualColumnsByRelation.TryGetValue(relation, out var actualColumns))
                {
                    mismatches.Add(new SchemaMismatch(relation, null, "mapped by the model but missing from the database"));
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    var column = property.GetColumnName(store.Value);
                    if (column != null && !actualColumns.Contains(column))
                    {
                        mismatches.Add(new SchemaMismatch(
                            relation,
                            column,
                            $"mapped from {entityType.ClrType.Name}.{property.Name} but missing from the database"));
                    }
                }
            }

            return mismatches;
        }

        // Function summary: Reads relation and column names from the provider's information schema.
        private static async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> ReadSchemaAsync(
            DbContext context,
            CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection();
            var opened = false;
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                opened = true;
            }

            try
            {
                using var command = connection.CreateCommand();

                // information_schema is ANSI and exposes views alongside tables on both providers, so one query
                // covers everything the model can map to.
                command.CommandText =
                    "SELECT table_name, column_name FROM information_schema.columns";

                var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var relation = reader.GetString(0);
                    var column = reader.GetString(1);

                    if (!result.TryGetValue(relation, out var columns))
                    {
                        columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        result[relation] = columns;
                    }

                    ((HashSet<string>)columns).Add(column);
                }

                return result;
            }
            finally
            {
                if (opened)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}

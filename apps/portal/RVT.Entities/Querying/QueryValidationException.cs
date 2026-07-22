// File summary: Signals a search filter or sort that names something the entity does not expose.
// Major updates:
// - 2026-07-14 pending Added so unknown filter/sort fields fail loudly instead of silently widening the result set.

using System;

namespace RVT.Entities.Querying
{
    /// <summary>
    /// Thrown when a filter or sort names a property the entity does not have, or an operation the builder does
    /// not implement. These used to be dropped silently, which turned a fully misspelled filter into a
    /// match-everything query - the query still succeeded, it just returned rows the caller never asked for.
    /// </summary>
    public sealed class QueryValidationException : InvalidOperationException
    {
        public QueryValidationException(string message)
            : base(message)
        {
        }

        // Function summary: Reports a filter or sort field the entity does not expose.
        public static QueryValidationException UnknownProperty(Type entityType, string? propertyName, string usage)
        {
            return new QueryValidationException(
                $"Unknown {usage} field '{propertyName}' for '{entityType.Name}'.");
        }

        // Function summary: Reports a filter operation the expression builder does not implement.
        public static QueryValidationException UnsupportedOperation(Type entityType, string? propertyName, object operation)
        {
            return new QueryValidationException(
                $"Unsupported filter operation '{operation}' on '{entityType.Name}.{propertyName}'.");
        }

        // Function summary: Reports a filter type the expression builder does not understand.
        public static QueryValidationException UnsupportedFilter(Type filterType)
        {
            return new QueryValidationException($"Unsupported filter type '{filterType.Name}'.");
        }
    }
}

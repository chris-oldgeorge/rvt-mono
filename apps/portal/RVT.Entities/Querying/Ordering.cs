// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Linq.Expressions;
using System.Reflection;

namespace RVT.Entities.Querying;

public enum OrderByDirectionEnum
{
    Ascending,
    Descending
}

public class OrderByProperty
{
    public string? OrderByColumn { get; set; }
    public OrderByDirectionEnum OrderByDirection { get; set; } = OrderByDirectionEnum.Ascending;
}

public static class QueryOrderingExtensions
{
    // Resolved once instead of scanning every Queryable overload on each ordered column of every query.
    private static readonly Dictionary<string, MethodInfo> OrderingMethods = new[]
        {
            nameof(Queryable.OrderBy),
            nameof(Queryable.OrderByDescending),
            nameof(Queryable.ThenBy),
            nameof(Queryable.ThenByDescending)
        }
        .ToDictionary(
            name => name,
            name => typeof(Queryable).GetMethods()
                .Single(method => method.Name == name
                    && method.GetParameters().Length == 2
                    && method.GetGenericArguments().Length == 2),
            StringComparer.Ordinal);

    // Function summary: Handles the t workflow for this module.
    public static IQueryable<T> OrderedBy<T>(this IQueryable<T> source, IEnumerable<OrderByProperty>? orderByProperties)
    {
        if (orderByProperties == null)
        {
            return source;
        }

        IOrderedQueryable<T>? ordered = null;
        foreach (var orderBy in orderByProperties.Where(o => !string.IsNullOrWhiteSpace(o.OrderByColumn)))
        {
            // An unrecognized sort field used to be dropped, so callers silently got unordered results.
            var property = QueryPropertyResolver.Resolve(typeof(T), orderBy.OrderByColumn!)
                ?? throw QueryValidationException.UnknownProperty(typeof(T), orderBy.OrderByColumn, "sort");

            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var keySelector = Expression.Lambda(propertyExpression, parameter);
            var descending = orderBy.OrderByDirection == OrderByDirectionEnum.Descending;
            var methodName = (ordered, descending) switch
            {
                (null, true) => nameof(Queryable.OrderByDescending),
                (null, false) => nameof(Queryable.OrderBy),
                (_, true) => nameof(Queryable.ThenByDescending),
                _ => nameof(Queryable.ThenBy)
            };

            ordered = (IOrderedQueryable<T>)OrderingMethods[methodName]
                .MakeGenericMethod(typeof(T), property.PropertyType)
                .Invoke(null, [ordered ?? source, keySelector])!;
        }

        return ordered ?? source;
    }
}

internal static class QueryPropertyResolver
{
    // Function summary: Handles the resolve workflow for this module.
    public static PropertyInfo? Resolve(Type type, string propertyName)
    {
        return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    }
}

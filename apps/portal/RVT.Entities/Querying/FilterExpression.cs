// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-06-25 pending Returned concrete string-call expressions for CA1859 analyzer cleanup.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Globalization;
using System.Linq.Expressions;

namespace RVT.Entities.Querying;

public static class FilterExpression
{
    public static class ExpressionBuilder
    {
        // Function summary: Handles the t workflow for this module.
        public static Expression<Func<T, bool>> GetExpression<T>(IEnumerable<Filter> filters)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? body = null;

            foreach (var filter in filters)
            {
                var expression = BuildExpression(parameter, filter);
                body = body == null ? expression : Expression.AndAlso(body, expression);
            }

            // No filters supplied means "match everything", which is legitimate. Filters that were supplied but
            // could not be built no longer reach here - they throw - so this can never silently widen a query.
            return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(true), parameter);
        }

        // Function summary: Builds expression data for callers.
        private static Expression BuildExpression(ParameterExpression parameter, Filter filter)
        {
            return filter switch
            {
                SingleFilter singleFilter => BuildSingleExpression(parameter, singleFilter),
                OrFilter orFilter => BuildOrExpression(parameter, orFilter),
                _ => throw QueryValidationException.UnsupportedFilter(filter.GetType())
            };
        }

        // Function summary: Builds or expression data for callers.
        private static Expression BuildOrExpression(ParameterExpression parameter, OrFilter filter)
        {
            Expression? body = null;
            foreach (var childFilter in filter.Filters)
            {
                var expression = BuildExpression(parameter, childFilter);
                body = body == null ? expression : Expression.OrElse(body, expression);
            }

            // An OR group with no branches would match nothing, not everything - say so rather than guessing.
            return body ?? throw new QueryValidationException("An OR filter must contain at least one branch.");
        }

        // Function summary: Builds single expression data for callers.
        private static BinaryExpression BuildSingleExpression(ParameterExpression parameter, SingleFilter filter)
        {
            var property = QueryPropertyResolver.Resolve(parameter.Type, filter.PropertyName)
                ?? throw QueryValidationException.UnknownProperty(parameter.Type, filter.PropertyName, "filter");

            var member = Expression.Property(parameter, property);
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var constant = CreateValueExpression(filter.Value, targetType, property.PropertyType);

            return filter.Operation switch
            {
                Op.Equals => Expression.Equal(member, constant),
                Op.NotEquals or Op.NotEqual => Expression.NotEqual(member, constant),
                Op.GreaterThan => Expression.GreaterThan(member, constant),
                Op.GreaterThanOrEqual => Expression.GreaterThanOrEqual(member, constant),
                Op.LessThan => Expression.LessThan(member, constant),
                Op.LessThanOrEqual => Expression.LessThanOrEqual(member, constant),
                Op.IsNull => Expression.Equal(member, Expression.Constant(null, property.PropertyType)),
                Op.IsNotNull => Expression.NotEqual(member, Expression.Constant(null, property.PropertyType)),
                Op.Contains => BuildStringCall(member, nameof(string.Contains), filter.Value),
                Op.StartsWith => BuildStringCall(member, nameof(string.StartsWith), filter.Value),
                Op.EndsWith => BuildStringCall(member, nameof(string.EndsWith), filter.Value),
                _ => throw QueryValidationException.UnsupportedOperation(parameter.Type, filter.PropertyName, filter.Operation)
            };
        }

        // Function summary: Builds string call data for callers.
        private static BinaryExpression BuildStringCall(Expression member, string methodName, object? value)
        {
            var stringMember = member.Type == typeof(string)
                ? member
                : Expression.Call(member, nameof(object.ToString), Type.EmptyTypes);
            var notNull = Expression.NotEqual(stringMember, Expression.Constant(null, typeof(string)));
            var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
            var call = Expression.Call(stringMember, method, Expression.Constant(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
            return Expression.AndAlso(notNull, call);
        }

        // Function summary: Creates value expression data for the current workflow.
        private static Expression CreateValueExpression(object? value, Type targetType, Type propertyType)
        {
            if (value == null)
            {
                return Expression.Constant(null, propertyType);
            }

            if (targetType.IsEnum)
            {
                value = value is string text
                    ? Enum.Parse(targetType, text, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
            }
            else if (targetType == typeof(Guid))
            {
                value = value is Guid ? value : Guid.Parse(value.ToString()!);
            }
            else if (targetType != value.GetType())
            {
                value = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            var constant = Expression.Constant(value, targetType);
            return targetType == propertyType ? constant : Expression.Convert(constant, propertyType);
        }
    }
}

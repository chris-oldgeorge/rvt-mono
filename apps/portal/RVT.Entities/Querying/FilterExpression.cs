// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-07-31 pending Captured filter values so EF parameterizes them instead of inlining SQL literals.
// - 2026-06-25 pending Returned concrete string-call expressions for CA1859 analyzer cleanup.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace RVT.Entities.Querying;

public static class FilterExpression
{
    public static class ExpressionBuilder
    {
        // Function summary: Handles the t workflow for this module.
        public static Expression<Func<T, bool>> GetExpression<T>(IEnumerable<Filter> filters)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
            Expression? body = null;

            foreach (Filter filter in filters)
            {
                Expression expression = BuildExpression(parameter, filter);
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
            foreach (Filter childFilter in filter.Filters)
            {
                Expression expression = BuildExpression(parameter, childFilter);
                body = body == null ? expression : Expression.OrElse(body, expression);
            }

            // An OR group with no branches would match nothing, not everything - say so rather than guessing.
            return body ?? throw new QueryValidationException("An OR filter must contain at least one branch.");
        }

        // Function summary: Builds single expression data for callers.
        private static BinaryExpression BuildSingleExpression(ParameterExpression parameter, SingleFilter filter)
        {
            PropertyInfo property = QueryPropertyResolver.Resolve(parameter.Type, filter.PropertyName)
                ?? throw QueryValidationException.UnknownProperty(parameter.Type, filter.PropertyName, "filter");

            MemberExpression member = Expression.Property(parameter, property);
            Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            Expression constant = CreateValueExpression(filter.Value, targetType, property.PropertyType);

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
            Expression stringMember = member.Type == typeof(string)
                ? member
                : Expression.Call(member, nameof(ToString), Type.EmptyTypes);
            BinaryExpression notNull = Expression.NotEqual(stringMember, Expression.Constant(null, typeof(string)));
            MethodInfo method = typeof(string).GetMethod(methodName, [typeof(string)])!;
            MethodCallExpression call = Expression.Call(
                stringMember,
                method,
                CaptureValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, typeof(string)));
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

            Expression captured = CaptureValue(value, targetType);
            return targetType == propertyType ? captured : Expression.Convert(captured, propertyType);
        }

        /// <summary>
        /// Wraps a filter value so EF Core compiles it into a query <em>parameter</em> rather than a SQL literal.
        /// <para>
        /// A bare <c>Expression.Constant</c> is inlined: <c>WHERE m.serial_id = 'SER-123'</c>. Every distinct
        /// serial or time bound then produces distinct SQL text, so PostgreSQL plans each variant from scratch
        /// and nothing in the plan cache is ever reused - on measurement tables that is the hot read path.
        /// Reading a field off a captured object is the shape EF recognises as a closure, which is exactly what
        /// a hand-written lambda over a local variable produces: <c>WHERE m.serial_id = $1</c>.
        /// </para>
        /// <para>
        /// The NULL comparisons above deliberately stay literal: <c>= $1</c> with a null parameter never matches,
        /// while <c>IS NULL</c> is what those operations mean.
        /// </para>
        /// </summary>
        private static Expression CaptureValue(object value, Type targetType)
        {
            Type boxType = typeof(FilterValueBox<>).MakeGenericType(targetType);
            object box = Activator.CreateInstance(boxType, value)!;
            return Expression.Field(Expression.Constant(box, boxType), nameof(FilterValueBox<>.Value));
        }
    }

    /// <summary>
    /// The closure stand-in <see cref="ExpressionBuilder"/> reads filter values through. A public field, not a
    /// property: EF's parameter extraction recognises both, and a field keeps the shape identical to the
    /// compiler-generated closure classes it was written for.
    /// </summary>
    internal sealed class FilterValueBox<T>
    {
        public readonly T Value;

        // Function summary: Captures one filter value for parameterized query translation.
        public FilterValueBox(T value)
        {
            Value = value;
        }
    }
}

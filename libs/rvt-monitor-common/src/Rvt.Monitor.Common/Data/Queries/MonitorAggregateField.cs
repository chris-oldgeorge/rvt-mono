using System.Linq.Expressions;

namespace Rvt.Monitor.Common.Data.Queries;

public sealed class MonitorAggregateField<TEntity>
{
    private MonitorAggregateField(string name, Expression<Func<TEntity, double?>> selector, bool useMaximum)
    {
        Name = name;
        Selector = selector;
        UseMaximum = useMaximum;
    }

    public string Name { get; }
    public Expression<Func<TEntity, double?>> Selector { get; }
    public bool UseMaximum { get; }

    public static MonitorAggregateField<TEntity> Average(string name, Expression<Func<TEntity, double?>> selector)
    {
        return Create(name, selector, useMaximum: false);
    }

    public static MonitorAggregateField<TEntity> Maximum(string name, Expression<Func<TEntity, double?>> selector)
    {
        return Create(name, selector, useMaximum: true);
    }

    private static MonitorAggregateField<TEntity> Create(
        string name,
        Expression<Func<TEntity, double?>> selector,
        bool useMaximum)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new NotSupportedException($"Unsupported aggregate field '{name}'.");
        }

        return new MonitorAggregateField<TEntity>(name, selector, useMaximum);
    }
}

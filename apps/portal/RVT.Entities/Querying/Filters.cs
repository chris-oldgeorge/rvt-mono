// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities.Querying;

public enum Op
{
    Equals,
    NotEquals,
    NotEqual,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsNull,
    IsNotNull
}

public abstract class Filter
{
}

public class SingleFilter : Filter
{
    public string PropertyName { get; set; } = string.Empty;
    public Op Operation { get; set; }
    public object? Value { get; set; }
}

public class OrFilter : Filter
{
    public List<Filter> Filters { get; set; } = [];
}

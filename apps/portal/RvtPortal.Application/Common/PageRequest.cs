namespace RvtPortal.Application.Common;

public static class PageSortDirections
{
    public const string Ascending = "Ascending";
    public const string Descending = "Descending";

    public static string Normalize(string? value) =>
        string.Equals(value, Descending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
            ? Descending
            : Ascending;
}

public sealed record PageRequest(
    string? SearchText,
    int Page,
    int PageSize,
    string Sort,
    string SortDir);

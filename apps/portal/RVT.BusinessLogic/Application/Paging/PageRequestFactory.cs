// File summary: Normalizes transport paging and sorting inputs before they reach business-layer queries.
// Major updates:
// - 2026-07-05 pending Added shared request normalization for controller-to-business refactoring.

namespace RVT.BusinessLogic.Application.Paging;

public static class PageSortDirections
{
    public const string Ascending = "Ascending";
    public const string Descending = "Descending";

    // Function summary: Normalizes caller-supplied sort direction values to the API-compatible constants.
    public static string Normalize(string? value)
    {
        return string.Equals(value, Descending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
                ? Descending
                : Ascending;
    }
}

public static class PageRequestFactory
{
    // Function summary: Builds a normalized page request while preserving invalid-sort details for transport mapping.
    public static PageRequest Create(
        string? searchText,
        int? page,
        int? pageSize,
        string? sort,
        string? sortDir,
        string defaultSort,
        IReadOnlySet<string> allowedSorts)
    {
        var requestedSort = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort.Trim();
        if (!allowedSorts.Contains(requestedSort))
        {
            return new PageRequest(searchText, -1, -1, requestedSort, PageSortDirections.Normalize(sortDir));
        }

        var requestedPage = page.GetValueOrDefault(1);
        var requestedPageSize = pageSize.GetValueOrDefault(20);
        return new PageRequest(
            searchText,
            requestedPage <= 0 ? 1 : requestedPage,
            requestedPageSize <= 0 ? 20 : Math.Min(requestedPageSize, 100),
            requestedSort,
            PageSortDirections.Normalize(sortDir));
    }

    // Function summary: Checks whether a page request represents an unsupported sort field.
    public static bool IsInvalidSort(PageRequest request)
    {
        return request.Page == -1 && request.PageSize == -1;
    }
}

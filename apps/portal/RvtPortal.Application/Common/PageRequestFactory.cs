namespace RvtPortal.Application.Common;

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
        string requestedSort = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort.Trim();
        if (!allowedSorts.Contains(requestedSort))
        {
            return new PageRequest(searchText, -1, -1, requestedSort, PageSortDirections.Normalize(sortDir));
        }

        int requestedPage = page.GetValueOrDefault(1);
        int requestedPageSize = pageSize.GetValueOrDefault(20);
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

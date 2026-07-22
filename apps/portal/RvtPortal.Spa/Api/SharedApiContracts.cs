// File summary: Exposes API endpoints used by the React portal for shared api contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-26 pending Added disabled option metadata for archived or otherwise unavailable selections.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

namespace RvtPortal.Spa.Api;

public class GetHealthResponse
{
    public string Status { get; set; } = "ok";
    public string Application { get; set; } = "RVTmonitoring SPA API";
    public string Framework { get; set; } = "net10.0";
    public DateTime ServerTimeUtc { get; set; }
}

public class SearchLookupRequest
{
    public string? Query { get; set; }
    public Guid? CompanyId { get; set; }
    public bool? IncludeAdmin { get; set; }
    public int? Take { get; set; }

    // Function summary: Normalizes lookup result limits to the supported range.
    public int GetNormalizedTake()
    {
        var requestedTake = Take.GetValueOrDefault(20);
        return requestedTake <= 0 ? 20 : Math.Min(requestedTake, 50);
    }
}

public class SearchLookupResponse
{
    public string Kind { get; set; } = "";
    public string Query { get; set; } = "";
    public int Take { get; set; }
    public List<string> Results { get; set; } = [];
}

public static class SortDirections
{
    public const string Ascending = "Ascending";
    public const string Descending = "Descending";

    // Function summary: Normalizes sort direction aliases to the API contract values.
    public static string Normalize(string? value)
    {
        return string.Equals(value, Descending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
                ? Descending
                : Ascending;
    }
}

public class PagedQueryRequest
{
    public string? SearchText { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Sort { get; set; }
    public string SortDir { get; set; } = SortDirections.Ascending;

    // Function summary: Normalizes page numbers to the first valid page.
    public int GetNormalizedPage()
    {
        var requestedPage = Page.GetValueOrDefault(1);
        return requestedPage <= 0 ? 1 : requestedPage;
    }

    // Function summary: Normalizes page sizes to the supported range.
    public int GetNormalizedPageSize()
    {
        var requestedPageSize = PageSize.GetValueOrDefault(20);
        return requestedPageSize <= 0 ? 20 : Math.Min(requestedPageSize, 100);
    }

    // Function summary: Normalizes sort direction values for paged requests.
    public string GetNormalizedSortDir()
    {
        return SortDirections.Normalize(SortDir);
    }
}

public class PagedResponse<T>
{
    public List<T> Results { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    public string SearchText { get; set; } = "";
    public string Sort { get; set; } = "";
    public string SortDir { get; set; } = SortDirections.Ascending;
}

public class EntityResponse<T>
{
    public T? Item { get; set; }
}

public class MutationResponse
{
    public Guid? Id { get; set; }
    public string Message { get; set; } = "";
}

public class OptionItem
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Disabled { get; set; }
}

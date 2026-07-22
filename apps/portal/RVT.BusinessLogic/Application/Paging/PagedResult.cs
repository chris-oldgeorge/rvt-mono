// File summary: Holds transport-neutral paged query results returned from business-layer workflows.
// Major updates:
// - 2026-07-05 pending Added reusable paged result model for controller-to-business refactoring.

namespace RVT.BusinessLogic.Application.Paging;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasPreviousPage => Page > 1 && Total > 0;
    public bool HasNextPage => Page * PageSize < Total;
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = PageSortDirections.Ascending;
}

// File summary: Holds normalized paging, sorting, and search values for business-layer queries.
// Major updates:
// - 2026-07-05 pending Added transport-neutral paging inputs for controller-to-business refactoring.

namespace RVT.BusinessLogic.Application.Paging;

public sealed record PageRequest(
    string? SearchText,
    int Page,
    int PageSize,
    string Sort,
    string SortDir);

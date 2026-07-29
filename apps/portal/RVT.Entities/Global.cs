// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities;


public class Paging
{
    public bool Paged { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

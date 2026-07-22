// File summary: Exposes API endpoints used by the React portal for lookups controller workflows.
// Major updates:
// - 2026-07-09 pending Awaited database-backed lookup service queries and moved result limiting into the service.
// - 2026-07-09 pending Refined generated endpoint comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-25 pending Restricted cross-tenant lookups to admin roles; the lookup search is admin-only in the SPA and was leaking company/site/monitor/user directories to any authenticated user.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Application.Lookups;
using RvtPortal.Spa.Data;
namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly ILookupService lookupService;

    // Function summary: Initializes lookup endpoints with the shared lookup service.
    public LookupsController(ILookupService lookupService)
    {
        this.lookupService = lookupService;
    }
    [HttpGet("{kind}")]
    [ProducesResponseType(typeof(SearchLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    // Function summary: Returns admin-scoped lookup values for a supported lookup kind using async database queries.
    public async Task<ActionResult<SearchLookupResponse>> Search([FromRoute] string kind, [FromQuery] SearchLookupRequest request)
    {
        var query = request.Query ?? "";
        var take = request.GetNormalizedTake();
        var normalizedKind = kind.Trim().ToLowerInvariant();
        var values = normalizedKind switch
        {
            "companies" => await lookupService.CompaniesSearchAsync(query, take, HttpContext.RequestAborted),
            "contracts" => await lookupService.ContractsSearchAsync(query, take, HttpContext.RequestAborted),
            "sites" => await lookupService.SitesSearchAsync(query, take, HttpContext.RequestAborted),
            "monitors" => await lookupService.MonitorsSearchAsync(query, take, HttpContext.RequestAborted),
            "monitors-new" => await lookupService.MonitorsNewSearchAsync(query, take, HttpContext.RequestAborted),
            "monitors-online" => await lookupService.MonitorsOnlineSearchAsync(query, take, HttpContext.RequestAborted),
            "monitors-offline" => await lookupService.MonitorsOfflineSearchAsync(query, take, HttpContext.RequestAborted),
            "users" when request.CompanyId.HasValue => await lookupService.UserSearchAsync(request.CompanyId.Value, query, take, request.IncludeAdmin == true, HttpContext.RequestAborted),
            "users" => await lookupService.UserSearchAsync(query, take, HttpContext.RequestAborted),
            _ => null
        };
        if (values == null)
        {
            return NotFound(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Unknown lookup kind",
                $"Unknown lookup kind '{kind}'."));
        }
        return new SearchLookupResponse
        {
            Kind = normalizedKind,
            Query = query,
            Take = take,
            Results = values
        };
    }
}

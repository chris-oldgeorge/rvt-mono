// File summary: Exposes API endpoints used by the React portal for company workflows.
// Major updates:
// - 2026-07-09 pending Routed company lifecycle writes through the company application service.
// - 2026-07-09 pending Routed company list/detail reads through the company application service.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-26 pending Routed company lifecycle writes through transactional MediatR commands.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Application.Companies;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyApplicationService companies;

    // Function summary: Initializes this HTTP adapter with company application workflows.
    public CompaniesController(ICompanyApplicationService companies)
    {
        this.companies = companies;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryCompaniesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    // Function summary: Queries companies through the company application service.
    public async Task<ActionResult<QueryCompaniesResponse>> Query([FromQuery] QueryCompaniesRequest request)
    {
        var result = await companies.Query(
            new CompanyQuery(
                request.SearchText,
                request.Sort,
                request.GetNormalizedSortDir(),
                request.GetNormalizedPage(),
                request.GetNormalizedPageSize()),
            HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(result.InvalidSort))
        {
            return InvalidSort(result.InvalidSort, result.AllowedSortFields);
        }
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return BadRequest(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Company search failed",
                result.ErrorMessage));
        }

        return result.Response!;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<CompanyDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves company detail by id.
    public async Task<ActionResult<EntityResponse<CompanyDetailResponse>>> Get(Guid id)
    {
        var company = await companies.GetAsync(id, HttpContext.RequestAborted);
        return company == null ? CompanyNotFound(id) : new EntityResponse<CompanyDetailResponse> { Item = company };
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityResponse<CompanyDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates a company through the company application service.
    public async Task<ActionResult<EntityResponse<CompanyDetailResponse>>> Create(CompanyMutationRequest request)
    {
        var result = await companies.CreateAsync(request, HttpContext.RequestAborted);
        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || !result.CompanyId.HasValue || result.Company == null)
        {
            return ValidationProblem(ModelState);
        }

        var response = new EntityResponse<CompanyDetailResponse>
        {
            Item = result.Company
        };

        return CreatedAtAction(nameof(Get), new { id = result.CompanyId.Value }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<CompanyDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates a company through the company application service.
    public async Task<ActionResult<EntityResponse<CompanyDetailResponse>>> Update(Guid id, CompanyMutationRequest request)
    {
        var result = await companies.UpdateAsync(id, request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return CompanyNotFound(id);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<CompanyDetailResponse>
        {
            Item = result.Company!
        };
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes a company through the company application service.
    public async Task<ActionResult<MutationResponse>> Delete(Guid id)
    {
        var result = await companies.DeleteAsync(id, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return CompanyNotFound(id);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return new MutationResponse
        {
            Id = id,
            Message = $"Company '{result.CompanyName}' has been deleted."
        };
    }

    // Function summary: Builds the invalid-sort problem response while preserving the existing company contract.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for companies.");
        problem.Extensions["allowedSortFields"] = allowedSortFields.ToArray();
        return BadRequest(problem);
    }

    // Function summary: Builds the company not-found response.
    private NotFoundObjectResult CompanyNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Company not found",
            $"Company '{id}' was not found."));
    }

    // Function summary: Maps command validation errors into the API model-state response.
    private void AddCommandErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }
    }
}

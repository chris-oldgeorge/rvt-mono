// File summary: Exposes API endpoints used by the React portal for contract workflows.
// Major updates:
// - 2026-07-09 pending Routed contract lifecycle writes through the contract application service.
// - 2026-07-09 pending Routed contract list/detail/options reads through the contract application service.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-26 pending Routed contract write workflows through transactional MediatR commands.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Application.Contracts;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/contracts")]
public class ContractsController : ControllerBase
{
    private readonly IContractApplicationService contracts;

    // Function summary: Initializes this HTTP adapter with contract application workflows.
    public ContractsController(IContractApplicationService contracts)
    {
        this.contracts = contracts;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryContractsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries contracts through the contract application service.
    public async Task<ActionResult<QueryContractsResponse>> Query([FromQuery] QueryContractsRequest request)
    {
        var result = await contracts.QueryAsync(
            new ContractQuery(
                request.CompanyId,
                request.SiteId,
                request.SearchText,
                request.Sort,
                request.GetNormalizedSortDir(),
                request.GetNormalizedPage(),
                request.GetNormalizedPageSize()),
            HttpContext.RequestAborted);
        return !string.IsNullOrWhiteSpace(result.InvalidSort)
            ? InvalidSort(result.InvalidSort, result.AllowedSortFields)
            : result.Response!;
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(ContractOptionsResponse), StatusCodes.Status200OK)]
    // Function summary: Returns contract edit options through the contract application service.
    public async Task<ActionResult<ContractOptionsResponse>> Options([FromQuery] Guid? companyId = null)
    {
        return await contracts.OptionsAsync(companyId, HttpContext.RequestAborted);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ContractDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves contract detail by id.
    public async Task<ActionResult<EntityResponse<ContractDetailResponse>>> Get(Guid id)
    {
        var contract = await contracts.GetAsync(id, HttpContext.RequestAborted);
        return contract == null ? ContractNotFound(id) : new EntityResponse<ContractDetailResponse> { Item = contract };
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityResponse<ContractDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates a contract through the contract application service.
    public async Task<ActionResult<EntityResponse<ContractDetailResponse>>> Create(ContractMutationRequest request)
    {
        var result = await contracts.CreateAsync(request, HttpContext.RequestAborted);
        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || !result.ContractId.HasValue || result.Contract == null)
        {
            return ValidationProblem(ModelState);
        }

        var response = new EntityResponse<ContractDetailResponse>
        {
            Item = result.Contract
        };
        return CreatedAtAction(nameof(Get), new { id = result.ContractId.Value }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<ContractDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates a contract through the contract application service.
    public async Task<ActionResult<EntityResponse<ContractDetailResponse>>> Update(Guid id, ContractMutationRequest request)
    {
        var result = await contracts.UpdateAsync(id, request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return ContractNotFound(id);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<ContractDetailResponse>
        {
            Item = result.Contract!
        };
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes a contract through the contract application service.
    public async Task<ActionResult<MutationResponse>> Delete(Guid id)
    {
        var result = await contracts.DeleteAsync(id, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return ContractNotFound(id);
        }

        return new MutationResponse
        {
            Id = id,
            Message = $"Contract '{result.ContractNumber}' has been deleted."
        };
    }

    // Function summary: Builds the invalid-sort problem response while preserving the existing contract API shape.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for contracts.");
        problem.Extensions["allowedSortFields"] = allowedSortFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase).ToArray();
        return BadRequest(problem);
    }

    // Function summary: Builds the contract not-found response.
    private NotFoundObjectResult ContractNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Contract not found",
            $"Contract '{id}' was not found."));
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

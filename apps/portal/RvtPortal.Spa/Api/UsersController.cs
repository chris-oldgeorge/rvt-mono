// File summary: Exposes API endpoints used by the React portal for users controller workflows.
// Major updates:
// - 2026-07-09 pending Refined generated endpoint comments after controller workflow cleanup.
// - 2026-07-09 pending Routed user account lifecycle and site-assignment write orchestration through an application service.
// - 2026-07-09 pending Routed user detail, options, and site-assignment read models through an application service.
// - 2026-07-08 pending Routed the admin user list query through an application service.
// - 2026-06-29 Routed user email diagnostics through injected EmailSender logging.
// - 2026-06-25 pending Narrowed private user item lookup parameter types for CA1859 cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-26 pending Routed admin user account lifecycle writes through transactional MediatR commands.
// - 2026-06-26 pending Routed user site-contact and site-removal writes through transactional MediatR commands.
// - 2026-06-26 pending Routed user site-assignment writes through transactional MediatR commands.
// - 2026-06-26 pending Used configured SPA public base URL for admin-sent account links and company-scoped installer accounts.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application.Paging;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Application.Users;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserAdministrationReadService userReads;
    private readonly IUserListApplicationService userLists;
    private readonly IUserAccountWorkflowService userAccounts;

    // Function summary: Initializes this HTTP adapter with user read use cases and account workflow orchestration.
    public UsersController(
        IUserAdministrationReadService userReads,
        IUserListApplicationService userLists,
        IUserAccountWorkflowService userAccounts)
    {
        this.userReads = userReads;
        this.userLists = userLists;
        this.userAccounts = userAccounts;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries admin users through the application-layer user list use case.
    public async Task<ActionResult<QueryUsersResponse>> Query([FromQuery] QueryUsersRequest request)
    {
        var page = PageRequestFactory.Create(
            request.SearchText,
            request.Page,
            request.PageSize,
            request.Sort,
            request.SortDir,
            UserListApplicationService.DefaultSort,
            UserListApplicationService.SortFields);
        if (PageRequestFactory.IsInvalidSort(page))
        {
            return InvalidUserSort(page.Sort);
        }

        var result = await userLists.QueryAsync(
            new UserListQuery(request.CompanyId, page, BuildUserListActor()),
            HttpContext.RequestAborted);
        return UserApiMapper.ToQueryResponse(result);
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(UserDetailResponse), StatusCodes.Status200OK)]
    // Function summary: Returns user form options through the user administration read service.
    public async Task<ActionResult<UserDetailResponse>> Options()
    {
        var options = await userReads.OptionsAsync(BuildUserListActor(), HttpContext.RequestAborted);
        return UserApiMapper.ToOptionsResponse(options);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EntityResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns one user detail response through the user administration read service.
    public async Task<ActionResult<EntityResponse<UserDetailResponse>>> Get(string id)
    {
        var detail = await ReadUserDetailAsync(id);
        if (detail is null)
        {
            return UserNotFound(id);
        }

        return new EntityResponse<UserDetailResponse>
        {
            Item = detail
        };
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityResponse<UserDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates an admin-managed user through the user account workflow service.
    public async Task<ActionResult<EntityResponse<UserDetailResponse>>> Create(UserMutationRequest request)
    {
        var result = await userAccounts.CreateAsync(
            request,
            BuildUserListActor(),
            BuildRequestOrigin(),
            HttpContext.RequestAborted);
        if (AddModelErrorsAndHasAny(result.Errors))
        {
            return ValidationProblem(ModelState);
        }
        if (result.NotFound || result.Detail == null || string.IsNullOrWhiteSpace(result.UserId))
        {
            return UserNotFound(result.UserId ?? request.Email);
        }

        return CreatedAtAction(nameof(Get), new { id = result.UserId }, new EntityResponse<UserDetailResponse>
        {
            Item = UserApiMapper.ToDetailResponse(result.Detail)
        });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EntityResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates an admin-managed user through the user account workflow service.
    public async Task<ActionResult<EntityResponse<UserDetailResponse>>> Update(string id, UserMutationRequest request)
    {
        var result = await userAccounts.UpdateAsync(id, request, BuildUserListActor(), HttpContext.RequestAborted);
        return UserDetailResult(result, id);
    }

    [HttpPost("{id}/resend-confirmation")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Sends a new account-confirmation link for an admin-managed user.
    public async Task<ActionResult<MessageResponse>> ResendConfirmation(string id)
    {
        var result = await userAccounts.ResendConfirmationAsync(id, BuildRequestOrigin(), HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return UserNotFound(id);
        }

        return new MessageResponse { Message = "Sent" };
    }

    [HttpPost("{id}/reset-password-link")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Sends a password-reset link for an admin-managed user.
    public async Task<ActionResult<MessageResponse>> SendResetPasswordLink(string id)
    {
        var result = await userAccounts.SendResetPasswordLinkAsync(id, BuildRequestOrigin(), HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return UserNotFound(id);
        }

        return new MessageResponse { Message = "Sent" };
    }

    [HttpPost("{id}/disable")]
    [ProducesResponseType(typeof(EntityResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Disables an admin-managed user account.
    public async Task<ActionResult<EntityResponse<UserDetailResponse>>> Disable(string id)
    {
        var result = await userAccounts.DisableAsync(id, BuildUserListActor(), HttpContext.RequestAborted);
        return UserDetailResult(result, id);
    }

    [HttpPost("{id}/enable")]
    [ProducesResponseType(typeof(EntityResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Enables an admin-managed user account.
    public async Task<ActionResult<EntityResponse<UserDetailResponse>>> Enable(string id)
    {
        var result = await userAccounts.EnableAsync(id, BuildUserListActor(), HttpContext.RequestAborted);
        return UserDetailResult(result, id);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes an admin-managed user account when the actor is permitted.
    public async Task<ActionResult<MutationResponse>> Delete(string id)
    {
        var result = await userAccounts.DeleteAsync(id, BuildUserListActor(), HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return UserNotFound(id);
        }
        if (result.Forbidden)
        {
            return ForbiddenForTarget();
        }
        if (AddModelErrorsAndHasAny(result.Errors))
        {
            return ValidationProblem(ModelState);
        }

        return new MutationResponse
        {
            Id = Guid.TryParse(id, out var parsedId) ? parsedId : null,
            Message = $"User '{result.Email}' has been deleted."
        };
    }

    [HttpGet("site-assignments/{siteId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<SiteAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns users currently assigned to a site.
    public async Task<ActionResult<EntityResponse<SiteAssignmentResponse>>> GetSiteAssignments(Guid siteId)
    {
        var response = await ReadSiteAssignmentsAsync(siteId);
        if (response == null)
        {
            return SiteNotFound(siteId);
        }

        return new EntityResponse<SiteAssignmentResponse> { Item = response };
    }

    [HttpPost("site-assignments")]
    [ProducesResponseType(typeof(EntityResponse<SiteAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Assigns a user to a site through the user account workflow service.
    public async Task<ActionResult<EntityResponse<SiteAssignmentResponse>>> AddToSite(SiteUserMutationRequest request)
    {
        var result = await userAccounts.AddToSiteAsync(request, BuildUserListActor(), HttpContext.RequestAborted);
        return SiteAssignmentResult(result, request.SiteId, request.UserId);
    }

    [HttpPost("site-assignments/contact")]
    [ProducesResponseType(typeof(EntityResponse<SiteAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Marks a site-assigned user as a site contact.
    public async Task<ActionResult<EntityResponse<SiteAssignmentResponse>>> SetSiteContact(SiteUserMutationRequest request)
    {
        var result = await userAccounts.SetSiteContactAsync(request, BuildUserListActor(), HttpContext.RequestAborted);
        return SiteAssignmentResult(result, request.SiteId, request.UserId);
    }

    [HttpDelete("site-assignments/contact/{siteId:guid}/{userId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<SiteAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Removes the site-contact flag from a site-assigned user.
    public async Task<ActionResult<EntityResponse<SiteAssignmentResponse>>> RemoveSiteContact(Guid siteId, Guid userId)
    {
        var result = await userAccounts.RemoveSiteContactAsync(siteId, userId, BuildUserListActor(), HttpContext.RequestAborted);
        return SiteAssignmentResult(result, siteId, userId);
    }

    [HttpDelete("site-assignments/{siteId:guid}/{userId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<SiteAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Removes a user's assignment from a site.
    public async Task<ActionResult<EntityResponse<SiteAssignmentResponse>>> RemoveFromSite(Guid siteId, Guid userId)
    {
        var result = await userAccounts.RemoveFromSiteAsync(siteId, userId, BuildUserListActor(), HttpContext.RequestAborted);
        return SiteAssignmentResult(result, siteId, userId);
    }

    // Function summary: Reads and maps one user detail through the application read service.
    private async Task<UserDetailResponse?> ReadUserDetailAsync(string id)
    {
        var model = await userReads.GetDetailAsync(id, BuildUserListActor(), HttpContext.RequestAborted);
        return model is null ? null : UserApiMapper.ToDetailResponse(model);
    }

    // Function summary: Reads and maps one site-assignment response through the application read service.
    private async Task<SiteAssignmentResponse?> ReadSiteAssignmentsAsync(Guid siteId)
    {
        var model = await userReads.GetSiteAssignmentsAsync(siteId, BuildUserListActor(), HttpContext.RequestAborted);
        return model is null ? null : UserApiMapper.ToSiteAssignmentResponse(model);
    }

    // Function summary: Maps a user workflow result into the existing user detail API response contract.
    private ActionResult<EntityResponse<UserDetailResponse>> UserDetailResult(UserAccountWorkflowResult result, string id)
    {
        if (result.NotFound)
        {
            return UserNotFound(id);
        }
        if (result.Forbidden)
        {
            return ForbiddenForTarget();
        }
        if (AddModelErrorsAndHasAny(result.Errors))
        {
            return ValidationProblem(ModelState);
        }
        if (result.Detail == null)
        {
            return UserNotFound(id);
        }

        return new EntityResponse<UserDetailResponse>
        {
            Item = UserApiMapper.ToDetailResponse(result.Detail)
        };
    }

    // Function summary: Maps a site-assignment workflow result into the existing assignment API response contract.
    private ActionResult<EntityResponse<SiteAssignmentResponse>> SiteAssignmentResult(
        SiteAssignmentWorkflowResult result,
        Guid siteId,
        Guid userId)
    {
        if (result.UserNotFound)
        {
            return UserNotFound(userId.ToString());
        }
        if (result.SiteNotFound || result.Assignment == null)
        {
            return SiteNotFound(siteId);
        }
        if (AddModelErrorsAndHasAny(result.Errors))
        {
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<SiteAssignmentResponse>
        {
            Item = UserApiMapper.ToSiteAssignmentResponse(result.Assignment)
        };
    }

    // Function summary: Copies workflow validation errors into MVC model state and reports whether any were added.
    private bool AddModelErrorsAndHasAny(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }

        return !ModelState.IsValid;
    }

    // Function summary: Builds the current admin actor used by user application services.
    private UserListActor BuildUserListActor()
    {
        return new UserListActor(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.IsInRole(RoleNames.RVTMasterAdmin),
            User.IsInRole(RoleNames.RVTAdmin));
    }

    // Function summary: Captures the current request origin for account links without passing HTTP types into application services.
    private UserAccountRequestOrigin BuildRequestOrigin()
    {
        return new UserAccountRequestOrigin(
            Request.Scheme,
            Request.Host.ToString(),
            Request.PathBase.ToString());
    }

    // Function summary: Builds the existing invalid-sort problem response for unsupported user list sort fields.
    private BadRequestObjectResult InvalidUserSort(string requestedSort)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for users.");
        problem.Extensions["allowedSortFields"] = UserListApplicationService.SortAliases.Keys
            .Where(key => key[0] == char.ToLowerInvariant(key[0]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return BadRequest(problem);
    }

    // Function summary: Builds the existing not-found response for missing user records.
    private NotFoundObjectResult UserNotFound(string id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "User not found",
            $"User '{id}' was not found."));
    }

    // Function summary: Builds the existing not-found response for missing site records.
    private NotFoundObjectResult SiteNotFound(Guid siteId)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Site not found",
            $"Site '{siteId}' was not found."));
    }

    // Function summary: Builds the existing forbidden response for protected target-user operations.
    private ObjectResult ForbiddenForTarget()
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiProblems.Create(
            HttpContext,
            StatusCodes.Status403Forbidden,
            "Permission required",
            "Your role does not have permission to manage this user."));
    }

}

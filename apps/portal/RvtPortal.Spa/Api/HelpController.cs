// File summary: Exposes Help/FAQ CMS endpoints for published browsing and admin content management.
// Major updates:
// - 2026-07-09 pending Routed Help CMS article writes through the Help application service.
// - 2026-07-09 pending Routed Help CMS reads through the Help application service.
// - 2026-06-10 pending Added admin Help CMS listing, editing, publishing, and removal endpoints.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added Help CMS article creation, listing, and detail endpoints.
// - 2026-06-26 pending Routed Help CMS article writes through transactional MediatR commands.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Application.Help;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/help")]
public class HelpController : ControllerBase
{
    private readonly IHelpApplicationService help;

    // Function summary: Initializes this HTTP adapter with Help application workflows.
    public HelpController(IHelpApplicationService help)
    {
        this.help = help;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HelpOverviewResponse), StatusCodes.Status200OK)]
    // Function summary: Retrieves published Help CMS sections and articles for users.
    public async Task<ActionResult<HelpOverviewResponse>> Query([FromQuery] string? searchText = null)
    {
        var overview = await help.QueryAsync(searchText, HttpContext.RequestAborted);
        return HelpApiMapper.ToOverviewResponse(overview);
    }

    [HttpGet("articles/{slug}")]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves a published Help CMS article by slug.
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> GetArticle(string slug)
    {
        var article = await help.GetPublishedArticleAsync(slug, HttpContext.RequestAborted);
        if (article == null)
        {
            return HelpNotFound(slug);
        }

        return new EntityResponse<HelpArticleResponse>
        {
            Item = HelpApiMapper.ToArticleResponse(article)
        };
    }

    [HttpPost("articles")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates a Help CMS article through the Help application service.
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> CreateArticle(HelpArticleMutationRequest request)
    {
        var result = await help.CreateArticleAsync(request, HttpContext.RequestAborted);
        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid || !result.ArticleId.HasValue || result.Article == null)
        {
            return ValidationProblem(ModelState);
        }

        var response = new EntityResponse<HelpArticleResponse> { Item = HelpApiMapper.ToArticleResponse(result.Article) };
        return CreatedAtAction(nameof(GetArticle), new { slug = result.Slug }, response);
    }

    [HttpGet("admin")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(HelpAdminOverviewResponse), StatusCodes.Status200OK)]
    // Function summary: Retrieves all Help CMS content for admin management.
    public async Task<ActionResult<HelpAdminOverviewResponse>> QueryAdmin(
        [FromQuery] string? searchText = null,
        [FromQuery] string? status = null,
        [FromQuery] string? contentType = null)
    {
        var overview = await help.QueryAdminAsync(searchText, status, contentType, HttpContext.RequestAborted);
        return HelpApiMapper.ToAdminOverviewResponse(overview);
    }

    [HttpGet("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves a Help CMS article by id for admin editing.
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> GetAdminArticle(Guid id)
    {
        var article = await help.GetAdminArticleAsync(id, HttpContext.RequestAborted);
        if (article == null)
        {
            return HelpNotFound(id);
        }

        return new EntityResponse<HelpArticleResponse>
        {
            Item = HelpApiMapper.ToArticleResponse(article)
        };
    }

    [HttpPut("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates a Help CMS article through the Help application service.
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> UpdateArticle(Guid id, HelpArticleMutationRequest request)
    {
        var result = await help.UpdateArticleAsync(id, request, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return HelpNotFound(id);
        }

        AddCommandErrors(result.Errors);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (result.Article == null)
        {
            return HelpNotFound(id);
        }

        return new EntityResponse<HelpArticleResponse>
        {
            Item = HelpApiMapper.ToArticleResponse(result.Article)
        };
    }

    [HttpPost("admin/articles/{id:guid}/publication")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Publishes or unpublishes a Help CMS article through the Help application service.
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> SetArticlePublication(Guid id, HelpPublishRequest request)
    {
        var result = await help.SetArticlePublicationAsync(id, request.IsPublished, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return HelpNotFound(id);
        }

        if (result.Article == null)
        {
            return HelpNotFound(id);
        }

        return new EntityResponse<HelpArticleResponse>
        {
            Item = HelpApiMapper.ToArticleResponse(result.Article)
        };
    }

    [HttpDelete("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Removes a Help CMS article through the Help application service.
    public async Task<ActionResult<MutationResponse>> DeleteArticle(Guid id)
    {
        var result = await help.DeleteArticleAsync(id, HttpContext.RequestAborted);
        if (result.NotFound)
        {
            return HelpNotFound(id);
        }

        return new MutationResponse
        {
            Id = id,
            Message = "Help article removed."
        };
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

    // Function summary: Handles the help not found workflow.
    private NotFoundObjectResult HelpNotFound(string slug)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Help article not found",
            $"Help article '{slug}' was not found."));
    }

    // Function summary: Handles the help not found workflow for id-based admin requests.
    private NotFoundObjectResult HelpNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Help article not found",
            $"Help article '{id}' was not found."));
    }
}

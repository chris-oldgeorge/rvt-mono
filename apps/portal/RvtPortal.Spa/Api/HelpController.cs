// File summary: Adapts Help/FAQ HTTP requests to transport-neutral application use cases.
// Major updates:
// - 2026-07-28 Routed every Help workflow through the standalone application boundary and canonical admin routes.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Application.Common;
using RvtPortal.Application.Help;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/help")]
public sealed class HelpController(
    IHelpApplicationService help,
    ICurrentUserContextFactory currentUserContextFactory,
    IApiResultMapper resultMapper) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HelpOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HelpOverviewResponse>> Query(
        [FromQuery] string? searchText = null)
    {
        var actor = await CurrentActorAsync();
        var result = await help.QueryPublishedAsync(
            actor,
            searchText,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            HelpApiMapper.ToOverviewResponse);
    }

    [HttpGet("articles/{slug}")]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> GetArticle(
        string slug)
    {
        var actor = await CurrentActorAsync();
        var result = await help.GetPublishedArticleAsync(
            actor,
            slug,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            ToEntityResponse);
    }

    [HttpPost("admin/articles")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> CreateArticle(
        HelpArticleMutationRequest request)
    {
        var actor = await CurrentActorAsync();
        var result = await help.CreateAsync(
            actor,
            HelpApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        if (result.Kind == UseCaseResultKind.Success && result.Value is not null)
        {
            var response = ToEntityResponse(result.Value);
            return CreatedAtAction(
                nameof(GetAdminArticle),
                new { id = result.Value.Id },
                response);
        }

        return resultMapper.ToActionResult(
            this,
            result,
            ToEntityResponse);
    }

    [HttpGet("admin")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(HelpAdminOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HelpAdminOverviewResponse>> QueryAdmin(
        [FromQuery] string? searchText = null,
        [FromQuery] string? status = null,
        [FromQuery] string? contentType = null)
    {
        var actor = await CurrentActorAsync();
        var result = await help.QueryAdminAsync(
            actor,
            HelpApiMapper.ToAdminQuery(searchText, status, contentType),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            HelpApiMapper.ToAdminOverviewResponse);
    }

    [HttpGet("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> GetAdminArticle(
        Guid id)
    {
        var actor = await CurrentActorAsync();
        var result = await help.GetAdminArticleAsync(
            actor,
            id,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            ToEntityResponse);
    }

    [HttpPut("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> UpdateArticle(
        Guid id,
        HelpArticleMutationRequest request)
    {
        var actor = await CurrentActorAsync();
        var result = await help.UpdateAsync(
            actor,
            id,
            HelpApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            ToEntityResponse);
    }

    [HttpPost("admin/articles/{id:guid}/publication")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<HelpArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntityResponse<HelpArticleResponse>>> SetArticlePublication(
        Guid id,
        HelpPublishRequest request)
    {
        var actor = await CurrentActorAsync();
        var result = await help.SetPublicationAsync(
            actor,
            id,
            request.IsPublished,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            ToEntityResponse);
    }

    [HttpDelete("admin/articles/{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(MutationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MutationResponse>> DeleteArticle(Guid id)
    {
        var actor = await CurrentActorAsync();
        var result = await help.DeleteAsync(
            actor,
            id,
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            deleted => new MutationResponse
            {
                Id = deleted.ArticleId,
                Message = "Help article removed."
            });
    }

    private Task<RvtPortal.Application.Identity.PortalUserContext>
        CurrentActorAsync() =>
        currentUserContextFactory.CreateAsync(
            User,
            HttpContext.RequestAborted);

    private static EntityResponse<HelpArticleResponse> ToEntityResponse(
        HelpArticleModel article) =>
        new()
        {
            Item = HelpApiMapper.ToArticleResponse(article)
        };
}

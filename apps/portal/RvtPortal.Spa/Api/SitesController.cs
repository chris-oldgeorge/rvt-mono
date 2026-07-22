// File summary: Exposes API endpoints used by the React portal for sites controller workflows.
// Major updates:
// - 2026-07-09 pending Refined generated endpoint comments after controller workflow cleanup.
// - 2026-07-09 pending Routed site mutation and notification-setting writes through the site application service.
// - 2026-07-09 pending Routed detail, monitor, notification, and option reads through the site application service.
// - 2026-07-08 pending Routed the site list query through the business application service.
// - 2026-07-08 pending Routed customer-logo uploads through transport-neutral storage ports.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Added monitor coordinates to site detail rows for embedded map parity.
// - 2026-06-24 pending Added customer logo upload, preview, and delete endpoints for report branding.
// - 2026-06-26 pending Routed site write workflows through transactional MediatR commands.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Ports.Storage;
using RVT.BusinessLogic.Sites;
using RvtPortal.Spa.Application.Sites;
using RvtPortal.Spa.Adapters.Storage;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Data;
namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/sites")]
public class SitesController : ControllerBase
{
    private readonly ISiteApplicationService sites;
    private readonly ICurrentUserContextFactory currentUserContextFactory;
    private readonly IApiResultMapper resultMapper;
    private readonly ICustomerLogoStorage customerLogoStorage;

    // Function summary: Initializes this HTTP adapter with site use cases and storage ports.
    public SitesController(
        ISiteApplicationService sites,
        ICurrentUserContextFactory currentUserContextFactory,
        IApiResultMapper resultMapper,
        ICustomerLogoStorage customerLogoStorage)
    {
        this.sites = sites;
        this.currentUserContextFactory = currentUserContextFactory;
        this.resultMapper = resultMapper;
        this.customerLogoStorage = customerLogoStorage;
    }
    [HttpGet]
    [ProducesResponseType(typeof(QuerySitesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries visible sites through the business-layer site use case.
    public async Task<ActionResult<QuerySitesResponse>> Query([FromQuery] QuerySitesRequest request)
    {
        var page = BuildSitePageRequest(request);
        if (PageRequestFactory.IsInvalidSort(page))
        {
            return InvalidSort(page.Sort, SiteApplicationService.SortFields);
        }

        var user = await currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
        var result = await sites.QueryAsync(
            user,
            new SiteQuery(request.CompanyId, request.IncludeArchived, page),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            value => SiteApiMapper.ToQueryResponse(value, user.IsCompanyUser && !user.IsAdmin));
    }
    [HttpGet("options")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(SiteOptionsResponse), StatusCodes.Status200OK)]
    // Function summary: Returns site form options for the selected company context.
    public async Task<ActionResult<SiteOptionsResponse>> Options([FromQuery] Guid? companyId = null)
    {
        var result = await sites.OptionsAsync(companyId, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, SiteApiMapper.ToOptionsResponse);
    }
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns one authorized site detail response.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> Get(Guid id)
    {
        var result = await sites.GetAsync(await CreateUserContextAsync(), id, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            model => new EntityResponse<SiteDetailResponse> { Item = ToSiteDetailResponse(model) });
    }
    [HttpPost("{id:guid}/customer-logo")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [RequestSizeLimit(2 * 1024 * 1024 + 32 * 1024)]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Uploads or replaces the customer logo used by reports for a site.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> UploadCustomerLogo(Guid id, IFormFile? logo, CancellationToken cancellationToken)
    {
        var user = await CreateUserContextAsync();
        if (!await sites.CanManageSiteAsync(user, id, cancellationToken))
        {
            return SiteNotFound(id);
        }

        if (logo == null)
        {
            return BadRequest(new ProblemDetails { Title = "Customer logo required", Detail = "Choose a logo image before uploading." });
        }

        try
        {
            await customerLogoStorage.SaveAsync(id, new FormFileUpload(logo), cancellationToken);
        }
        catch (StorageValidationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid customer logo", Detail = ex.Message });
        }

        var detail = await ReadSiteDetailAsync(id, user, cancellationToken);
        if (detail == null)
        {
            return SiteNotFound(id);
        }

        return new EntityResponse<SiteDetailResponse>
        {
            Item = detail
        };
    }

    [HttpDelete("{id:guid}/customer-logo")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Deletes the customer logo used by reports for a site.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> DeleteCustomerLogo(Guid id, CancellationToken cancellationToken)
    {
        var user = await CreateUserContextAsync();
        if (!await sites.CanManageSiteAsync(user, id, cancellationToken))
        {
            return SiteNotFound(id);
        }

        await customerLogoStorage.DeleteAsync(id, cancellationToken);
        var detail = await ReadSiteDetailAsync(id, user, cancellationToken);
        if (detail == null)
        {
            return SiteNotFound(id);
        }

        return new EntityResponse<SiteDetailResponse>
        {
            Item = detail
        };
    }

    [HttpGet("{id:guid}/customer-logo")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Streams a protected customer logo to users who can read the site.
    public async Task<IActionResult> CustomerLogo(Guid id, CancellationToken cancellationToken)
    {
        if (!await sites.CanReadSiteAsync(await CreateUserContextAsync(), id, cancellationToken))
        {
            return SiteNotFound(id);
        }

        var logo = await customerLogoStorage.OpenReadAsync(id, cancellationToken);
        return logo == null
            ? SiteNotFound(id)
            : File(logo.Stream, logo.ContentType, logo.FileName);
    }

    [HttpPost]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Creates a site through the site application service.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> Create(SiteMutationRequest request)
    {
        var result = await sites.CreateAsync(
            await CreateUserContextAsync(),
            SiteApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        if (result.Kind != ApplicationResultKind.Success || result.Value == null)
        {
            return resultMapper.ToActionResult(this, result, ToSiteDetailEntity);
        }

        var response = ToSiteDetailEntity(result.Value);
        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, response);
    }
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates a site through the site application service.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> Update(Guid id, SiteMutationRequest request)
    {
        var result = await sites.UpdateAsync(
            await CreateUserContextAsync(),
            id,
            SiteApiMapper.ToMutation(request),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToSiteDetailEntity);
    }
    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = RoleAuthorization.AdminRoles)]
    [ProducesResponseType(typeof(EntityResponse<SiteDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Archives a site through the site application service.
    public async Task<ActionResult<EntityResponse<SiteDetailResponse>>> Archive(Guid id)
    {
        var user = await CreateUserContextAsync();
        var result = await sites.ArchiveAsync(
            user,
            id,
            user.UserName ?? user.UserId?.ToString() ?? "SPA API",
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, ToSiteDetailEntity);
    }
    [HttpGet("{id:guid}/monitors")]
    [ProducesResponseType(typeof(QuerySiteMonitorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns the paged monitor panel for one authorized site.
    public async Task<ActionResult<QuerySiteMonitorsResponse>> Monitors(Guid id, [FromQuery] PagedQueryRequest request)
    {
        var page = BuildFixedSortPageRequest(request, SiteApplicationService.MonitorSort);
        var result = await sites.QueryMonitorsAsync(await CreateUserContextAsync(), id, page, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, SiteApiMapper.ToMonitorQueryResponse);
    }
    [HttpGet("{id:guid}/notifications/open")]
    [ProducesResponseType(typeof(QuerySiteNotificationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns the paged open-notification panel for one authorized site.
    public async Task<ActionResult<QuerySiteNotificationsResponse>> OpenNotifications(Guid id, [FromQuery] PagedQueryRequest request)
    {
        var page = BuildFixedSortPageRequest(request, SiteApplicationService.NotificationSort);
        var result = await sites.QueryOpenNotificationsAsync(await CreateUserContextAsync(), id, page, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, SiteApiMapper.ToNotificationQueryResponse);
    }
    [HttpGet("{id:guid}/notification-settings")]
    [ProducesResponseType(typeof(SiteNotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns notification settings for one authorized site.
    public async Task<ActionResult<SiteNotificationSettingsResponse>> NotificationSettings(Guid id)
    {
        var result = await sites.GetNotificationSettingsAsync(await CreateUserContextAsync(), id, HttpContext.RequestAborted);
        return resultMapper.ToActionResult(this, result, SiteApiMapper.ToNotificationSettingsResponse);
    }
    [HttpPut("{siteId:guid}/notification-settings/{siteUserId:guid}")]
    [ProducesResponseType(typeof(EntityResponse<SiteNotificationSettingItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Updates notification settings through the site application service.
    public async Task<ActionResult<EntityResponse<SiteNotificationSettingItem>>> UpdateNotificationSetting(Guid siteId, Guid siteUserId, SiteNotificationSettingMutationRequest request)
    {
        var user = await CreateUserContextAsync();
        var result = await sites.UpdateNotificationSettingAsync(
            user,
            siteId,
            siteUserId,
            SiteApiMapper.ToNotificationSettingMutation(request),
            HttpContext.RequestAborted);
        return resultMapper.ToActionResult(
            this,
            result,
            model => new EntityResponse<SiteNotificationSettingItem>
            {
                Item = SiteApiMapper.ToNotificationSettingItem(model)
            });
    }
    // Function summary: Normalizes site list query paging and sorting for the business-layer use case.
    private static PageRequest BuildSitePageRequest(QuerySitesRequest request)
    {
        return PageRequestFactory.Create(
            request.SearchText,
            request.Page,
            request.PageSize,
            request.Sort,
            request.SortDir,
            SiteApplicationService.DefaultSort,
            SiteApplicationService.SortFields);
    }

    // Function summary: Normalizes fixed-sort site panel paging while preserving legacy sort-field behavior.
    private static PageRequest BuildFixedSortPageRequest(PagedQueryRequest request, string sort)
    {
        return new PageRequest(
            request.SearchText,
            request.GetNormalizedPage(),
            request.GetNormalizedPageSize(),
            sort,
            request.GetNormalizedSortDir());
    }

    // Function summary: Creates a transport-neutral current-user context from the authenticated HTTP user.
    private Task<PortalUserContext> CreateUserContextAsync()
    {
        return currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
    }

    // Function summary: Reads a site detail model through the application service and maps it to the existing API DTO.
    private async Task<SiteDetailResponse?> ReadSiteDetailAsync(Guid id, PortalUserContext? user = null, CancellationToken cancellationToken = default)
    {
        var currentUser = user ?? await CreateUserContextAsync();
        var result = await sites.GetAsync(
            currentUser,
            id,
            cancellationToken == default ? HttpContext.RequestAborted : cancellationToken);
        return result.Value == null ? null : ToSiteDetailResponse(result.Value);
    }

    // Function summary: Maps a business-layer site detail model while adding the protected customer-logo link owned by the HTTP adapter.
    private SiteDetailResponse ToSiteDetailResponse(SiteDetailModel model)
    {
        return SiteApiMapper.ToDetailResponse(model, customerLogoStorage.BuildProtectedLink(model.Id));
    }

    // Function summary: Maps a business-layer site detail model to the existing entity response envelope.
    private EntityResponse<SiteDetailResponse> ToSiteDetailEntity(SiteDetailModel model)
    {
        return new EntityResponse<SiteDetailResponse> { Item = ToSiteDetailResponse(model) };
    }

    // Function summary: Builds the existing invalid-sort problem response for site endpoints.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields)
    {
        var problem = ApiProblems.Create(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "Invalid sort field",
            $"Sort field '{requestedSort}' is not supported for sites.");
        problem.Extensions["allowedSortFields"] = allowedSortFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase).ToArray();
        return BadRequest(problem);
    }
    // Function summary: Builds the existing not-found response for missing or unauthorized sites.
    private NotFoundObjectResult SiteNotFound(Guid id)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Site not found",
            $"Site '{id}' was not found."));
    }
}

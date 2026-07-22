// File summary: Exposes API endpoints used by the React portal for notifications controller workflows.
// Major updates:
// - 2026-07-09 pending Routed notification close and batch-close write workflows through the notification application service.
// - 2026-07-09 pending Routed notification list/detail reads through the notification application service.
// - 2026-06-26 pending Scoped notification attribution to effective deployment/contract ownership windows.
// - 2026-06-25 pending Routed notification close workflows through transactional MediatR commands.
// - 2026-06-25 pending Passed monitor type into detail alert-level projection for vibration peak-only display.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVT.BusinessLogic.Application;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Application.Notifications;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationApplicationService notifications;
    private readonly ICurrentUserContextFactory currentUserContextFactory;

    // Function summary: Initializes this HTTP adapter with notification application workflows.
    public NotificationsController(
        INotificationApplicationService notifications,
        ICurrentUserContextFactory currentUserContextFactory)
    {
        this.notifications = notifications;
        this.currentUserContextFactory = currentUserContextFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryNotificationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries role-scoped notifications through the notification application service.
    public async Task<ActionResult<QueryNotificationsResponse>> Query([FromQuery] QueryNotificationsRequest request)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "notificationTime" : request.Sort.Trim();
        if (!NotificationApplicationService.SortFields.ContainsKey(requestedSort))
        {
            return InvalidSort(requestedSort, NotificationApplicationService.SortFields.Keys);
        }

        var query = new NotificationQuery(
            request.SearchText,
            request.GetNormalizedPage(),
            request.GetNormalizedPageSize(),
            requestedSort,
            request.GetNormalizedSortDir(),
            NotificationListStates.Normalize(request.State),
            request.MonitorType,
            request.AlertType,
            request.OpenAlerts,
            request.SiteId);
        var result = await notifications.QueryAsync(await CreateUserContextAsync(), query, HttpContext.RequestAborted);
        return NotificationApiMapper.ToQueryResponse(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntityResponse<NotificationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Retrieves a role-scoped notification detail through the notification application service.
    public async Task<ActionResult<EntityResponse<NotificationDetailResponse>>> Get(Guid id)
    {
        var detail = await notifications.GetAsync(await CreateUserContextAsync(), id, HttpContext.RequestAborted);
        if (detail == null)
        {
            return NotificationNotFound(id);
        }

        return new EntityResponse<NotificationDetailResponse>
        {
            Item = NotificationApiMapper.ToDetailResponse(detail)
        };
    }

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(EntityResponse<NotificationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Closes a notification through the application service and maps the refreshed detail.
    public async Task<ActionResult<EntityResponse<NotificationDetailResponse>>> Close(Guid id, NotificationCloseRequest request)
    {
        var user = await CreateUserContextAsync();
        var result = await notifications.CloseAsync(user, id, request.Note, HttpContext.RequestAborted);
        if (result.NotFound || (result.Detail == null && result.Errors.Count == 0))
        {
            return NotificationNotFound(id);
        }

        if (result.Errors.Count > 0)
        {
            AddModelErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return new EntityResponse<NotificationDetailResponse>
        {
            Item = NotificationApiMapper.ToDetailResponse(result.Detail!)
        };
    }

    [HttpPost("batch-close")]
    [ProducesResponseType(typeof(NotificationBatchCloseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Dispatches transactional batch close after validating the transport request shape.
    public async Task<ActionResult<NotificationBatchCloseResponse>> BatchClose(NotificationBatchCloseRequest request)
    {
        var ids = request.NotificationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            ModelState.AddModelError(nameof(NotificationBatchCloseRequest.NotificationIds), "Select at least one notification to close.");
        }
        if (request.Note?.Length > 255)
        {
            ModelState.AddModelError(nameof(NotificationBatchCloseRequest.Note), "Note must be 255 characters or fewer.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await CreateUserContextAsync();
        return await notifications.BatchCloseAsync(user, ids, request.Note, HttpContext.RequestAborted);
    }

    // Function summary: Creates a transport-neutral current-user context from the authenticated HTTP user.
    private Task<PortalUserContext> CreateUserContextAsync()
    {
        return currentUserContextFactory.CreateAsync(User, HttpContext.RequestAborted);
    }

    // Function summary: Copies command validation errors into MVC model state.
    private void AddModelErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }
    }

    // Function summary: Handles invalid sort requests for notification lists.
    private ObjectResult InvalidSort(string sort, IEnumerable<string> validSorts)
    {
        return Problem(
            detail: $"Sort '{sort}' is not supported. Use one of: {string.Join(", ", validSorts)}.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid sort field");
    }

    // Function summary: Builds the not-found response used for hidden or missing notifications.
    private NotFoundObjectResult NotificationNotFound(Guid id)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Notification not found",
            Detail = $"Notification '{id}' was not found or is not visible to the current user.",
            Status = StatusCodes.Status404NotFound
        });
    }
}

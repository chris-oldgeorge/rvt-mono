// File summary: Exposes React portal dashboard API endpoints as thin adapters over dashboard application services.
// Major updates:
// - 2026-07-30 pending Parsed calendar-day requests as UTC; the Unspecified kind failed timestamptz query binds on PostgreSQL.
// - 2026-07-09 pending Moved dashboard summary, map marker, and calendar logic behind IDashboardApplicationService.
// - 2026-07-08 pending Routed breach-alert list querying through an application service.
// - 2026-06-26 pending Scoped dashboard notifications and calendar data to effective deployment/contract windows.
// - 2026-06-25 pending Passed monitor type into alert-level projection for vibration peak-only display.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-10 pending Removed artificial async wrapping from calendar deployment option projection and deployment lookup helper.

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Spa.Api.Mappers;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Dashboard;

namespace RvtPortal.Spa.Api;

[ApiController]
[Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser + "," + RoleNames.RVTInstaller)]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardApplicationService _dashboard;
    private readonly IDashboardBreachApplicationService _dashboardBreaches;
    private readonly ICurrentUserContextFactory _currentUsers;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes this HTTP adapter with dashboard query use cases and current-user context creation.
    public DashboardController(
        IDashboardApplicationService dashboard,
        IDashboardBreachApplicationService dashboardBreaches,
        ICurrentUserContextFactory currentUsers,
        TimeProvider timeProvider)
    {
        _dashboard = dashboard;
        _dashboardBreaches = dashboardBreaches;
        _currentUsers = currentUsers;
        _timeProvider = timeProvider;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    // Function summary: Returns a role-scoped dashboard summary through the dashboard application service.
    public async Task<ActionResult<DashboardSummaryResponse>> Summary()
    {
        DashboardActor actor = await CreateActorAsync();
        DashboardSummaryModel result = await _dashboard.GetSummaryAsync(actor, HttpContext.RequestAborted);
        return DashboardApiMapper.ToSummaryResponse(result);
    }

    [HttpGet("breaches-alerts")]
    [Authorize(Roles = RoleNames.RVTMasterAdmin)]
    [ProducesResponseType(typeof(BreachesAlertsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // Function summary: Queries breach-alert rows through the application-layer dashboard use case.
    public async Task<ActionResult<BreachesAlertsResponse>> BreachesAlerts([FromQuery] BreachesAlertsRequest request)
    {
        PageRequest page = PageRequestFactory.Create(
            request.SearchText,
            request.Page,
            request.PageSize,
            request.Sort,
            request.SortDir,
            DashboardBreachApplicationService.DefaultSort,
            DashboardBreachApplicationService.SortFields);
        if (PageRequestFactory.IsInvalidSort(page))
        {
            return InvalidSort(page.Sort, DashboardBreachApplicationService.SortFields);
        }

        DashboardBreachResult result = await _dashboardBreaches.QueryAsync(
            new DashboardBreachQuery(request.Date, page),
            HttpContext.RequestAborted);
        return DashboardApiMapper.ToBreachesAlertsResponse(result);
    }

    [HttpGet("map-markers")]
    [ProducesResponseType(typeof(MapMarkersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns role-scoped dashboard map markers through the dashboard application service.
    public async Task<ActionResult<MapMarkersResponse>> MapMarkers([FromQuery] MapMarkersRequest request)
    {
        DashboardActor actor = await CreateActorAsync();
        DashboardMapMarkersModel? result = await _dashboard.GetMapMarkersAsync(actor, request.SiteId, HttpContext.RequestAborted);
        if (result is null && request.SiteId.HasValue)
        {
            return SiteNotFound(request.SiteId.Value);
        }

        return DashboardApiMapper.ToMapMarkersResponse(result ?? new DashboardMapMarkersModel());
    }

    [HttpGet("calendar/month")]
    [Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
    [ProducesResponseType(typeof(CalendarMonthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns a role-scoped dashboard calendar month through the dashboard application service.
    public async Task<ActionResult<CalendarMonthResponse>> CalendarMonth([FromQuery] CalendarMonthRequest request)
    {
        if (!request.DeploymentId.HasValue)
        {
            ModelState.AddModelError(nameof(CalendarMonthRequest.DeploymentId), "Deployment is required.");
            return ValidationProblem(ModelState);
        }

        if (!TrySelectCalendarMonth(request.Year, request.Month, out DateTime selectedMonth))
        {
            return ValidationProblem(ModelState);
        }

        DashboardActor actor = await CreateActorAsync();
        DashboardCalendarMonthModel? result = await _dashboard.GetCalendarMonthAsync(
            actor,
            request.DeploymentId.Value,
            selectedMonth,
            HttpContext.RequestAborted);
        if (result is null)
        {
            return DeploymentNotFound(request.DeploymentId.Value);
        }

        return DashboardApiMapper.ToCalendarMonthResponse(result);
    }

    [HttpGet("calendar/day")]
    [Authorize(Roles = RoleAuthorization.AdminRoles + "," + RoleNames.CompanyUser)]
    [ProducesResponseType(typeof(CalendarDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Function summary: Returns a role-scoped dashboard calendar day through the dashboard application service.
    public async Task<ActionResult<CalendarDayResponse>> CalendarDay([FromQuery] CalendarDayRequest request)
    {
        if (!request.MonitorId.HasValue)
        {
            ModelState.AddModelError(nameof(CalendarDayRequest.MonitorId), "Monitor is required.");
        }

        if (!TryCreateDate(request.Year, request.Month, request.Day, out DateTime displayDay))
        {
            ModelState.AddModelError(nameof(CalendarDayRequest.Day), "A valid calendar day is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        DashboardActor actor = await CreateActorAsync();
        Guid monitorId = request.MonitorId.GetValueOrDefault();
        DashboardCalendarDayModel? result = await _dashboard.GetCalendarDayAsync(
            actor,
            monitorId,
            displayDay,
            HttpContext.RequestAborted);
        if (result is null)
        {
            return MonitorNotFound(monitorId);
        }

        return DashboardApiMapper.ToCalendarDayResponse(result);
    }

    // Function summary: Builds the dashboard actor from authenticated HTTP user state.
    private async Task<DashboardActor> CreateActorAsync()
    {
        PortalUserContext currentUser = await _currentUsers.CreateAsync(User, HttpContext.RequestAborted);
        return DashboardActor.FromPortalUser(
            currentUser,
            User.IsInRole(RoleNames.RVTMasterAdmin),
            User.IsInRole(RoleNames.RVTAdmin));
    }

    // Function summary: Attempts select calendar month and reports whether it succeeded.
    private bool TrySelectCalendarMonth(int? year, int? month, out DateTime selectedMonth)
    {
        // Missing month parts fall back to the injected clock's UTC business day; DateTime.Today read
        // the server-local zone and could not be faked in tests.
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime.Date;
        if (year is < 1 or > 9999)
        {
            ModelState.AddModelError(nameof(CalendarMonthRequest.Year), "A valid year is required.");
        }

        if (month is < 1 or > 12)
        {
            ModelState.AddModelError(nameof(CalendarMonthRequest.Month), "A valid month is required.");
        }

        if (!ModelState.IsValid)
        {
            selectedMonth = MonthStart(now);
            return false;
        }

        selectedMonth = MonthStart(year ?? now.Year, month ?? now.Month, now.Kind);
        return true;
    }

    // Function summary: Returns the first day of the supplied month.
    private static DateTime MonthStart(DateTime value)
    {
        return MonthStart(value.Year, value.Month, value.Kind);
    }

    // Function summary: Returns the first day for explicit year/month values while preserving the supplied DateTimeKind.
    private static DateTime MonthStart(int year, int month, DateTimeKind kind)
    {
        return new DateTime(year, month, 1, 0, 0, 0, kind);
    }

    // Function summary: Attempts create a calendar day and reports whether it succeeded.
    private static bool TryCreateDate(int? year, int? month, int? day, out DateTime date)
    {
        if (year.HasValue && month.HasValue && day.HasValue)
        {
            // Calendar days are UTC days (the portal's timezone ruling), and the resulting value becomes a
            // timestamptz query bound - Npgsql rejects anything but Kind=Utc there, so parse it as UTC.
            return DateTime.TryParseExact(
                $"{year.Value:D4}-{month.Value:D2}-{day.Value:D2}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out date);
        }

        date = DateTime.MinValue;
        return false;
    }

    // Function summary: Builds an invalid-sort problem response for breach-alert queries.
    private BadRequestObjectResult InvalidSort(string requestedSort, IEnumerable<string> allowedSortFields) =>
        ApiProblems.InvalidSort(HttpContext, requestedSort, allowedSortFields, "breach and alert data");

    // Function summary: Builds the dashboard site-not-found response used by map-marker filters.
    private NotFoundObjectResult SiteNotFound(Guid siteId)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Site not found",
            $"Site '{siteId}' was not found or is not visible to the current user."));
    }

    // Function summary: Builds the dashboard deployment-not-found response used by calendar month queries.
    private NotFoundObjectResult DeploymentNotFound(Guid deploymentId)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Deployment not found",
            $"Deployment '{deploymentId}' was not found or is not visible to the current user."));
    }

    // Function summary: Builds the dashboard monitor-not-found response used by calendar day queries.
    private NotFoundObjectResult MonitorNotFound(Guid monitorId)
    {
        return NotFound(ApiProblems.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Monitor not found",
            $"Monitor '{monitorId}' was not found or is not visible to the current user."));
    }
}

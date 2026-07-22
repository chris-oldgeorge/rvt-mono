# Backend Business Layer Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move business/application decisions out of SPA API controllers and API contracts into `RVT.BusinessLogic`, while preserving every existing frontend-facing route, verb, request body, query parameter, response shape, and status behavior.

**Architecture:** Keep `RvtPortal.Spa/Api` as the transport layer: routing attributes, authorization attributes, model binding, current-user extraction, HTTP result translation, and API DTO mapping. Add application-service facades in `RVT.BusinessLogic` for monitor, site, report-rule, alert-level, dashboard, and notification workflows. Controllers call those services and translate `ApplicationResult<T>` into the existing DTO/HTTP responses.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core, ASP.NET Identity, existing `RVT.DataAccess` contexts, existing MediatR registration where already used, xUnit/API tests in `RvtPortal.Spa.Tests`.

---

## Non-Negotiable Compatibility Rules

- Do not change route templates, HTTP verbs, authorization attributes, query parameter names, request DTO names, response DTO names, JSON property names, or status code behavior.
- Do not move frontend-facing API contract classes out of `RvtPortal.Spa/Api` during this refactor.
- Do not make `RVT.BusinessLogic` reference `RvtPortal.Spa`; that would create a reversed dependency on the web host.
- Do not put `ActionResult`, `ProblemDetails`, `ModelStateDictionary`, `ClaimsPrincipal`, `HttpContext`, `IFormFile`, or controller-specific concepts into `RVT.BusinessLogic`.
- Keep file upload/streaming mechanics in the API/host layer, but move the decision logic around whether a site/monitor can accept that action into business services.
- Keep changes incremental and route-safe. One domain area must build and test before moving to the next.

## Current Problem Areas To Refactor

- `RvtPortal.Spa/Api/MonitorsController.cs`: currently owns state rules, role visibility, monitor assignment, delete-vs-archive decisions, measurement impact counting, default alert-level creation, and DTO construction.
- `RvtPortal.Spa/Api/SitesController.cs`: currently owns site search/query shaping, validation, operating-hour validation, archive creation, notification-setting decisions, monitor/notification subqueries, and DTO construction.
- `RvtPortal.Spa/Api/ReportRulesController.cs`: currently owns rule creation/update/delete, supported frequency policy, recipient eligibility, assignment paging, Help CMS guideline lookup, and manual generation forwarding.
- `RvtPortal.Spa/Api/*ApiContracts.cs`: mostly DTOs, but `PagedQueryRequest`, `SortDirections`, and `MonitorListStates` contain normalization/policy helpers that should move to the business/application layer or an API request-mapping helper.

## Target File Structure

Create these new business-layer folders and files:

- `RVT.BusinessLogic/Application/ApplicationResult.cs`: web-independent result envelope.
- `RVT.BusinessLogic/Application/PortalUserContext.cs`: current-user facts needed by business workflows.
- `RVT.BusinessLogic/Application/Paging/PageRequest.cs`: normalized paging/sort/search input.
- `RVT.BusinessLogic/Application/Paging/PagedResult.cs`: web-independent paged result.
- `RVT.BusinessLogic/Application/Paging/PageRequestFactory.cs`: clamp/default paging and sort values using allowed sort fields.
- `RVT.BusinessLogic/Application/Users/IPortalUserDirectory.cs`: abstraction over ASP.NET Identity user lookup/roles.
- `RVT.BusinessLogic/Monitors/MonitorApplicationService.cs`: monitor query, detail authorization, assignment, removal impact, delete/archive, default alert-level workflows.
- `RVT.BusinessLogic/Monitors/MonitorApplicationModels.cs`: business-layer monitor list/detail/impact models.
- `RVT.BusinessLogic/Sites/SiteApplicationService.cs`: site query, detail, mutation validation, archive, site monitor/notification/settings workflows.
- `RVT.BusinessLogic/Sites/SiteApplicationModels.cs`: business-layer site models.
- `RVT.BusinessLogic/Reports/ReportRuleApplicationService.cs`: report-rule query/detail/mutation/recipient/manual-generation orchestration.
- `RVT.BusinessLogic/Reports/ReportRuleApplicationModels.cs`: business-layer report-rule models.
- `RVT.BusinessLogic/Reports/IReportGenerationGateway.cs`: business-layer abstraction for manual generation.
- `RVT.BusinessLogic/Help/ReportGuidelineReader.cs`: lookup of published report alert-rule guidance by monitor type/content type.
- `RVT.BusinessLogic/DependencyInjection.cs`: business-layer service registration extension, or extend existing `InitBusinessLogic.cs` if that is the local convention.

Create these API-layer adapter files:

- `RvtPortal.Spa/Api/ApiResultMapper.cs`: maps `ApplicationResult<T>` to `ActionResult<TResponse>`.
- `RvtPortal.Spa/Api/CurrentUserContextFactory.cs`: converts `ClaimsPrincipal` plus `UserManager<ApplicationUser>` into `PortalUserContext`.
- `RvtPortal.Spa/Api/PortalUserDirectory.cs`: implements `IPortalUserDirectory` using ASP.NET Identity.
- `RvtPortal.Spa/Api/ReportGenerationGateway.cs`: wraps existing `ReportGenerationClient` behind `IReportGenerationGateway`.
- `RvtPortal.Spa/Api/Mappers/MonitorApiMapper.cs`: maps monitor application models to existing `MonitorApiContracts` DTOs.
- `RvtPortal.Spa/Api/Mappers/SiteApiMapper.cs`: maps site application models to existing `ContractSiteApiContracts` DTOs.
- `RvtPortal.Spa/Api/Mappers/ReportRuleApiMapper.cs`: maps report application models to existing `ReportApiContracts` DTOs.

Modify these existing files:

- `RvtPortal.Spa/Api/MonitorsController.cs`: reduce endpoints to request normalization, service call, mapping, and HTTP result translation.
- `RvtPortal.Spa/Api/SitesController.cs`: reduce endpoints to transport/upload/download handling and service calls.
- `RvtPortal.Spa/Api/ReportRulesController.cs`: reduce endpoints to transport plus service calls.
- `RvtPortal.Spa/Api/SharedApiContracts.cs`: keep DTO properties; mark behavior helpers for replacement, then remove or stop using them after services own normalization.
- `RvtPortal.Spa/Api/MonitorApiContracts.cs`: move monitor state normalization out of the contract file; keep state constants only if needed for API compatibility.
- `RvtPortal.Spa/ServiceCollectionExtensions.cs`: register new adapters and business services.
- `RvtPortal.Spa.Tests/*`: add route/contract regression tests and focused service tests.
- `docs/onboarding/FRONTEND_BACKEND_HANDOFF_DESIGN.md`: update backend boundary guidance after implementation.
- `project_state.md`: update broad architectural state after implementation.

## Desired Controller Shape

Controllers should look like this after each endpoint is refactored:

```csharp
[HttpGet]
[ProducesResponseType(typeof(QueryMonitorsResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<ActionResult<QueryMonitorsResponse>> Query([FromQuery] QueryMonitorsRequest request, CancellationToken cancellationToken)
{
    var user = await currentUserContextFactory.CreateAsync(User, cancellationToken);
    var page = PageRequestFactory.Create(
        request.SearchText,
        request.Page,
        request.PageSize,
        request.Sort,
        request.SortDir,
        defaultSort: "fleetNumber",
        allowedSorts: MonitorApplicationSorts.List);

    var result = await monitorApplicationService.QueryAsync(
        user,
        page,
        request.State,
        request.MonitorType,
        cancellationToken);

    return apiResultMapper.ToActionResult(result, MonitorApiMapper.ToQueryResponse);
}
```

The controller may bind HTTP-specific inputs and translate HTTP-specific outputs. It must not decide monitor state visibility, site validity, recipient eligibility, delete-vs-archive behavior, or persistence mutations.

## Task 1: Add Route And Contract Guardrails Before Refactoring

**Files:**

- Create: `RvtPortal.Spa.Tests/ApiContractStabilityTests.cs`
- Modify: none in production code

- [x] **Step 1: Add endpoint signature tests**

Create tests that reflect over controller attributes and assert the route templates currently consumed by the frontend still exist. Include at least these endpoints:

```csharp
[Fact]
public void MonitorRoutes_RemainStable()
{
    var routes = ApiRouteSnapshot.ForController<MonitorsController>();

    Assert.Contains(routes, route => route.HttpMethod == "GET" && route.Template == "api/monitors");
    Assert.Contains(routes, route => route.HttpMethod == "GET" && route.Template == "api/monitors/options");
    Assert.Contains(routes, route => route.HttpMethod == "GET" && route.Template == "api/monitors/{id:guid}");
    Assert.Contains(routes, route => route.HttpMethod == "PUT" && route.Template == "api/monitors/{id:guid}/fleet-number");
    Assert.Contains(routes, route => route.HttpMethod == "POST" && route.Template == "api/monitors/{id:guid}/contract-assignment");
    Assert.Contains(routes, route => route.HttpMethod == "DELETE" && route.Template == "api/monitors/{id:guid}/contract-assignment");
    Assert.Contains(routes, route => route.HttpMethod == "GET" && route.Template == "api/monitors/unattached");
    Assert.Contains(routes, route => route.HttpMethod == "DELETE" && route.Template == "api/monitors/{id:guid}/unattached");
}
```

Add the same style of assertions for:

- `SitesController`
- `ReportRulesController`
- `ReportsController`
- `ReportContentController`
- `AlertLevelsController`
- `NotificationsController`
- `DashboardController`
- `DataController`

- [x] **Step 2: Add DTO property-name tests**

Add serializer-based tests for high-risk DTOs:

```csharp
[Fact]
public void QueryMonitorsResponse_JsonShape_RemainsStable()
{
    var response = new QueryMonitorsResponse
    {
        Results = [new MonitorListItem { Id = Guid.Empty, SerialId = "S1", TypeOfMonitor = "AirQ" }],
        Page = 1,
        PageSize = 20,
        Total = 1,
        TotalPages = 1,
        Sort = "fleetNumber",
        SortDir = SortDirections.Ascending,
        State = MonitorListStates.All
    };

    using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonOptions.Default));
    var root = document.RootElement;

    Assert.True(root.TryGetProperty("results", out _));
    Assert.True(root.TryGetProperty("page", out _));
    Assert.True(root.TryGetProperty("pageSize", out _));
    Assert.True(root.TryGetProperty("sort", out _));
    Assert.True(root.TryGetProperty("sortDir", out _));
    Assert.True(root.TryGetProperty("state", out _));
}
```

- [x] **Step 3: Run the guardrail tests**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
```

Expected: all new tests pass before any refactor begins.

- [ ] **Step 4: Commit the guardrails**

```bash
git add RvtPortal.Spa.Tests/ApiContractStabilityTests.cs
git commit -m "test: lock SPA API route and DTO contracts"
```

## Task 2: Add Web-Independent Application Primitives

**Files:**

- Create: `RVT.BusinessLogic/Application/ApplicationResult.cs`
- Create: `RVT.BusinessLogic/Application/PortalUserContext.cs`
- Create: `RVT.BusinessLogic/Application/Paging/PageRequest.cs`
- Create: `RVT.BusinessLogic/Application/Paging/PagedResult.cs`
- Create: `RVT.BusinessLogic/Application/Paging/PageRequestFactory.cs`
- Create: `RvtPortal.Spa/Api/ApiResultMapper.cs`
- Create: `RvtPortal.Spa/Api/CurrentUserContextFactory.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`

- [x] **Step 1: Add `ApplicationResult<T>`**

```csharp
namespace RVT.BusinessLogic.Application;

public enum ApplicationResultKind
{
    Success,
    NotFound,
    Forbidden,
    Validation,
    Conflict,
    ExternalServiceUnavailable
}

public sealed record ApplicationError(string Field, string Message);

public sealed class ApplicationResult<T>
{
    private ApplicationResult(ApplicationResultKind kind, T? value, IReadOnlyList<ApplicationError> errors, string? message = null)
    {
        Kind = kind;
        Value = value;
        Errors = errors;
        Message = message;
    }

    public ApplicationResultKind Kind { get; }
    public T? Value { get; }
    public IReadOnlyList<ApplicationError> Errors { get; }
    public string? Message { get; }

    public static ApplicationResult<T> Success(T value) => new(ApplicationResultKind.Success, value, []);
    public static ApplicationResult<T> NotFound(string message) => new(ApplicationResultKind.NotFound, default, [], message);
    public static ApplicationResult<T> Forbidden() => new(ApplicationResultKind.Forbidden, default, []);
    public static ApplicationResult<T> Validation(params ApplicationError[] errors) => new(ApplicationResultKind.Validation, default, errors);
    public static ApplicationResult<T> Conflict(string message) => new(ApplicationResultKind.Conflict, default, [], message);
    public static ApplicationResult<T> ExternalServiceUnavailable(string message) => new(ApplicationResultKind.ExternalServiceUnavailable, default, [], message);
}
```

- [x] **Step 2: Add current-user context**

```csharp
namespace RVT.BusinessLogic.Application;

public sealed record PortalUserContext(
    Guid? UserId,
    string? UserName,
    Guid? CompanyId,
    bool IsAdmin,
    bool IsInstaller,
    bool IsCompanyUser);
```

- [x] **Step 3: Add normalized paging models**

```csharp
namespace RVT.BusinessLogic.Application.Paging;

public sealed record PageRequest(
    string? SearchText,
    int Page,
    int PageSize,
    string Sort,
    string SortDir);

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasPreviousPage => Page > 1 && Total > 0;
    public bool HasNextPage => Page * PageSize < Total;
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = "Ascending";
}
```

- [x] **Step 4: Add `PageRequestFactory`**

```csharp
namespace RVT.BusinessLogic.Application.Paging;

public static class PageRequestFactory
{
    public static PageRequest Create(
        string? searchText,
        int? page,
        int? pageSize,
        string? sort,
        string? sortDir,
        string defaultSort,
        IReadOnlySet<string> allowedSorts)
    {
        var requestedSort = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort.Trim();
        if (!allowedSorts.Contains(requestedSort))
        {
            return new PageRequest(searchText, -1, -1, requestedSort, NormalizeSortDir(sortDir));
        }

        var normalizedPage = page.GetValueOrDefault(1);
        var normalizedPageSize = pageSize.GetValueOrDefault(20);
        return new PageRequest(
            searchText,
            normalizedPage <= 0 ? 1 : normalizedPage,
            normalizedPageSize <= 0 ? 20 : Math.Min(normalizedPageSize, 100),
            requestedSort,
            NormalizeSortDir(sortDir));
    }

    public static bool IsInvalidSort(PageRequest request)
    {
        return request.Page == -1 && request.PageSize == -1;
    }

    private static string NormalizeSortDir(string? value)
    {
        return string.Equals(value, "Descending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
                ? "Descending"
                : "Ascending";
    }
}
```

- [x] **Step 5: Add API result mapper**

Map `ApplicationResultKind` to the existing HTTP conventions:

- `Success` -> `Ok` or caller-supplied success result.
- `NotFound` -> existing `ApiProblems.Create(..., 404, ...)`.
- `Forbidden` -> `Forbid()`.
- `Validation` -> `ValidationProblem`.
- `Conflict` -> `409 ProblemDetails`.
- `ExternalServiceUnavailable` -> `502` or `503`, matching current reporting-service behavior.

- [x] **Step 6: Register primitives**

Register:

```csharp
services.AddScoped<ICurrentUserContextFactory, CurrentUserContextFactory>();
services.AddScoped<IApiResultMapper, ApiResultMapper>();
```

- [x] **Step 7: Run tests**

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet build RvtPortal.Spa.sln
```

Expected: route/contract guardrails and solution build pass.

## Task 3: Move Report Rule Workflows First

**Files:**

- Create: `RVT.BusinessLogic/Reports/ReportRuleApplicationModels.cs`
- Create: `RVT.BusinessLogic/Reports/IReportGenerationGateway.cs`
- Create: `RVT.BusinessLogic/Reports/ReportRuleApplicationService.cs`
- Create: `RvtPortal.Spa/Api/ReportGenerationGateway.cs`
- Create: `RvtPortal.Spa/Api/Mappers/ReportRuleApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Test: existing report-rule API tests plus new service tests.

- [x] **Step 1: Define report application models**

Create business-layer models that do not reference API DTO classes:

```csharp
namespace RVT.BusinessLogic.Reports;

public sealed record ReportRuleQuery(
    Guid? SiteId,
    string? SearchText,
    int? Page,
    int? PageSize,
    string? Sort,
    string? SortDir);

public sealed record ReportRuleMutation(
    Guid SiteId,
    ReportFrequencyType Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    string? ReportName);

public sealed record ReportRuleListModel(
    Guid Id,
    Guid SiteId,
    string SiteName,
    ReportFrequencyType Frequency,
    string FrequencyLabel,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    string? ReportName,
    DateTime? LastGenerated,
    bool CanManage);
```

- [x] **Step 2: Move supported report frequency policy**

Move these from `ReportRulesController` into `ReportRuleApplicationService`:

- supported frequencies
- supported days
- frequency labels
- rule validation for day-of-week/day-of-month
- site existence validation
- report name length validation

- [x] **Step 3: Move query/detail/create/update/delete use cases**

The service should expose:

```csharp
Task<ApplicationResult<PagedResult<ReportRuleListModel>>> QueryAsync(ReportRuleQuery query, CancellationToken cancellationToken);
Task<ApplicationResult<ReportRuleDetailModel>> GetAsync(Guid id, CancellationToken cancellationToken);
Task<ApplicationResult<ReportRuleDetailModel>> CreateAsync(PortalUserContext user, ReportRuleMutation mutation, CancellationToken cancellationToken);
Task<ApplicationResult<ReportRuleDetailModel>> UpdateAsync(PortalUserContext user, Guid id, ReportRuleMutation mutation, CancellationToken cancellationToken);
Task<ApplicationResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
```

- [x] **Step 4: Move recipient assignment logic**

Move these methods out of `ReportRulesController`:

- `BuildAssignmentResponseAsync`
- `QueryAssignmentUsersAsync`
- `BuildReportCandidateUsersAsync`
- `BuildUserItemsAsync`
- `ValidateReportUserAsync`

Use `IPortalUserDirectory` so the business layer can ask for users/roles without depending on `UserManager<ApplicationUser>`.

- [x] **Step 5: Move manual generation orchestration**

Create `IReportGenerationGateway` in `RVT.BusinessLogic/Reports` and implement it in the API project using the existing `ReportGenerationClient`. The business service owns rule-existence validation and calls the gateway; the API adapter owns HTTP.

- [x] **Step 6: Slim `ReportRulesController`**

After refactor, controller methods should not use `RVTSearchContext`, `RVTDbContext`, `UserManager<ApplicationUser>`, or raw EF queries directly. It should depend on:

```csharp
private readonly IReportRuleApplicationService reportRuleService;
private readonly ICurrentUserContextFactory currentUserContextFactory;
private readonly IApiResultMapper resultMapper;
```

- [x] **Step 7: Verify**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~Report
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet build RvtPortal.Spa.sln
```

Expected: report behavior and API route/DTO guardrails pass.

- [x] **Step 8: Commit**

```bash
git add RVT.BusinessLogic/Reports RvtPortal.Spa/Api/ReportRulesController.cs RvtPortal.Spa/Api/Mappers/ReportRuleApiMapper.cs RvtPortal.Spa/Api/ReportGenerationGateway.cs RvtPortal.Spa/ServiceCollectionExtensions.cs RvtPortal.Spa.Tests
git commit -m "refactor: move report rule workflows to business layer"
```

Completed in commit `9b849b3`.

## Task 4: Move Site Workflows

**Files:**

- Create: `RVT.BusinessLogic/Sites/SiteApplicationModels.cs`
- Create: `RVT.BusinessLogic/Sites/SiteApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/SiteApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/SitesController.cs`
- Test: site controller/service tests.

- [x] **Step 1: Move site query and detail logic**

Move from `SitesController`:

- visible-site query policy
- company-user scoping
- site list filtering/search/sort/paging
- site counters
- detail construction
- site monitor list construction
- open notification list construction

Expose:

```csharp
Task<ApplicationResult<PagedResult<SiteListModel>>> QueryAsync(PortalUserContext user, SiteQuery query, CancellationToken cancellationToken);
Task<ApplicationResult<SiteDetailModel>> GetAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);
Task<ApplicationResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);
Task<ApplicationResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);
```

- [x] **Step 2: Move site mutation validation**

Move from `SitesController`:

- site name required/length/duplicate validation
- address field length validation
- operating-hour time-pair validation
- selected contract/company validation
- contract already-assigned rule

Expose:

```csharp
Task<ApplicationResult<SiteDetailModel>> CreateAsync(PortalUserContext user, SiteMutation mutation, CancellationToken cancellationToken);
Task<ApplicationResult<SiteDetailModel>> UpdateAsync(PortalUserContext user, Guid id, SiteMutation mutation, CancellationToken cancellationToken);
Task<ApplicationResult<SiteDetailModel>> ArchiveAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);
```

- [x] **Step 3: Keep logo upload transport in controller**

`SitesController.UploadCustomerLogo` should:

1. bind `IFormFile`
2. ask `SiteApplicationService.CanManageSiteAsync(user, id)`
3. save via `ICustomerLogoStorage`
4. ask service for the updated detail
5. map detail to existing `SiteDetailResponse`

The controller should not validate site ownership or build site details itself.

- [x] **Step 4: Move notification settings logic**

Move from `SitesController`:

- company user can only update own settings
- optional time parsing
- time-pair validation
- create/update `NotificationSettings`
- return updated setting row

Expose:

```csharp
Task<ApplicationResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(PortalUserContext user, Guid siteId, CancellationToken cancellationToken);
Task<ApplicationResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(PortalUserContext user, Guid siteId, Guid siteUserId, SiteNotificationSettingMutation mutation, CancellationToken cancellationToken);
```

- [x] **Step 5: Verify**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~Site
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet build RvtPortal.Spa.sln
```

Expected: site behavior and frontend contract guardrails pass.

Checkpoint 2026-07-05:

- Core site query/detail, site mutation validation, archive, options, and logo authorization now use `ISiteApplicationService`.
- Embedded and standalone site monitor/open-notification rows now use paged service methods.
- Notification settings read/update rules now live in `ISiteApplicationService`, with a boundary test preventing persistence logic from moving back into `SitesController`.
- Verification passed in the Windows VM: red/green boundary test, `dotnet build RvtPortal.Spa.sln`, `ContractSiteOperationsTests` 5/5, `ApiContractStabilityTests` 8/8, and full `RvtPortal.Spa.Tests` 217/217.

## Task 5: Move Monitor Workflows

**Files:**

- Create: `RVT.BusinessLogic/Monitors/MonitorApplicationModels.cs`
- Create: `RVT.BusinessLogic/Monitors/MonitorApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/MonitorApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/MonitorsController.cs`
- Move or wrap: `RvtPortal.Spa/Api/MonitorDetailSummaryService.cs`
- Test: monitor API and service tests.

- [ ] **Step 1: Move monitor list state policy**

Move from `MonitorApiContracts.cs` and `MonitorsController.cs`:

- monitor state constants
- state normalization
- installer-only state override
- `CanUseState`
- `ApplyState`
- `ApplyRoleVisibilityAsync`
- visible site lookup

Keep API string values unchanged: `all`, `new`, `not-in-use`, `offline`, `online`, `installer`.

- [ ] **Step 2: Move list/detail query logic**

Move from `MonitorsController`:

- `BuildMonitorRowsAsync`
- `BuildListItem`
- `FindCurrentDeploymentAsync`
- `FindDeploymentAsync`
- `CanReadMonitorAsync`
- offline/alert/caution calculation

Expose:

```csharp
Task<ApplicationResult<PagedResult<MonitorListModel>>> QueryAsync(PortalUserContext user, MonitorQuery query, CancellationToken cancellationToken);
Task<ApplicationResult<MonitorDetailModel>> GetAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);
```

- [ ] **Step 3: Move mutation and assignment rules**

Move from `MonitorsController`:

- fleet number required/length/duplicate rules
- first-fleet-number default alert-level creation
- contract assignment validation
- contract already assigned/current deployment rules
- unassign immediate-delete versus close-deployment rule

Expose:

```csharp
Task<ApplicationResult<MonitorDetailModel>> SetFleetNumberAsync(PortalUserContext user, Guid id, string? fleetNumber, CancellationToken cancellationToken);
Task<ApplicationResult<MonitorDetailModel>> AddToContractAsync(PortalUserContext user, Guid id, MonitorAssignment assignment, CancellationToken cancellationToken);
Task<ApplicationResult<Guid>> RemoveFromContractAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);
```

- [ ] **Step 4: Move unattached monitor delete/archive workflow**

Move from `MonitorsController`:

- unattached query
- removal impact counts
- measurement row counts
- archive-if-related-data decision
- hard delete when no related data exists

Expose:

```csharp
Task<ApplicationResult<PagedResult<UnattachedMonitorModel>>> QueryUnattachedAsync(PortalUserContext user, MonitorQuery query, CancellationToken cancellationToken);
Task<ApplicationResult<MonitorRemovalImpactModel>> GetRemovalImpactAsync(PortalUserContext user, Guid id, CancellationToken cancellationToken);
Task<ApplicationResult<MonitorRemovalModel>> RemoveUnattachedAsync(PortalUserContext user, Guid id, string? reason, CancellationToken cancellationToken);
```

- [ ] **Step 5: Keep picture streaming transport in controller**

`MonitorsController.GetPicture` should:

1. ask `MonitorApplicationService.GetPictureReferenceAsync(user, id)`
2. open stream via `IMonitorPictureStorage`
3. return `File(stream, contentType, fileName)`

The controller should not build monitor detail or decide read authorization.

- [ ] **Step 6: Verify**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~Monitor
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet build RvtPortal.Spa.sln
```

Expected: monitor behavior and frontend contract guardrails pass.

## Task 6: Clean API Contracts So They Are Transport-Only

**Files:**

- Modify: `RvtPortal.Spa/Api/SharedApiContracts.cs`
- Modify: `RvtPortal.Spa/Api/MonitorApiContracts.cs`
- Modify: controller request mapping code.

- [ ] **Step 1: Stop using behavior methods on request DTOs**

Replace controller calls such as:

```csharp
var page = request.GetNormalizedPage();
var pageSize = request.GetNormalizedPageSize();
var sortDir = request.GetNormalizedSortDir();
```

with:

```csharp
var page = PageRequestFactory.Create(
    request.SearchText,
    request.Page,
    request.PageSize,
    request.Sort,
    request.SortDir,
    defaultSort,
    allowedSorts);
```

- [ ] **Step 2: Move monitor state normalization**

Move `MonitorListStates.Normalize` to `RVT.BusinessLogic/Monitors/MonitorListStatePolicy.cs`. Keep constants in API contracts only if frontend compatibility tests need them; otherwise, expose them through mapper-level constants.

- [ ] **Step 3: Make API contracts passive**

After all controllers stop calling DTO behavior methods, remove these methods from API contracts:

- `SearchLookupRequest.GetNormalizedTake`
- `PagedQueryRequest.GetNormalizedPage`
- `PagedQueryRequest.GetNormalizedPageSize`
- `PagedQueryRequest.GetNormalizedSortDir`
- `MonitorListStates.Normalize`

Keep the properties unchanged.

- [ ] **Step 4: Verify**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
dotnet build RvtPortal.Spa.sln
```

Expected: full backend tests pass and JSON contract tests show no frontend-visible drift.

## Task 7: Sweep Remaining Controllers

**Files:**

- Modify: `RvtPortal.Spa/Api/AlertLevelsController.cs`
- Modify: `RvtPortal.Spa/Api/DashboardController.cs`
- Modify: `RvtPortal.Spa/Api/DataController.cs`
- Modify: `RvtPortal.Spa/Api/NotificationsController.cs`
- Modify: `RvtPortal.Spa/Api/CompaniesController.cs`
- Modify: `RvtPortal.Spa/Api/ContractsController.cs`
- Modify: `RvtPortal.Spa/Api/UsersController.cs`

- [ ] **Step 1: Classify each controller method**

For each method, mark it as one of:

- transport-only and acceptable
- query/mapping logic to move
- validation/policy logic to move
- mutation workflow to move

- [ ] **Step 2: Move only meaningful logic**

Keep tiny CRUD endpoints if they already delegate to services and do not contain policy. Move code when the controller:

- calls EF directly
- builds business decision branches
- validates domain rules
- determines role visibility
- decides archive/delete/update side effects
- performs in-memory filtering/paging over loaded data

- [ ] **Step 3: Verify after each controller**

Run the controller-specific tests plus:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~ApiContractStabilityTests
dotnet build RvtPortal.Spa.sln
```

Expected: endpoint surface remains unchanged after each migrated controller.

## Task 8: Documentation And Developer Guidance

**Files:**

- Modify: `docs/onboarding/FRONTEND_BACKEND_HANDOFF_DESIGN.md`
- Modify: `AGENTS.md`
- Modify: workspace `project_state.md`

- [ ] **Step 1: Document the new layering rule**

Add this guidance:

```markdown
Backend layering rule:

- Controllers own HTTP transport only: route attributes, authorization attributes, model binding, current-user extraction, cancellation tokens, result mapping, file upload/download streaming.
- API contracts own transport shape only: request/response properties that must remain frontend-compatible.
- `RVT.BusinessLogic` owns application decisions: validation, authorization decisions beyond route attributes, role visibility, query shaping, archive/delete choices, default creation side effects, and cross-entity workflow orchestration.
- `RVT.DataAccess` owns EF contexts, repositories, provider-specific database access, canonical schema mappings, and migrations.
```

- [ ] **Step 2: Update AGENTS.md**

Add a rule requiring future endpoint work to place new business workflows in `RVT.BusinessLogic` and add/maintain API contract stability tests when touching controllers.

- [ ] **Step 3: Update project_state.md**

Record:

- date
- files moved/created
- controllers refactored
- route/DTO guardrail results
- full test result
- known remaining controllers if the sweep is incomplete

## Final Verification Gate

Run these commands before declaring the refactor complete:

```bash
dotnet restore RvtPortal.Spa.sln
dotnet build RvtPortal.Spa.sln
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
git diff --check
```

If frontend tooling is available in the checkout, also run:

```bash
cd RvtPortal.Client
npm test -- --run
npm run build
```

Expected:

- backend solution builds
- backend tests pass
- frontend build/tests pass when available
- route/DTO stability tests pass
- no whitespace errors
- no changes to frontend API client are required

## Execution Order

1. Route/DTO guardrails.
2. Application primitives and API adapters.
3. Report rules, because it is high-value and already has clearer query boundaries.
4. Sites, because it contains broad validation and subresource workflows.
5. Monitors, because it is the largest/highest-risk controller and benefits from the primitives proven by tasks 3 and 4.
6. Contract cleanup.
7. Remaining controller sweep.
8. Documentation and project-state update.

## Acceptance Criteria

- Controllers no longer contain EF query chains for migrated workflows.
- Controllers no longer contain domain validation rules for migrated workflows.
- Controllers no longer contain role-visibility policy beyond route-level `[Authorize]`.
- API contracts contain no request-normalization or business-policy methods.
- `RVT.BusinessLogic` contains the business/application services for report, site, and monitor workflows.
- Existing frontend endpoints and JSON shapes are unchanged.
- Route/DTO stability tests pass.
- Full backend test suite passes.
- Documentation describes the new layering rule clearly enough for onboarding.

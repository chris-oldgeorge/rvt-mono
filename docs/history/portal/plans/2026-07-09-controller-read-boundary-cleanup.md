# Controller Read Boundary Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move remaining controller-owned read/business composition behind application services without changing SPA API endpoints.

**Architecture:** Each controller keeps route attributes, request normalization, DTO mapping, `ProblemDetails`, and MediatR write dispatch. Query/detail/business composition moves into focused application services, with API mappers preserving existing response contracts.

**Tech Stack:** ASP.NET Core, EF Core, MediatR, xUnit, existing `RVTDbContext`/`RVTSearchContext`, existing API DTOs.

## Global Constraints

- Do not change existing endpoint routes, HTTP verbs, or public API DTO JSON shapes.
- Use TDD: add a failing architecture guardrail before each controller refactor.
- Update source file summaries and major-update comments in changed source files.
- Keep write workflows on existing MediatR commands unless a controller has no command boundary yet.
- Prefer application services over direct EF/Identity dependencies in controllers.
- Run focused workflow tests for each slice, then full backend build/tests before final handoff.

---

### Task 1: Notifications Controller

**Files:**
- Create: `RvtPortal.Spa/Application/Notifications/NotificationApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/NotificationApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/NotificationsController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

**Interfaces:**
- Produces: `INotificationApplicationService.QueryAsync`, `GetAsync`, `BuildCloseDetailAsync`, `BuildActorAsync`.
- Consumes: existing `CloseNotificationCommand`, `BatchCloseNotificationsCommand`, existing `NotificationAlertWorkflowTests`.

- [ ] Add a failing guardrail proving `NotificationsController` no longer accepts `RVTDbContext` or `UserManager<ApplicationUser>`.
- [ ] Move notification list/detail composition, visibility filtering, related notifications, alert-level projection, and actor creation into `NotificationApplicationService`.
- [ ] Map application models to existing `QueryNotificationsResponse` and `NotificationDetailResponse`.
- [ ] Keep close and batch-close endpoints on MediatR commands.
- [ ] Run `CqrsArchitectureTests|NotificationAlertWorkflowTests`.
- [ ] Commit the Notifications slice.

### Task 2: Installer API Controller

**Files:**
- Create: `RvtPortal.Spa/Application/Installers/InstallerApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/InstallerApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/InstallerApiController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [ ] Add a failing guardrail for `InstallerApiController`.
- [ ] Move installer monitor list, visibility, search, detail lookup, and install/remove orchestration helpers into an application service.
- [ ] Preserve existing installer routes and response DTOs.
- [ ] Run installer/API contract tests and architecture tests.
- [ ] Commit the Installer slice.

### Task 3: Alert Levels Controller

**Files:**
- Create: `RvtPortal.Spa/Application/AlertLevels/AlertLevelApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/AlertLevelApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/AlertLevelsController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [ ] Add a failing guardrail for `AlertLevelsController`.
- [ ] Move alert-level query/options/detail composition and management authorization into an application service.
- [ ] Keep mutation endpoints on existing MediatR commands.
- [ ] Run alert-level workflow and architecture tests.
- [ ] Commit the Alert Levels slice.

### Task 4: Companies And Contracts Controllers

**Files:**
- Create: `RvtPortal.Spa/Application/Companies/CompanyApplicationService.cs`
- Create: `RvtPortal.Spa/Application/Contracts/ContractApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/CompanyApiMapper.cs`
- Create: `RvtPortal.Spa/Api/Mappers/ContractApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/CompaniesController.cs`
- Modify: `RvtPortal.Spa/Api/ContractsController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [ ] Add failing guardrails for both controllers.
- [ ] Move list/detail/options composition behind application services.
- [ ] Keep create/update/delete command dispatch unchanged.
- [ ] Run company/contract/site workflow and architecture tests.
- [ ] Commit the Companies/Contracts slice.

### Task 5: Reports Controller

**Files:**
- Create: `RvtPortal.Spa/Application/Reports/ReportApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/ReportApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/ReportsController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [ ] Add a failing guardrail for `ReportsController`.
- [ ] Move report query/search/sort/page logic behind an application service.
- [ ] Keep existing report response contracts stable.
- [ ] Run report workflow and architecture tests.
- [ ] Commit the Reports slice.

### Task 6: Help Controller

**Files:**
- Create: `RvtPortal.Spa/Application/Help/HelpApplicationService.cs`
- Create: `RvtPortal.Spa/Api/Mappers/HelpApiMapper.cs`
- Modify: `RvtPortal.Spa/Api/HelpController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [ ] Add a failing guardrail for `HelpController`.
- [ ] Move public/admin CMS reads, filtering, and asset/detail composition behind an application service.
- [ ] Keep existing Help write commands and routes unchanged.
- [ ] Run Help/API contract and architecture tests.
- [ ] Commit the Help slice.

### Task 7: Final Verification

- [ ] Run `dotnet build ./RvtPortal.Spa.sln -m:1 --no-restore`.
- [ ] Run `dotnet test ./RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-build`.
- [ ] Run `dotnet format ./RvtPortal.Spa.sln --verify-no-changes --no-restore --verbosity minimal`.
- [ ] Run `git diff --check`.
- [ ] Update `project_state.md` with implemented slices, verification, and remaining follow-up.

# Controller Write Orchestration Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move remaining direct controller `IMediator` write orchestration behind application services in the agreed 1-7 order, then perform an overall code/comment cleanup.

**Architecture:** Keep existing routes, request/response DTOs, transactional MediatR commands, and command handlers. Controllers should own HTTP transport, validation/problem mapping, file transport, and current-user capture; application services should orchestrate workflows and invoke MediatR commands where transaction behavior already exists.

**Tech Stack:** ASP.NET Core 10, MediatR, EF Core, xUnit integration tests, `dotnet build`, `dotnet test`, `dotnet format`.

## Global Constraints

- Keep controller endpoint routes and API DTO JSON shape stable for the React SPA.
- Use TDD guardrails: add or strengthen an architecture test, verify it fails, implement, then verify focused organic tests and full backend tests.
- Preserve existing transactional command handlers and `ITransactionalRequest` behavior.
- Update touched source file summary/function summary/major-updates comments.
- Update `project_state.md` after each committed slice.

---

### Task 1: Company CRUD Write Orchestration

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/CompaniesController.cs`
- Modify: `RvtPortal.Spa/Application/Companies/CompanyApplicationService.cs`
- Test: `RvtPortal.Spa.Tests/CompanyUserAdminTests.cs`

**Interfaces:**
- Produces: `ICompanyApplicationService.CreateAsync`, `UpdateAsync`, and `DeleteAsync`.

- [ ] Add a guardrail asserting `CompaniesController` does not depend on `IMediator`.
- [ ] Verify the guardrail fails because `CompaniesController` currently has `IMediator`.
- [ ] Move create/update/delete orchestration into `CompanyApplicationService`.
- [ ] Keep controller response mapping, `CreatedAtAction`, validation problem mapping, and not-found mapping stable.
- [ ] Run focused tests: `CqrsArchitectureTests`, `CompanyUserAdminTests`, and `ApiContractStabilityTests.ManagementApiRoutes_RemainStable`.
- [ ] Commit and push.

### Task 2: Contract CRUD Write Orchestration

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/ContractsController.cs`
- Modify: `RvtPortal.Spa/Application/Contracts/ContractApplicationService.cs`
- Test: `RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`

**Interfaces:**
- Produces: `IContractApplicationService.CreateAsync`, `UpdateAsync`, and `DeleteAsync`.

- [ ] Add a guardrail asserting `ContractsController` does not depend on `IMediator`.
- [ ] Verify the guardrail fails because `ContractsController` currently has `IMediator`.
- [ ] Move contract create/update/delete orchestration behind `ContractApplicationService`.
- [ ] Run focused contract route and organic contract operation tests.
- [ ] Commit and push.

### Task 3: Help CMS Mutation Orchestration

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/HelpController.cs`
- Modify: `RvtPortal.Spa/Application/Help/HelpApplicationService.cs`
- Test: `RvtPortal.Spa.Tests/HelpCmsOperationsTests.cs`

**Interfaces:**
- Produces: `IHelpApplicationService.CreateAsync`, `UpdateAsync`, `SetPublicationAsync`, and `DeleteAsync`.

- [ ] Add a guardrail asserting `HelpController` does not depend on `IMediator`.
- [ ] Verify the guardrail fails because `HelpController` currently has `IMediator`.
- [ ] Move help CMS mutations behind `HelpApplicationService`.
- [ ] Run focused help CMS and route tests.
- [ ] Commit and push.

### Task 4: Alert Level Mutation Orchestration

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/AlertLevelsController.cs`
- Modify: `RvtPortal.Spa/Application/AlertLevels/AlertLevelApplicationService.cs`
- Test: alert-level workflow tests in `RvtPortal.Spa.Tests`.

**Interfaces:**
- Produces: alert-level create/update/delete/vibration update workflow methods on `IAlertLevelApplicationService`.

- [ ] Add a guardrail asserting `AlertLevelsController` does not depend on `IMediator`.
- [ ] Verify the guardrail fails because `AlertLevelsController` currently has `IMediator`.
- [ ] Move alert-level mutations behind `AlertLevelApplicationService`.
- [ ] Run focused alert-level tests and route stability tests.
- [ ] Commit and push.

### Task 5: Installer Remaining MediatR Calls

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/InstallerApiController.cs`
- Modify: `RvtPortal.Spa/Application/Installers/InstallerApplicationService.cs`
- Test: `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`

**Interfaces:**
- Produces: installer monitor-detail and deployment-update workflow methods on `IInstallerApplicationService`.

- [ ] Add a guardrail asserting `InstallerApiController` does not depend on `IMediator`.
- [ ] Verify the guardrail fails because `InstallerApiController` currently has `IMediator`.
- [ ] Move remaining installer monitor-detail query and deployment update orchestration behind `InstallerApplicationService`.
- [ ] Run focused installer monitor workflow tests and route tests.
- [ ] Commit and push.

### Task 6: Site Mutation Orchestration

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/SitesController.cs`
- Modify: site application service files under `RvtPortal.Spa/Application` and `RVT.BusinessLogic/Sites` as needed.
- Test: `RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`

**Interfaces:**
- Produces: site create/update/archive/notification-setting write workflow methods on the site application service boundary.

- [ ] Split file-upload transport behavior from database mutation orchestration.
- [ ] Add a guardrail for the selected site write workflow before production edits.
- [ ] Move site mutation workflows incrementally behind the service boundary.
- [ ] Run focused site operation tests and route tests.
- [ ] Commit and push.

### Task 7: Monitor Controller Workflow Split

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `RvtPortal.Spa/Api/MonitorsController.cs`
- Modify: monitor application service files under `RvtPortal.Spa/Application/Monitors`.
- Test: `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`, `MonitorRemovalApiTests.cs`, and `MonitorPictureCommandTests.cs`.

**Interfaces:**
- Produces: monitor detail, update, picture upload, fleet-number, contract assignment, removal, unattached delete, and default alert-level workflows behind monitor application services.

- [ ] Split this controller into multiple small TDD sub-slices because it is dense.
- [ ] Add one guardrail per sub-slice before production edits.
- [ ] Preserve file-upload/file-stream HTTP transport at the controller boundary.
- [ ] Run focused monitor tests after every sub-slice.
- [ ] Commit and push after stable groups of monitor workflows.

### Task 8: Overall Code And Comment Cleanup

**Files:**
- Modify touched controller/application/test files and `project_state.md`.

**Interfaces:**
- Produces: clean comments, no obsolete major-update text, and verified backend suite.

- [ ] Scan `RvtPortal.Spa/Api` for direct `IMediator` dependencies.
- [ ] Review touched source comments for stale "read only" wording after write orchestration moves.
- [ ] Run full backend build, full backend tests, formatter verification, and whitespace checks.
- [ ] Update `project_state.md` with final state and remaining follow-up work.

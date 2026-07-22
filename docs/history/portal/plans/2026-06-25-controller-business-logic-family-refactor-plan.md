# Controller Business Logic Family Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move remaining controller-side business write logic into MediatR handlers/readers in staged family sets, keeping transaction boundaries consistent through `ITransactionalRequest` and the existing Unit of Work pipeline.

**Architecture:** Keep EF Core as the ORM. Controllers should validate HTTP shape, build the current actor context, call MediatR/readers/services, and map results to HTTP responses. Command handlers own write decisions and persistence-bound state changes; `TransactionPipelineBehavior` owns one final `SaveChangesAsync()` for transactional requests.

**Tech Stack:** ASP.NET Core controllers, EF Core, MediatR, existing `IUnitOfWork` / `TransactionPipelineBehavior`, xUnit workflow and architecture tests.

---

## Family Sets

1. **Notifications Close Family:** Move single close and batch-close notification mutations from `NotificationsController` into `RvtPortal.Spa/Application/Notifications/NotificationCloseCommands.cs`.
2. **Alert Rules Family:** Move create/update/vibration upsert/delete decisions from `AlertLevelsController` into command handlers while leaving query projection in readers.
3. **Contracts/Companies CRUD Family:** Move simple aggregate create/update/delete flows from `ContractsController` and `CompaniesController` into commands.
4. **Sites/Help/Admin Content Family:** Move site, notification-setting, customer-logo, and Help CMS mutations into cohesive commands/services because these actions have multi-entity and storage side effects.
5. **Installer/Monitor Residual Writes Family:** Finish remaining monitor and installer write actions not already covered by `MonitorContractAssignmentCommands`, `RemoveUnattachedMonitorCommand`, or `UploadMonitorPictureCommand`.
6. **Read-Heavy Family:** Extract large query/projection workflows in notifications, dashboard, sites, and reports into readers only when they block testability or performance.

## Task 1: Notifications Close Commands

**Files:**
- Create: `RvtPortal.Spa/Application/Notifications/NotificationCloseCommands.cs`
- Modify: `RvtPortal.Spa/Api/NotificationsController.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/NotificationAlertWorkflowTests.cs`

- [x] **Step 1: Write the failing architecture guardrail**

Add `NotificationCloseWorkflows_AreTransactionalCommands` to `CqrsArchitectureTests`. The test checks that `NotificationsController` injects `IMediator`, sends `CloseNotificationCommand` and `BatchCloseNotificationsCommand`, no longer calls `SaveChangesAsync`, and that the command file exists with `ITransactionalRequest`.

- [x] **Step 2: Run the red test**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests.NotificationCloseWorkflows_AreTransactionalCommands" --logger "console;verbosity=minimal"
```

Expected: fail because `RvtPortal.Spa/Application/Notifications/NotificationCloseCommands.cs` does not exist and `NotificationsController` still calls `domainContext.SaveChangesAsync()`.

Result: failed as expected because `NotificationsController` did not contain `IMediator`.

- [x] **Step 3: Add transactional notification commands**

Create `NotificationCloseCommands.cs` with:

```csharp
public sealed record NotificationCloseActor(Guid? UserId, bool IsAdmin, bool IsCompanyUser);

public sealed record CloseNotificationCommand(Guid NotificationId, string? Note, NotificationCloseActor Actor)
    : IRequest<CloseNotificationResult>, ITransactionalRequest;

public sealed record BatchCloseNotificationsCommand(IReadOnlyCollection<Guid> NotificationIds, string? Note, NotificationCloseActor Actor)
    : IRequest<NotificationBatchCloseResponse>, ITransactionalRequest;
```

The handlers load notifications with monitor data, find the best matching deployment for notification time, check company-user visibility against `SiteUsers`, reject non-alert rows, set `ClosedNote`, `ClosedTime`, and `ClosedByUser`, and rely on the MediatR transaction pipeline for saving.

- [x] **Step 4: Route controller close actions through MediatR**

Update `NotificationsController` to inject `IMediator`, build a `NotificationCloseActor` from the current principal, call `mediator.Send(...)`, and map results. Keep HTTP request-shape validation in the controller; remove close mutation and direct save logic from the controller.

- [x] **Step 5: Run green tests**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests.NotificationCloseWorkflows_AreTransactionalCommands|FullyQualifiedName~NotificationAlertWorkflowTests.NotificationDetailCloseAndBatchClose_UpdateVisibleAlertsOnly" --logger "console;verbosity=minimal"
```

Expected: both tests pass.

Result: passed `2/2`.

- [x] **Step 6: Broaden verification**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~NotificationAlertWorkflowTests" --logger "console;verbosity=minimal"
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
git diff --check
```

Expected: architecture and notification workflow tests pass, full backend suite passes, whitespace check passes.

Result:

- `CqrsArchitectureTests|NotificationAlertWorkflowTests` passed `9/9`.
- Full backend suite passed `217/217`.
- `git diff --check` passed.

- [x] **Step 7: Update docs, Plane, commit, and push**

Update this checklist, `project_state.md`, and the Plane item with test evidence. Commit only this family-set work on `review-improvements`:

```bash
git add RvtPortal.Spa/Application/Notifications/NotificationCloseCommands.cs RvtPortal.Spa/Api/NotificationsController.cs RvtPortal.Spa.Tests/CqrsArchitectureTests.cs docs/superpowers/plans/2026-06-25-controller-business-logic-family-refactor-plan.md /Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/project_state.md
git commit -m "refactor(api): move notification closes into commands"
git push origin review-improvements
```

Result: Plane issue `#433` received evidence comment `551a217e-8103-45e9-ba41-c2ff54d12a78`; implementation commit `a880268` was pushed to `origin/review-improvements` for Family 1.

## Task 2: Remaining Controller Write Families

**Files:**
- Create: `RvtPortal.Spa/Application/AlertLevels/AlertLevelCommands.cs`
- Create: `RvtPortal.Spa/Application/Contracts/ContractCommands.cs`
- Create: `RvtPortal.Spa/Application/Help/HelpArticleCommands.cs`
- Create: `RvtPortal.Spa/Application/Installers/InstallerDeploymentCommands.cs`
- Create: `RvtPortal.Spa/Application/Monitors/MonitorMutationCommands.cs`
- Create: `RvtPortal.Spa/Application/ReportRules/ReportRuleCommands.cs`
- Create: `RvtPortal.Spa/Application/Sites/SiteCommands.cs`
- Create: `RvtPortal.Spa/Application/Users/UserSiteAssignmentCommands.cs`
- Modify: matching controller files under `RvtPortal.Spa/Api`
- Modify: `RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [x] **Step 1: Write the failing architecture guardrail**

Added `RemainingControllerWriteFamilies_AreRoutedThroughTransactionalCommands` to require the remaining staged controller families to inject `IMediator`, avoid direct `SaveChangesAsync()`, and have command files implementing `ITransactionalRequest`.

Result: red run failed as expected because `AlertLevelsController` had not yet been routed through `IMediator`.

- [x] **Step 2: Move write decisions into commands**

Moved residual writes for alert levels, contracts, help articles, installer deployment location updates, monitor mutations/default alert rules, report rules/report users, sites/site notification settings, and user site assignment into transactional command handlers. Controllers now keep HTTP response mapping, actor/context creation, and DTO rebuilding.

`EfCoreUnitOfWork` now saves both `RVTDbContext` and `RVTSearchContext` because the report-rule commands mutate search-context entities while still relying on the MediatR transaction pipeline as the single save boundary.

- [x] **Step 3: Focused verification while implementing**

Focused suites passed during the family migration:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~NotificationAlertWorkflowTests|FullyQualifiedName~ContractSiteOperationsTests|FullyQualifiedName~ReportWorkflowTests|FullyQualifiedName~HelpCmsOperationsTests|FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~CompanyUserAdminTests|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Result: passed `42/42`.

- [x] **Step 4: Full backend verification**

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --logger "console;verbosity=minimal"
```

Result: passed `233/233`.

## Later Family Acceptance Criteria

- Alert rules: no `SaveChangesAsync()` remains in `AlertLevelsController`; create/update/vibration/delete actions call commands and existing alert-rule workflow tests pass. Completed for the staged alert-rule endpoints in Task 2.
- Contracts/companies: controller CRUD methods contain no entity mutation logic; command handlers return explicit not-found/validation results; workflow tests cover create/update/delete. Completed for contracts in Task 2; companies had already been left outside this staged guardrail because its controller writes are service-mediated rather than direct save calls.
- Sites/help/admin content: file-storage side effects stay behind service abstractions; database and storage writes are coordinated or compensating behavior is documented. Completed for site and Help CMS database writes in Task 2.
- Residual monitor/installer writes: remaining controller write actions use MediatR commands or focused services; existing monitor/installer workflow tests pass. Completed in Task 2.
- Read-heavy family: reader extractions are driven by measurable complexity, testability, or performance needs; no broad `IDbContext` abstraction is introduced until a concrete test seam requires it.

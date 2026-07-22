# FI-CQRS.2 Next Controller CQRS Candidates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Identify the next small, high-risk controller workflows to move behind CQRS/MediatR boundaries after the ARCH.2 controller-family migration.

**Architecture:** Keep the current MediatR transaction pattern: commands that mutate EF-tracked state implement `ITransactionalRequest`, handlers make write decisions, and `TransactionPipelineBehavior` performs the single Unit of Work save/commit. Controllers keep HTTP validation, actor extraction, response mapping, and reloading DTOs for the SPA.

**Tech Stack:** ASP.NET Core controllers, EF Core, ASP.NET Identity, MediatR, existing `IUnitOfWork` / `TransactionPipelineBehavior`, xUnit architecture and workflow tests.

---

## Decision Summary

Recommended implementation order:

1. **FI-CQRS.3 User site-contact assignment commands**: smallest remaining write family, directly adjacent to existing `AddUserToSiteCommand`.
2. **FI-CQRS.4 Company lifecycle commands**: highest small partial-write risk because company delete loops through users, site-user data, Identity deletion, and company deletion.
3. **FI-CQRS.5 User account lifecycle commands**: broader Identity lifecycle risk; split into create/update/status/delete command handlers after company flow establishes the pattern.
4. **FI-CQRS.6 Report-rule recipient reader**: read-heavy authorization/data-shaping extraction to stop loading all users and resolving roles per user in the controller.

Non-prioritized for this pass:

- Help CMS writes already route through `CreateHelpArticleCommand`, `UpdateHelpArticleCommand`, `SetHelpArticlePublicationCommand`, and `DeleteHelpArticleCommand`.
- Contracts, report rules, alert levels, notifications, sites, monitors, installer writes, and monitor removal already have MediatR command coverage from the prior ARCH.2 cycle.
- Reports list reads were already pushed into database-side projection and sorting.

## Evidence Reviewed

- `RvtPortal.Spa/Application/Common/TransactionPipelineBehavior.cs`: transactional requests execute through `IUnitOfWork.ExecuteInTransactionAsync(...)` and one final `SaveChangesAsync(...)`.
- `RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs`: current Unit of Work saves both domain and search contexts.
- `RvtPortal.Spa/Application/Users/UserSiteAssignmentCommands.cs`: `AddUserToSiteCommand` already creates `SiteUsers` plus default `NotificationSettings` atomically.
- `RvtPortal.Spa/Api/UsersController.cs`: `SetSiteContact`, `RemoveSiteContact`, and `RemoveFromSite` still call `IUserService` directly.
- `RvtPortal.Spa/Api/CompaniesController.cs`: create/update/delete still call `ICompanyService`, and delete performs multi-step user cleanup plus Identity deletion in the controller.
- `RvtPortal.Spa/Api/UsersController.cs`: create/update/disable/enable/delete still perform Identity and role mutations in the controller.
- `RvtPortal.Spa/Api/ReportRulesController.cs`: recipient queries build assignment users in the controller, load all Identity users, and call `GetRolesAsync` inside loops.

## Task 1: User Site-Contact Assignment Commands

**Files:**
- Modify: `RvtPortal.Spa/Application/Users/UserSiteAssignmentCommands.cs`
- Modify: `RvtPortal.Spa/Api/UsersController.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/CompanyUserAdminTests.cs`

- [ ] **Step 1: Add a failing architecture assertion**

Extend `RemainingControllerWriteFamilies_AreRoutedThroughTransactionalCommands` or add a focused test that requires `UsersController` to send these commands:

```csharp
SetSiteContactCommand
RemoveSiteContactCommand
RemoveUserFromSiteCommand
```

Expected red result: the controller currently calls `userService.SiteContactSet(...)`, `userService.SiteContactUnSet(...)`, and `userService.DeleteSiteUser(...)`.

- [ ] **Step 2: Add command records and handlers**

Add the three records to `UserSiteAssignmentCommands.cs`:

```csharp
public sealed record SetSiteContactCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed record RemoveSiteContactCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;

public sealed record RemoveUserFromSiteCommand(Guid UserId, Guid SiteId)
    : IRequest<UserSiteAssignmentCommandResult>, ITransactionalRequest;
```

Handlers should load the matching `SiteUsers` row through `RVTDbContext.SiteUsers`, return `SiteNotFound = true` when the assignment is missing, update contact flags in tracked entities, and rely on the transaction pipeline for saving.

- [ ] **Step 3: Route controller methods through MediatR**

Update:

```csharp
SetSiteContact(...)
RemoveSiteContact(...)
RemoveFromSite(...)
```

Each method should send the matching command, map `SiteNotFound` to the existing `SiteNotFound(...)` response, clear lookup cache only when an assignment was removed, and rebuild `BuildSiteAssignmentResponseAsync(...)`.

- [ ] **Step 4: Verify behavior**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CompanyUserAdminTests.UserAdministration_EnforcesRoleRulesAndSupportsStatusAndLinkActions|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: contact set/unset and user removal from site still pass, and the architecture test prevents regression to controller-side service writes.

## Task 2: Company Lifecycle Commands

**Files:**
- Create: `RvtPortal.Spa/Application/Companies/CompanyCommands.cs`
- Modify: `RvtPortal.Spa/Api/CompaniesController.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/CompanyUserAdminTests.cs`

- [ ] **Step 1: Add a failing guardrail**

Require `CompaniesController` to inject/use `IMediator`, and require `CompanyCommands.cs` to exist with `ITransactionalRequest`.

- [ ] **Step 2: Add create/update/delete commands**

Create commands:

```csharp
public sealed record CreateCompanyCommand(string? CompanyName)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;

public sealed record UpdateCompanyCommand(Guid CompanyId, string? CompanyName)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;

public sealed record DeleteCompanyCommand(Guid CompanyId)
    : IRequest<CompanyCommandResult>, ITransactionalRequest;
```

The handlers should validate blank/length/duplicate company name, return not-found for missing company, and move delete orchestration out of the controller: remove site-user data for each company user, delete Identity users, then delete the company.

- [ ] **Step 3: Route controller company mutations through commands**

Keep `BuildDetailAsync(...)` and HTTP response mapping in `CompaniesController`; command results should expose `CompanyId`, `CompanyName`, `NotFound`, and validation errors.

- [ ] **Step 4: Verify behavior**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CompanyUserAdminTests.CompanyCrud_ValidatesUniqueNamesAndDeletesCompanyUsers|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: duplicate validation and company-user cleanup still pass, with company mutation decisions behind transactional command handlers.

## Task 3: User Account Lifecycle Commands

**Files:**
- Create: `RvtPortal.Spa/Application/Users/UserAccountCommands.cs`
- Modify: `RvtPortal.Spa/Api/UsersController.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/CompanyUserAdminTests.cs`

- [ ] **Step 1: Split the command surface**

Add commands for:

```csharp
CreateUserCommand
UpdateUserCommand
DisableUserCommand
EnableUserCommand
DeleteUserCommand
```

Keep email sending as an explicit controller-side post-command side effect unless a later mail outbox exists; that avoids sending email inside a DB transaction.

- [ ] **Step 2: Move Identity mutations into handlers**

Handlers should own `UserManager.CreateAsync`, role add/remove, `UserManager.UpdateAsync`, security stamp update, `IUserService.DeleteUserData`, and `UserManager.DeleteAsync`. Return Identity errors as structured command errors for controller mapping.

- [ ] **Step 3: Verify behavior**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CompanyUserAdminTests.UserAdministration_EnforcesRoleRulesAndSupportsStatusAndLinkActions|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: create/update role rules, status toggles, delete protection, and link actions still pass.

## Task 4: Report-Rule Recipient Reader

**Files:**
- Create: `RvtPortal.Spa/Application/ReportRules/ReportRuleRecipientReader.cs`
- Modify: `RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/ReportWorkflowTests.cs`

- [ ] **Step 1: Add a read-model guardrail**

Add a test that requires `ReportRulesController` to depend on `IReportRuleRecipientReader` and no longer contain `BuildReportCandidateUsersAsync`.

- [ ] **Step 2: Extract recipient loading**

Move the logic that finds active site users, assigned report users, admin users, roles, company names, and active site counts into a reader. Preserve the current response contracts:

```csharp
ReportUserAssignmentResponse
QueryReportUsersResponse
```

The first pass may preserve the same behavior while reducing controller responsibility; later performance work can convert all role/company/site-count shaping to database-side projections.

- [ ] **Step 3: Verify behavior**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~ReportWorkflowTests.ReportRuleUsers_QueryAssignedAndAvailableRecipients|FullyQualifiedName~ReportWorkflowTests.ReportRuleUsers_AddAndRemoveSiteAssignments|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: assigned/available recipients, search, sort, paging, add, and remove flows still pass.

## Plane Follow-Up Recommendation

Create follow-up Plane work items only when implementation starts:

- `[FI-CQRS.3] Move user site-contact assignments into transactional commands`
- `[FI-CQRS.4] Move company lifecycle mutations into transactional commands`
- `[FI-CQRS.5] Move user account lifecycle mutations into transactional commands`
- `[FI-CQRS.6] Extract report-rule recipient reader`

## Verification for This Identification Pass

This pass is documentation-only. Verification should include:

```bash
git diff --check
```

Expected: no whitespace errors.

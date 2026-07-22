# Generic Repository Mapperly Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the legacy generic repository base class and replace reflection-based repository projection mapping with Mapperly, without introducing Dapper.

**Architecture:** Keep EF Core as the primary ORM. Tighten `GenericRepository<TEntity>` around constructor-injected `DbContext` and a cached `DbSet<TEntity>`, then move projection-copy mappings into compile-time generated Mapperly methods. Preserve existing repository interfaces for this phase so callers can migrate incrementally toward explicit EF query readers later.

**Tech Stack:** ASP.NET Core, EF Core 10, Mapperly source generator, xUnit, Plane issue `#431`.

---

### Task 1: Guardrails

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

- [x] **Step 1: Add a failing repository architecture guardrail**

Add a test that reads repository source files and asserts:
- `GenericRepository` has no parameterless constructor.
- `GenericRepository` has no `ReadUncommitted` method.
- `GenericRepository` has no reflection `MapObjects` helper.
- repository files do not call `MapObjects(`.
- `RVT.Dataaccess.csproj` references Mapperly and does not reference Dapper or AutoMapper.
- a Mapperly mapper source file exists in `RVT.Dataaccess/Mappers`.

- [x] **Step 2: Run the red test**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

Expected: fail before implementation because `GenericRepository` still has `ReadUncommitted`, reflection mapping, parameterless construction, and no Mapperly mapper.

### Task 2: Base Repository Refactor

**Files:**
- Modify: `RVT.Dataaccess/GenericRepository.cs`
- Modify: all `RVT.Dataaccess/*Repository.cs` classes deriving from `GenericRepository<TEntity>`

- [x] **Step 1: Refactor the base constructor**

Replace the settable `Context` property and lazy `_dbSet` with:
- `protected DbContext Context { get; }`
- `protected DbSet<TEntity> DbSet { get; }`
- `protected GenericRepository(DbContext context)` with null checking.

- [x] **Step 2: Remove unsafe/obsolete helpers**

Remove:
- `ReadUncommitted()`
- `MapObjects(object source, object destination)`
- unused `System.Transactions` import.

- [x] **Step 3: Update derived repositories**

Change derived constructors from:

```csharp
public ExampleRepository(RVTDbContext ContextDB)
{
    base.Context = ContextDB;
}
```

to:

```csharp
public ExampleRepository(RVTDbContext ContextDB)
    : base(ContextDB)
{
}
```

Keep existing service/repository interfaces unchanged in this phase.

### Task 3: Mapperly Projection Mapping

**Files:**
- Modify: `RVT.Dataaccess/RVT.Dataaccess.csproj`
- Create: `RVT.Dataaccess/Mappers/SearchProjectionMapper.cs`
- Modify: repository files currently calling `MapObjects(...)`

- [x] **Step 1: Add Mapperly**

Add `Riok.Mapperly` as a private-assets source-generator/analyzer package to `RVT.Dataaccess.csproj`.

- [x] **Step 2: Add Mapperly mapper**

Create `SearchProjectionMapper` as an internal static partial Mapperly mapper with explicit mapping methods for the existing repository projection pairs, including monitor/search, notification/user-search, report/user-search, dust/peak-level interval projections, trace projections, and status projections.

- [x] **Step 3: Replace reflection calls**

Change repository `Map(...)` methods from `MapObjects(entity, item)` to strongly typed `SearchProjectionMapper.Map(entity)` calls.

- [x] **Step 4: Remove repository-local reflection helpers**

Delete private `MapObjects` shadows from repositories once call sites use Mapperly.

### Task 4: Verification, Docs, Plane, Commit

**Files:**
- Modify: root `project_state.md`
- Modify: `docs/superpowers/plans/2026-06-25-generic-repository-mapperly-plan.md`

- [x] **Step 1: Run focused guardrails**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"
```

- [x] **Step 2: Run repository/business focused tests**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~ReportWorkflowTests|FullyQualifiedName~NotificationWorkflowTests|FullyQualifiedName~SiteWorkflowTests" --logger "console;verbosity=minimal"
```

- [x] **Step 3: Run full backend suite**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

- [x] **Step 4: Update Plane and docs**

Comment on Plane issue `#431` with files changed, no-Dapper confirmation, and verification results. Update root `project_state.md`.

- [x] **Step 5: Commit and push**

Commit on `review-improvements`. Do not merge to `master`.

---

## Completion Checkpoint - 2026-06-25

Implementation:
- Kept EF Core as the primary ORM; Dapper was not added.
- Added Mapperly `Riok.Mapperly` `4.3.1` as a private source-generator/analyzer package in `RVT.Dataaccess/RVT.Dataaccess.csproj`.
- Added `RVT.Dataaccess/Mappers/SearchProjectionMapper.cs` with explicit typed mappings for the existing repository projection pairs.
- Refactored `GenericRepository<TEntity>` to require constructor-injected `DbContext`, cache `DbSet<TEntity>` once, and remove `ReadUncommitted()` plus the reflection `MapObjects()` helper.
- Updated derived repositories to call `: base(ContextDB)` or `: base(context)`.
- Replaced repository projection `MapObjects(...)` call sites with Mapperly-generated mapping methods and removed repository-local reflection helper shadows.
- Added `CqrsArchitectureTests.GenericRepository_DoesNotUseDirtyReadsOrReflectionMapping`.

Verification:
- Red guardrail failed first on the old parameterless `GenericRepository()`.
- `dotnet build RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -v minimal` passed with `0` warnings and `0` errors.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"` passed `4/4`.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~ReportWorkflowTests|FullyQualifiedName~NotificationWorkflowTests|FullyQualifiedName~SiteWorkflowTests" --logger "console;verbosity=minimal"` passed `21/21`.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"` passed `215/215`.
- `git diff --check` passed.

Plane:
- Created issue `#431` / `079cdb33-93ea-4efb-a50b-bf0fc9e61d8f`: `[REPO.1] Refactor GenericRepository and replace reflection mapping with Mapperly`.

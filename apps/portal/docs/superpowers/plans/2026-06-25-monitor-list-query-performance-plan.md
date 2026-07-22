# Monitor List Query Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move SPA monitor inventory and unattached monitor list filtering, sorting, counting, and paging from in-memory controller code into database-shaped EF Core queries.

**Architecture:** Add a focused `IMonitorListReader` in `RvtPortal.Spa/Application/Monitors` that projects monitor list rows from `RVTDbContext` using queryable expressions. Controllers keep HTTP concerns only and ask the reader for paged results or assignment lists.

**Tech Stack:** ASP.NET Core, EF Core 10, MediatR-adjacent application services, xUnit, existing SPA API DTOs.

---

### Task 1: Add Guardrail Tests

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Test: `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`

- [x] **Step 1: Write failing architecture test**

Add assertions that `MonitorsController` depends on `IMonitorListReader`, the reader file exists, and broad `BuildMonitorRowsAsync` materialization is gone from the controller.

- [x] **Step 2: Run red test**

Run: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"`

Expected: fail because `IMonitorListReader` is not wired yet.

### Task 2: Add Monitor List Reader

**Files:**
- Create: `RvtPortal.Spa/Application/Monitors/MonitorListReader.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`

- [x] **Step 1: Implement reader contracts**

Create:
- `MonitorListQuery`
- `MonitorListPage`
- `MonitorAssignmentLists`
- `IMonitorListReader`

- [x] **Step 2: Implement query projection**

Use `RVTDbContext` queryables to:
- filter archived/type/role/state/search in SQL
- project latest active deployment fields
- compute `HasAlerts` and `HasCautions` using `Any`
- apply sort, count, skip, and take before `ToListAsync`

- [x] **Step 3: Register reader**

Register `IMonitorListReader` as scoped in `ServiceCollectionExtensions`.

### Task 3: Wire Controller To Reader

**Files:**
- Modify: `RvtPortal.Spa/Api/MonitorsController.cs`

- [x] **Step 1: Inject reader**

Add `IMonitorListReader monitorListReader` to the constructor.

- [x] **Step 2: Replace inventory query**

Replace `BuildMonitorRowsAsync` usage in `Query` with `monitorListReader.QueryAsync`.

- [x] **Step 3: Replace unattached query**

Replace `QueryUnattached` broad row loading with `monitorListReader.QueryUnattachedAsync`, then enrich only returned page rows with removal impact.

- [x] **Step 4: Replace assignment row loading**

Replace the assignment endpoint's full row load with `monitorListReader.BuildAssignmentListsAsync`.

- [x] **Step 5: Remove obsolete helpers**

Delete controller methods that only supported in-memory list construction and filtering.

### Task 4: Verify Behavior And Commit

**Files:**
- Modify if needed: `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`
- Modify: root `project_state.md` outside repo

- [x] **Step 1: Run focused tests**

Run: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"`

- [x] **Step 2: Run full backend suite**

Run: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"`

- [x] **Step 3: Update docs and Plane**

Update `project_state.md` and comment on Plane issue `#429` with commit/test evidence.

- [x] **Step 4: Commit on branch**

Commit on `review-improvements`; do not merge to `master`.

---

## Completion Checkpoint - 2026-06-25

Completed on branch `review-improvements`.

Implementation:
- Added `RvtPortal.Spa/Application/Monitors/MonitorListReader.cs` and registered `IMonitorListReader`.
- Updated `RvtPortal.Spa/Api/MonitorsController.cs` so monitor inventory, unattached monitor paging, and assignment lists use database-shaped reader queries.
- Added `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs` guardrails to keep the broad controller-side `BuildMonitorRowsAsync()` path from returning.
- Updated source-file major-update comments and root `project_state.md`.

Verification:
- Red architecture guardrail failed before implementation because `IMonitorListReader` was absent.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~CqrsArchitectureTests" --logger "console;verbosity=minimal"` passed `2/2`.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~MonitorWorkflowTests" --logger "console;verbosity=minimal"` passed `14/14`.
- Combined focused architecture/monitor workflow run passed `16/16`.
- Full backend suite passed `213/213`.
- `git diff --check` passed.

Plane:
- Completed issue `#429` / `48a37ba5-fae2-4729-b2df-66d64f649bb4`: `[PERF.1] Push monitor list filtering and paging into database`.

Benchmark evidence:
- Ran a throwaway benchmark harness from `/private/tmp/rvt-query-bench` against a disposable local Timescale/Postgres container, not the real `rvt` database.
- Seeded dataset: `25,000` monitors, `17,500` active deployments, and `8,334` open notifications.

Speed comparison:

| Scenario | Page size | Total rows | Old shape median | New reader median | Median delta | Speedup |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Broad `online` monitor list, no search | `20` | `6,249` | `237ms` | `42ms` | `195ms` | `5.64x` |
| `not-in-use` monitor list, no search | `20` | `7,500` | `261ms` | `56ms` | `205ms` | `4.66x` |
| Site-search page, search text `site 04` | `20` | `251` | `238ms` | `208ms` | `30ms` | `1.14x` |

- Old shape materialized about `50,854` EF objects per request in this seed; the new reader materialized the `20` requested page rows after database-side count/filter/sort/page.
- Caveat: this is synthetic local relational evidence, not production telemetry. It supports the improvement, especially for broad list pages, but should be followed by production-like profiling once representative data and indexes are available.

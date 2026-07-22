# CA1859 Concrete Type Suggestions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the 53 open SonarCloud `external_roslyn:CA1859` concrete-type performance suggestions without changing public API behavior.

**Architecture:** Treat this as a scoped analyzer cleanup. Local variables and private helpers move from interface/base types to the concrete types that are already constructed and consumed locally; public interfaces stay unchanged unless Sonar flagged only private/internal implementation details.

**Tech Stack:** C#/.NET 10, Roslyn analyzer CA1859, xUnit, ASP.NET Core SPA backend, SonarCloud, Plane.

---

### Task 1: Establish Analyzer Baseline

**Files:**
- Read only: SonarCloud `external_roslyn:CA1859` issue list
- Read only: `RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`

- [x] **Step 1: Fetch the Sonar issue list**

Run:

```bash
curl -sS -u "$SONAR_TOKEN:" -o /private/tmp/ca1859-issues.json "https://sonarcloud.io/api/issues/search?organization=aileron-forward&componentKeys=aileron-forward_rvtportal-spa-alpha&rules=external_roslyn%3ACA1859&statuses=OPEN,CONFIRMED,REOPENED&ps=500"
```

Expected: JSON reports `total: 53`.

- [x] **Step 2: Run analyzer red gate**

Run:

```bash
dotnet build RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -warnaserror:CA1859 -v minimal
```

Expected: FAIL with CA1859 warnings promoted to errors.

Actual: the authenticated SonarCloud query reported `total: 53`, but local builds do not emit CA1859 under this repository's current analyzer settings, even with `-warnaserror:CA1859` and `AnalysisMode=AllEnabledByDefault`. SonarCloud issue inventory is the red baseline for this pass.

### Task 2: Fix BusinessLogic Local List Types

**Files:**
- Modify: `RVT.BusinessLogic/CompanyService.cs`
- Modify: `RVT.BusinessLogic/ContractService.cs`
- Modify: `RVT.BusinessLogic/MonitorService.cs`
- Modify: `RVT.BusinessLogic/NotificationService.cs`
- Modify: `RVT.BusinessLogic/ReportRuleService.cs`
- Modify: `RVT.BusinessLogic/ReportService.cs`
- Modify: `RVT.BusinessLogic/ReportUserService.cs`
- Modify: `RVT.BusinessLogic/SiteService.cs`
- Modify: `RVT.BusinessLogic/UserService.cs`

- [x] **Step 1: Replace local `IList<OrderByProperty>` declarations**

Change only local variables constructed as `new List<OrderByProperty>()`:

```csharp
List<OrderByProperty> orderBy = new List<OrderByProperty>();
```

Expected: no method signatures or repository contracts change.

- [x] **Step 2: Keep behavior identical**

Leave the existing `orderBy.Add(...)` calls and `orderBy.ToArray()` calls unchanged.

### Task 3: Fix Private Helper/Field Concrete Types

**Files:**
- Modify: `RVT.DatabaseMigrator/NameMapping.cs`
- Modify: `RVT.Entities/Querying/FilterExpression.cs`
- Modify: `RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Modify: `RvtPortal.Spa.Tests/DatabaseNamingConventionTests.cs`
- Modify: `RvtPortal.Spa/Api/CustomerLogoStorage.cs`
- Modify: `RvtPortal.Spa/Api/MonitorsController.cs`
- Modify: `RvtPortal.Spa/Api/UsersController.cs`

- [x] **Step 1: Return concrete CSV parser lists where helpers already build `List<string>`**

Use:

```csharp
private static List<string> ParseCsvLine(string line)
```

and for record lists:

```csharp
private static List<IReadOnlyList<string>> ParseCsvRecords(string csv)
```

Expected: callers continue to enumerate/index rows as before.

- [x] **Step 2: Return concrete expression type from string helper**

Use:

```csharp
private static BinaryExpression BuildStringCall(Expression member, string methodName, object? value)
```

Expected: callers still receive it as an `Expression` where needed through implicit upcast.

- [x] **Step 3: Narrow private/static collection fields and helper parameters**

Use `Dictionary<string, string>` for `AllowedContentTypes`, `List<Contract>` for the private monitor DTO helper parameter, and `Dictionary<Guid, string>` for the private user DTO helper parameter.

Expected: no controller action signatures or JSON contracts change.

### Task 4: Verify and Document

**Files:**
- Modify: workspace `project_state.md`
- Modify: source file major-update comments in touched source/test files

- [x] **Step 1: Run analyzer green gate**

Run:

```bash
dotnet build RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -warnaserror:CA1859 -v minimal
```

Expected: PASS with no CA1859 errors.

Actual: `dotnet build RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -v minimal` passed with `0` warnings and `0` errors. The explicit CA1859 warning-as-error variant also passed because local analyzer settings do not emit the Sonar external rule locally.

- [x] **Step 2: Run backend regression tests**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: PASS.

Actual: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"` passed `217/217`.

- [x] **Step 3: Run whitespace check**

Run:

```bash
git diff --check
```

Expected: no output.

Actual: `git diff --check` produced no output.

- [ ] **Step 4: Update Plane and project state**

Add implementation and verification evidence to the Plane issue, move it to Done, and record commit/test evidence in `project_state.md`.

# Sonar Maintainability High Medium Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve current SonarCloud High maintainability issues and the safe Medium maintainability issues, excluding complexity-style refactors.

**Architecture:** Use the live SonarCloud issue snapshot as the source of truth, then apply focused fixes by rule family. Treat mechanical issues such as unused code, readonly fields, generic DI overloads, and simple string comparisons as in scope; treat large public API shape changes such as long parameter lists as documented follow-up unless they are already isolated and tested.

**Tech Stack:** SonarCloud, C#/.NET 10, TypeScript/React/Vitest, xUnit, GitHub Actions/Sonar scanner.

---

### Task 1: Pull and Classify Sonar Issues

**Files:**
- Read: `/private/tmp/sonar-maint-high.json`
- Read: `/private/tmp/sonar-maint-medium.json`
- Modify: `docs/superpowers/plans/2026-06-29-sonar-maintainability-high-medium-plan.md`

- [x] **Step 1: Query SonarCloud High maintainability issues**

Run:

```bash
curl -sS -u "$SONAR_TOKEN:" \
  -o /private/tmp/sonar-maint-high.json \
  "https://sonarcloud.io/api/issues/search?organization=aileron-forward&componentKeys=aileron-forward_rvtportal-spa-alpha&impactSoftwareQualities=MAINTAINABILITY&impactSeverities=HIGH&statuses=OPEN,CONFIRMED,REOPENED&ps=500"
```

Expected: HTTP 200 with 6 issues.

- [x] **Step 2: Query SonarCloud Medium maintainability issues**

Run:

```bash
curl -sS -u "$SONAR_TOKEN:" \
  -o /private/tmp/sonar-maint-medium.json \
  "https://sonarcloud.io/api/issues/search?organization=aileron-forward&componentKeys=aileron-forward_rvtportal-spa-alpha&impactSoftwareQualities=MAINTAINABILITY&impactSeverities=MEDIUM&statuses=OPEN,CONFIRMED,REOPENED&ps=500"
```

Expected: HTTP 200 with 246 issues.

- [x] **Step 3: Filter complexity issues**

Current snapshot has no `S3776`, `S1541`, `S134`, or message text containing `complex`.

### Task 2: Fix High Maintainability Findings

**Files:**
- Modify: `RVT.BusinessLogic/MonitorService.cs`
- Inspect: `RVT.BusinessLogic/IMonitorService.cs`
- Already handled: `RVT.Utilities/AppLogging.cs`

- [x] **Step 1: Align `MonitorService` parameter names with `IMonitorService`**

Fix `csharpsquid:S927` by choosing one naming convention per interface method and applying it consistently to implementation signatures.

- [x] **Step 2: Confirm `AppLogging.cs` is absent**

The two `csharpsquid:S1186` issues are superseded by the active branch deletion of `RVT.Utilities/AppLogging.cs`.

- [x] **Step 3: Run focused backend tests**

Run:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: all backend tests pass.

### Task 3: Fix Safe Medium Mechanical Findings

**Files:**
- Modify by cluster after checking each local file still contains the flagged pattern.
- Add or update focused tests only when behavior can change.

- [x] **Step 1: Remove stale private helpers and commented code**

Handle `S1144` and `S125` only when the method/comment still exists locally and has no callers.

- [x] **Step 2: Apply mechanical C# analyzer improvements**

Handle `CA2263`, `CA1862`, `CA1865`, `CA2249`, `CA1861`, `CA1822`, `CA1859`, `SYSLIB1045`, `S2933`, `S2971`, and `S3358` where local code matches the Sonar issue.

- [x] **Step 3: Apply TypeScript maintainability fixes**

Handle redundant type aliases, optional chaining suggestions, duplicate branch bodies, and ARIA label suggestions where local code matches the Sonar issue.

- [x] **Step 4: Exclude or defer large shape changes**

Document `S107` long parameter list findings and any database script findings that need policy/exclusion rather than source rewrites.

### Task 4: Verify and Document

**Files:**
- Modify: `/Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/project_state.md`

- [x] **Step 1: Run client checks**

Run:

```bash
npm run lint
npm run test:run
npm run build
```

Expected: lint and tests pass; build passes with only known Vite chunk advisory.

- [x] **Step 2: Run backend checks**

Run:

```bash
dotnet build RvtPortal.Spa.sln --configuration Debug -v minimal
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: build and tests pass.

- [x] **Step 3: Update project state**

Record the Sonar baseline, fixed rule families, deferred items, and verification evidence.

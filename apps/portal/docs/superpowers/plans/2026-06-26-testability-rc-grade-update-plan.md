# Testability RC Grade Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the Sonar coverage signal and add richer release-candidate tests that read like real user and business workflows.

**Architecture:** Keep generated/migration ballast out of coverage calculations, but raise real confidence with behavior-first tests around transaction boundaries, reporting, alerts, and client panel interactions. Architecture guardrails remain as support tests, not the main evidence of quality.

**Tech Stack:** GitHub Actions, SonarCloud scanner properties, xUnit integration tests, Vitest/Testing Library, ASP.NET Core test host, React 19.

---

### Task 1: Coverage Scope And CI Evidence

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Create: `docs/testing/testability-rc-grade-update.md`

- [x] Write a failing readiness test that requires `sonar.coverage.exclusions` to exclude EF migrations, EF snapshots, generated entity/search projection models, generated OpenAPI schema, and client build/test outputs.
- [x] Add the scanner property to the Sonar begin command.
- [x] Document the local baseline: backend all-file coverage `51.5%`, backend excluding migrations/projections `62.6%`, SPA API assembly `73.3%`, client line coverage `49.1%`.
- [x] Run the focused readiness test and `git diff --check`.

### Task 2: Backend Behavior Scenarios

**Files:**
- Modify: `RvtPortal.Spa.Tests/TransactionPipelineBehaviorTests.cs`
- Modify: `RvtPortal.Spa.Tests/ReportWorkflowTests.cs`
- Modify: `RvtPortal.Spa.Tests/NotificationAlertWorkflowTests.cs`
- Modify: `RvtPortal.Spa.Tests/CompanyUserAdminTests.cs`

- [x] Add a transaction test whose name describes the business outcome: a failed transactional command must not save partial work.
- [x] Keep existing report workflow scenarios for daily schedules, recipient assignment, and generation request validation as part of the focused RC evidence set.
- [x] Add alert workflow scenarios for closed-note visibility and close authorization.
- [x] Add user/site assignment scenario coverage for default notification settings.
- [x] Run focused backend suites and then the full backend suite.

### Task 3: Client Interaction Scenarios

**Files:**
- Modify: `RvtPortal.Client/src/App.test.tsx`
- Modify if needed: focused component test files under `RvtPortal.Client/src`

- [x] Add user-facing tests around weak coverage panels: admin companies/users, reports, notifications, and monitor workflows.
- [x] Prefer accessible queries, real form changes, and visible outcome assertions over shallow render checks.
- [x] Run `npm run test:coverage`, `npm run lint`, and `npm run build`.

### Task 4: Evidence, Plane, Commit, Push

**Files:**
- Modify: `docs/testing/testability-rc-grade-update.md`
- Modify: workspace `project_state.md`

- [x] Record coverage before/after, commands, and known follow-up gaps.
- [x] Add Plane comments to the four `Testability RC grade update` items.
- [x] Commit on `testability-rc-grade-update` and push the branch.

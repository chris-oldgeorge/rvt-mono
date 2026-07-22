# Monitor Contract Window Data Boundaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure monitor-bound readings, traces, notifications, alert summaries, archive exports, and generated reports are attributed only to the customer/site contract that owned the monitor at the data timestamp.

**Architecture:** Introduce one effective ownership window rule: `max(deployment.StartDate, contract.OnHireDate)` through `min(deployment.EndDate, contract.OffHireDate)`, with open ends capped by the requested/report window where appropriate. Use the same rule in SPA/backend readers, close commands, archive SQL, and the reporting service so monitor-id/serial-id lookups are never enough by themselves.

**Tech Stack:** ASP.NET Core/.NET 10, EF Core, xUnit, Npgsql, Timescale/Postgres canonical SQL, Plane API, QuestPDF reporting service.

---

## File Structure

- Create `RvtPortal.Spa/Application/Monitors/MonitorOwnershipWindow.cs`: shared effective-window helper for deployments/contracts and timestamp/range predicates.
- Modify `RvtPortal.Spa/Api/NotificationsController.cs`: remove current/latest deployment fallback; related notifications are same ownership window only.
- Modify `RvtPortal.Spa/Application/Notifications/NotificationCloseCommands.cs`: use the same timestamp match for close authorization and batch close.
- Modify `RvtPortal.Spa/Api/DashboardController.cs`: scope open notification counts, map/list alert flags, and calendar notification queries by effective ownership window.
- Modify `RvtPortal.Spa/Application/Monitors/MonitorListReader.cs`: ensure `HasAlerts`/`HasCautions` check current deployment effective window.
- Modify `RvtPortal.Spa/Application/Monitors/MonitorDetailReader.cs`: constrain recent notifications and fallback reading to the selected deployment window.
- Modify `RvtPortal.Spa/Api/DataController.cs` and `RvtPortal.Spa/Api/MonitorDataSource.cs`: clamp trace list/detail/download to effective deployment window.
- Modify `RVT.BusinessLogic/MonitorData.cs`: clamp normal measurement data and trace detail metadata to effective deployment/contract bounds.
- Modify `RVT.BusinessLogic/Archive/SiteArchiveService.cs`: update raw SQL joins to use effective deployment/contract dates.
- Modify `/Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/Source Code/rvt-reporting-new/src/Rvt.Reporting.Core/Models/ReportModels.cs`: carry effective report window per monitor.
- Modify `/Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/Source Code/rvt-reporting-new/src/Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs`: attach averages, notifications, and alert rule triggered counts only inside each monitor's report ownership window.
- Modify tests in `RvtPortal.Spa.Tests/NotificationAlertWorkflowTests.cs`, `DashboardMapCalendarTests.cs`, `DataViewTests.cs`, `MonitorWorkflowTests.cs`, `SiteArchiveServiceSecurityTests.cs`, and reporting service tests.
- Update `project_state.md` and source-file major-update comments.

## Task 1: Plane And Plan Tracking

- [x] **Step 1: Save this plan**

Run:
```bash
test -f docs/superpowers/plans/2026-06-26-monitor-contract-window-data-boundaries-plan.md
```

- [x] **Step 2: Create Plane item**

Create `[DATA.1] Enforce contract windows on monitor-bound data` in the RVT Plane project and move it to In Progress with a comment linking this plan.

## Task 2: SPA Notification Ownership Regressions

- [x] **Step 1: Write failing tests**

Add tests that seed one monitor with an old ended deployment/contract and a new active deployment/contract:

- company user on the new site must not see or close an old notification outside the new effective window.
- admin notification detail must not attribute a notification to current/latest deployment when no deployment window matches.
- related notifications in detail must stay inside the selected notification's ownership window.

- [x] **Step 2: Verify red**

Run:
```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~NotificationAlertWorkflowTests"
```

Expected: new moved-monitor notification tests fail because fallback deployment matching still attributes old notifications to current/latest deployment.

- [x] **Step 3: Implement notification helper usage**

Add `MonitorOwnershipWindow` and update notification list/detail/close lookup to match only timestamp-inside-effective-window deployments.

- [x] **Step 4: Verify green**

Run the same focused test command and expect all `NotificationAlertWorkflowTests` to pass.

## Task 3: SPA Dashboard, Monitor List, Detail, And Calendar

- [x] **Step 1: Write failing tests**

Add tests proving old open notifications do not set current monitor alert flags/counts, monitor detail only shows deployment-window notifications, and calendar day/month queries do not include notifications outside the selected deployment/contract window.

- [x] **Step 2: Verify red**

Run:
```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~DashboardMapCalendarTests|FullyQualifiedName~MonitorWorkflowTests"
```

Expected: new tests fail due monitor-only open notification and detail queries.

- [x] **Step 3: Implement scoped notification queries**

Apply effective-window predicates to dashboard rows, visible open notifications, calendar queries, monitor list `HasAlerts`/`HasCautions`, and monitor detail recent notification queries.

- [x] **Step 4: Verify green**

Run the same focused command and expect the touched suites to pass.

## Task 4: SPA Data Views And Archive SQL

- [x] **Step 1: Write failing tests**

Add tests proving trace list/detail/download exclude traces outside the deployment/contract window and that archive SQL uses contract bounds as well as deployment bounds.

- [x] **Step 2: Verify red**

Run:
```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~DataViewTests|FullyQualifiedName~SiteArchiveServiceSecurityTests"
```

Expected: trace boundary test and archive SQL contract-bound assertion fail.

- [x] **Step 3: Implement data/archive boundary**

Clamp trace list/detail/download and `MonitorData.GetDeploymentData` to the effective window. Update archive SQL joins to use the effective start/end from deployment and contract dates.

- [x] **Step 4: Verify green**

Run the same focused command and expect the touched suites to pass.

## Task 5: Reporting Service Ownership Windows

- [x] **Step 1: Write failing repository tests**

Add unit-level SQL-shape tests and/or repository tests proving monitor child data is filtered by each monitor's effective report window rather than only report period plus serial/monitor id.

- [x] **Step 2: Verify red**

Run:
```bash
dotnet test tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --configuration Release --filter "FullyQualifiedName~TimescaleSchemaIntegrationTests"
```

Expected: new SQL-shape guard fails because averages/notifications/rule trigger counts do not join against monitor-specific effective windows.

- [x] **Step 3: Implement report windows**

Add `EffectiveFrom`/`EffectiveTo` to `MonitorReportData`, calculate them in `ReadMonitorDataAsync`, and filter average points, notifications, and alert-rule notification joins by those window values.

- [x] **Step 4: Verify green**

Run the focused reporting tests and expect them to pass, skipping real Timescale checks when the gated connection string is absent.

## Task 6: Documentation, Plane Evidence, And Broad Verification

- [x] **Step 1: Update documentation**

Update source major-update comments and `/Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/project_state.md` with affected paths, rule definition, verification results, and known follow-up.

- [x] **Step 2: Run broad verification**

Run:
```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~NotificationAlertWorkflowTests|FullyQualifiedName~DashboardMapCalendarTests|FullyQualifiedName~DataViewTests|FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~SiteArchiveServiceSecurityTests"
dotnet test /Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/Source\ Code/rvt-reporting-new/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --configuration Release
git diff --check
```

- [x] **Step 3: Update Plane**

Add implementation/test evidence to `[DATA.1] Enforce contract windows on monitor-bound data` and move it to Done if verification passes.

## Self-Review

- Spec coverage: covers SPA/backend data views, notifications, dashboards, monitor detail/list, archive SQL, and reporting service child-data loading.
- Placeholder scan: no TBD placeholders; each task has concrete paths and verification commands.
- Type consistency: uses existing `Deployment`, `Contract`, `MonitorReportData`, notification, and measurement concepts already present in the codebase.

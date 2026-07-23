# Phase 12 Cutover Runbook

Date: 2026-05-24

Scope:

- Close the MVC-to-SPA migration plan.
- Confirm the SPA/API host remains deployable as one artifact.
- Confirm the clean repository remains free of the retired MVC project and migration-control tooling.

## Deployment Pipeline

The cutover artifact is `RvtPortal.Spa`. It owns Identity, API endpoints, static file serving, SPA fallback, Swagger, and the publish target that copies `RvtPortal.Client/dist` into `wwwroot`.

Repository path: `RvtPortal.Spa/RvtPortal.Spa.csproj`

Required CI gates:

```powershell
dotnet restore .\RvtPortal.Spa.sln
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --configuration Release -v minimal
dotnet publish .\RvtPortal.Spa\RvtPortal.Spa.csproj --configuration Release --output .\artifacts\rvtportal-spa -v minimal
```

Required client gates from `RvtPortal.Client`:

```powershell
npm ci
npm run lint
npm run build
npm run test:run
npm run test:e2e
```

Publish artifact checks:

- `wwwroot/index.html` exists.
- `wwwroot/assets` contains hashed JS/CSS output.
- `RvtPortal.Spa.dll` exists in the publish folder.
- `/api/health/live` returns `200` for the container liveness probe.
- `/api/health/ready` returns `200` only after database connectivity and schema validation succeed; use this endpoint as the deployment gate.
- A non-API route returns the SPA shell.
- An unknown `/api/*` route returns `application/problem+json`.

## Role Journey Sign-Off

Run these journeys in staging against the published `RvtPortal.Spa` artifact.

| Role | Required journey | Expected result |
|---|---|---|
| RVTMasterAdmin | Sign in, view dashboard, open breaches/alerts, navigate Companies, Users, Reports, Maps, Calendar, and Data. | All administrative routes render, protected API calls return `200`, and no legacy MVC route is required. |
| RVTAdmin | Sign in, manage companies/users/contracts/sites, edit monitor detail, update alert levels, close notifications, and open generated report links. | CRUD and assignment workflows persist through SPA APIs and return validation errors without full-page MVC postbacks. |
| RVTInstaller | Sign in, open assigned installer monitor list, edit deployment fields, check status, and confirm admin-only routes are hidden. | Installer can complete deployment/status workflow and receives access denied for admin routes. |
| CompanyUser | Sign in, open scoped dashboard/sites/monitors/notifications/data, update own notification settings, and verify hidden company data is inaccessible. | Data is scoped to assigned company/site records, and forbidden routes do not leak rows. |
| Anonymous | Open `/login`, `/forgot-password`, `/privacy`, unknown public route, and direct `/api/*` call. | Public pages render as intended; API calls require auth unless explicitly anonymous. |

Browser evidence:

- `npm run test:e2e` covers anonymous login shell, admin Companies, admin Users, and installer forbidden route behavior.
- Manual staging sign-off should record user, browser, environment URL, date/time, and any deviations.

## Data Compatibility

The SPA migration uses the existing database schema and shared business/data-access projects:

- `RvtPortal.Spa` references `RVT.BusinessLogic`, `RVT.DataAccess`, `RVT.Entities`, and `RVT.Utilities`.
- `ApplicationDbContext`, `RVTDbContext`, and `RVTSearchContext` still point at the configured `DefaultConnection`.
- No Phase 12 schema migration is required.
- Site archive remains DB-only in the SPA path until an owner approves any legacy blob/archive side-effect retirement.
- Generated reports continue to open existing stored report links; manual report generation is forwarded by the SPA backend to the configured reporting service.

Compatibility smoke:

```powershell
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --configuration Release --filter "CompanyUserAdminTests|ContractSiteOperationsTests|MonitorWorkflowTests|NotificationAlertWorkflowTests|ReportWorkflowTests|DashboardMapCalendarTests|DataViewTests" -v minimal
```

## Rollback Plan

Rollback stays branch/artifact based. MVC retirement has already been completed in this clean repository, so rollback uses the last release-system MVC artifact and captured configuration rather than rebuilding MVC from this repository.

1. Stop routing new traffic to the SPA host.
2. Re-deploy the last known-good MVC artifact from the release system.
3. Restore the prior application configuration values for MVC host bindings, secrets, connection strings, Redis, data protection, and blob/key vault settings.
4. Verify MVC login, dashboard, monitors, notifications, and reports with the production smoke account set.
5. Keep the database unchanged unless an incident owner explicitly approves a restore. The SPA path does not introduce a Phase 12 schema migration.
6. Record the incident/cutover result in Plane and this tracker before retrying cutover.

Rollback validation:

```powershell
dotnet build .\RvtPortal.Spa.sln --configuration Release -v minimal
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --configuration Release -v minimal
```

## MVC Retirement

MVC retirement is complete in this clean repository. The historical parity matrix remains as release evidence, while the active solution contains only the SPA/API host, shared projects, client, and tests.

The completed retirement criteria were:

- Phase 12 parity matrix has no unclassified MVC action or view.
- CI publish and staging smoke gates pass.
- Product owner signs off the role journeys.
- Operations confirms production traffic is served by `RvtPortal.Spa`.
- A rollback artifact and configuration snapshot are available.
- Error monitoring shows no new cutover blocker for one agreed observation window.

Clean-repo expectations:

1. Keep `RvtPortal.Spa.sln` buildable in Release.
2. Keep release evidence under `docs/release`.
3. Do not reintroduce retired MVC project files or migration-control tooling.
4. Keep rollback artifacts and configuration snapshots in the release system.

## Go/No-Go Checklist

| Gate | Go condition | Evidence source |
|---|---|---|
| Build | Solution Release build has 0 errors. | CI and local verification. |
| API tests | Full `RvtPortal.Spa.Tests` suite passes. | CI and Phase 12 notes. |
| Client tests | Lint, Vite build, Vitest, and Playwright pass. | CI/local verification. |
| Publish | `dotnet publish` produces SPA host artifact with `wwwroot/index.html`. | CI/local publish folder. |
| Security | NuGet and npm audits show no unresolved vulnerable packages. | Phase 11/12 verification. |
| Parity | `PARITY_MATRIX.md` preserves the historical MVC action and view classification evidence. | Release docs and readiness test. |
| Rollback | Last MVC artifact/config snapshot is identified. | Release owner sign-off. |
| Owner sign-off | Role journey sign-off recorded. | Plane work items and release notes. |

Decision:

- Go: deploy `RvtPortal.Spa` artifact and monitor.
- No-go: keep MVC serving production, fix blockers on a new branch, and rerun this checklist.

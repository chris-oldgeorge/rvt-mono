# Functionality Readiness Matrix

Verification note, 2026-05-26: Tasks 6 and 7 passed, and no alpha defect register entries were opened.

| Capability | Backend Evidence | Frontend Evidence | Browser/Manual Evidence | Alpha Status |
|---|---|---|---|---|
| Auth, session, roles, profile, password flows | `AuthEndpointTests`; `SecurityHardeningTests` for cookie/session hardening, `SameSite`, authorization behavior, and `Server-Timing` coverage. | `RvtPortal.Client/src/App.test.tsx`; `RvtPortal.Client/src/App.tsx`; auth/profile shell behavior in client API contracts. | `RvtPortal.Client/tests/e2e/auth-shell.spec.ts` covers login shell, role navigation, and installer access denial; Task 7 staging sign-off passed. | READY |
| Companies | `CompanyUserAdminTests` for company list/detail/create/edit/delete and authorization paths. | `RvtPortal.Client/src/admin/AdminPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; company routes and navigation. | `auth-shell.spec.ts` covers admin Companies route visibility and mocked company data; Task 7 manual CRUD pass completed. | READY |
| Users and site assignments | `CompanyUserAdminTests` for user admin, invite/reset/status/delete, and site assignment workflows. | `RvtPortal.Client/src/admin/AdminPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; user routes and site assignment panels. | `auth-shell.spec.ts` covers admin Users route and user detail rendering; Task 7 role journey sign-off passed. | READY |
| Contracts | `ContractSiteOperationsTests` for contract list/detail/create/edit/delete and authorization behavior. | `RvtPortal.Client/src/operations/ContractSitePanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; contract route rendering. | Task 7 browser/manual contract CRUD journey passed. | READY |
| Sites and notification settings | `ContractSiteOperationsTests`; `NotificationAlertWorkflowTests` for site notification setting paths where applicable; ID18 customer-logo upload/read/delete/internal-fetch coverage. | `RvtPortal.Client/src/operations/ContractSitePanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; site detail/edit, notification setting panels, and customer-logo controls. | Task 7 browser/manual site and notification-setting journey passed; 2026-06-24 ID18 customer-logo tests/build passed. | READY |
| Monitors and installer workflows | `MonitorWorkflowTests` for monitor inventory, assignment, deployment, installer status, and role-scoped access. | `RvtPortal.Client/src/operations/MonitorPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; installer/admin monitor routes. | `auth-shell.spec.ts` covers installer forbidden admin route behavior; Task 7 installer workflow sign-off passed. | READY |
| Notifications | `NotificationAlertWorkflowTests` for open/caution/all lists, detail, close, and batch close workflows. | `RvtPortal.Client/src/operations/NotificationAlertPanels.tsx`; `RvtPortal.Client/src/operations/DashboardPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`. | Task 7 browser/manual notification close and batch-close journey passed. | READY |
| Alert levels | `NotificationAlertWorkflowTests` for alert-level CRUD and threshold behavior. | `RvtPortal.Client/src/operations/NotificationAlertPanels.tsx`; monitor alert-level entry points in `RvtPortal.Client/src/operations/MonitorPanels.tsx`. | Task 7 browser/manual alert-level edit journey passed. | READY |
| Reports and report rules | `ReportWorkflowTests` and `ReportGenerationClientTests` for generated reports, report-rule CRUD, DB-side list paging, reporting-service manual generation handoff, and paged recipient assignment grids. | `RvtPortal.Client/src/operations/ReportPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; report navigation, guided setup steps, generation action, recipient grids, and Help CMS guideline display hook. | Task 7 browser/manual report journey passed; 2026-06-24 reporting upgrade tests and builds passed. | READY |
| Dashboards | `DashboardMapCalendarTests` for dashboard summary, role-specific dashboard data, and notification/breach evidence. | `RvtPortal.Client/src/operations/DashboardPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; dashboard summary rendering. | Task 7 role dashboard browser/manual sign-off passed. | READY |
| Maps | `DashboardMapCalendarTests` for map-marker API and site/user map behavior. | `RvtPortal.Client/src/operations/DashboardPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; `/maps` route rendering. | Task 7 browser/manual map route sign-off passed. | READY |
| Calendar | `DashboardMapCalendarTests` for month/day calendar data and deployment selection behavior. | `RvtPortal.Client/src/operations/DashboardPanels.tsx`; `RvtPortal.Client/src/App.test.tsx`; `/calendar` route rendering. | Task 7 browser/manual calendar route sign-off passed. | READY |
| Data grids, graphs, traces, CSV | `DataViewTests` for grid, graph, trace, and CSV/download data endpoints. | `RvtPortal.Client/src/operations/DataViewPanels.tsx`; `RvtPortal.Client/src/components/DataGrid.test.tsx`; `RvtPortal.Client/src/App.test.tsx`. | Task 7 browser/manual data-view journey passed. | READY |
| Ancillary privacy/error/not-found routes | `AncillaryRoutesTests` for privacy, error, not-found, SPA fallback, and API problem routes. | `RvtPortal.Client/src/App.test.tsx`; `RvtPortal.Client/src/App.tsx`; error boundary and route handling. | `App.test.tsx` covers privacy and not-found rendering; Task 7 browser/manual ancillary route check passed. | READY |
| Security/accessibility/observability | `SecurityHardeningTests` for endpoint authorization, strict session cookie behavior, security headers, problem responses, and `Server-Timing`. | `RvtPortal.Client/src/App.test.tsx`; role navigation guards and accessible route assertions across operations panels. | `auth-shell.spec.ts` covers role-based navigation/access checks; Task 7 accessibility/security smoke passed. | READY |

## Alpha Verification Evidence

- Backend restore/build/test: `dotnet restore RvtPortal.Spa.sln` exited 0 with known NU1510 warnings; `dotnet build RvtPortal.Spa.sln -c Release --no-restore -v minimal` exited 0 with 3 known NU1510 warnings; `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -c Release --no-build -v minimal` passed 73/73.
- Client install/lint/build/test: official Node runtime `/tmp/node-v24.11.1-darwin-arm64` was used because Codex-bundled Node cannot load the Rollup native module due to macOS library validation; `npm ci` added/audited 270 packages with 0 vulnerabilities; `npm run lint` passed; `npm run build` passed with the existing Vite large-chunk warning; `npm run test:run` passed 27/27; `npm run test:e2e` passed 4/4.
- Publish smoke: `dotnet publish RvtPortal.Spa/RvtPortal.Spa.csproj -c Release --output /tmp/rvtportal-spa-alpha-publish -v minimal` exited 0, and `wwwroot/index.html`, `RvtPortal.Spa.dll`, and hashed assets existed.
- Runtime smoke: `ASPNETCORE_ENVIRONMENT=Testing dotnet run --no-launch-profile --project RvtPortal.Spa/RvtPortal.Spa.csproj --urls http://127.0.0.1:5178` started with Hosting environment `Testing`; `/api/health` and `/swagger/v1/swagger.json` returned HTTP 200.
- SPA integration configuration: `RvtPortal.Spa/RvtPortal.Spa.csproj` defines `SpaRoot` as `..\RvtPortal.Client\`, `SpaProxyServerUrl` as `http://localhost:5173`, and `SpaProxyLaunchCommand` as `npm run dev:vs`; `RvtPortal.Client/package.json` includes the `dev:vs` script.

## Reporting Upgrade Evidence - 2026-06-24

- Backend: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -v minimal` passed in the Windows VM: 196/196 tests.
- Backend build: `dotnet build RvtPortal.Spa/RvtPortal.Spa.csproj --no-restore -v minimal` passed in the Windows VM with 0 warnings and 0 errors after restore.
- Frontend tests: `npm test -- --run` passed: 4 files, 41 tests.
- Frontend build: `npm run build` passed in the Windows VM; Vite reported the existing large chunk warning.
- Reporting upgrades now include DB-side report/report-rule search paging, manual generation requests wired to the reporting service, paged available/assigned recipient grids, guided rule setup steps, and Help CMS-backed alert-rule guideline hooks.

## ID18 Customer Logo Evidence - 2026-06-24

- Backend: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -v minimal` passed: 198/198 tests.
- Frontend tests: `npm test -- --run` passed: 4 files, 42 tests.
- Frontend build: `npm run build` passed inside the Windows VM; Vite reported the existing large chunk warning.
- Reporting service: `dotnet build Rvt.Reporting.New.slnx -v minimal` passed with 0 warnings and 0 errors; `dotnet test Rvt.Reporting.New.slnx -v minimal` passed 12/12 tests.
- ID18 now includes Site admin customer-logo upload/delete/preview, protected portal logo streaming, internal report-content fetch, and reporting-service PDF handoff.

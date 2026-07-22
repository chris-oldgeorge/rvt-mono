# Reporting Upgrades Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the SPA reporting module with manual report generation requests, scalable recipient assignment, production-grade report querying, a guided report setup wizard, and managed alert-rule guideline content.

**Architecture:** Keep the SPA backend as the boundary for reporting management. The SPA calls `RvtPortal.Spa` APIs, and the backend reaches the external reporting service through an injectable HTTP client configured by `ReportGenerationService` settings. Managed guideline content should reuse the existing Help/FAQ CMS pattern where practical.

**Tech Stack:** ASP.NET Core API controllers, Entity Framework Core, React/TypeScript, existing `DataGrid`, existing Help CMS entities/API patterns, Vitest/Testing Library, .NET API tests.

---

## Implementation Outcome - 2026-06-24

Implemented:

- Report and report-rule list APIs now compose EF queries for search, sort, count, and page retrieval before materialization.
- Manual report generation is exposed through `POST /api/report-rules/{id}/generation-requests` and an injectable `IReportGenerationClient`; the SPA backend now calls the containerized reporting service at `/internal/reports/rules/{reportRuleId}/generate`.
- Report recipient assignment now uses searchable, sortable, paged available/assigned user grids in the SPA, backed by `available-users` and `assigned-users` endpoints. The existing `/users` assignment summary endpoint remains backward-compatible.
- Report-rule setup now has guided setup steps in the rule form and routes newly created rules into recipient assignment.
- Alert-rule guideline content reuses the existing Help CMS: published Help articles with content type `Alert Rule Guideline` are returned with report-rule options/details and rendered in the setup panel when present.

Follow-up work:

- Validate the configured report-service URL/key against a running `rvt-reporting-new` instance and the target Timescale reporting schema.
- If RVT wants a dedicated multi-step route, split the current guided setup form into `/reports/rules/wizard`; current implementation keeps the journey inside the existing add/edit form.
- Consider a dedicated Help CMS content-type selector/shortcut for `Alert Rule Guideline`; the existing generic content-type field already supports the content.
- Recipient candidate lookup still derives role membership through `UserManager` before paging. It is paged at the API boundary, but a deeper role-query optimization may be useful for very large tenants.

Verification:

- `dotnet build RvtPortal.Spa/RvtPortal.Spa.csproj --no-restore -v minimal` in the Windows VM passed with 0 warnings and 0 errors after restore.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -v minimal` in the Windows VM passed: 196 passed, 0 failed.
- `npm test -- --run` passed: 4 files, 41 tests.
- `npm run build` in the Windows VM passed; Vite emitted the existing large chunk warning.

Plane:

- Created cycle `Reporting upgrades` (`50092a99-8755-4ac3-8f77-a539a3f97c68`) and six implementation work items.
- Updated all six work items to `Done`; cycle readback shows 6 total issues and 6 completed.

## Current Context

Active repo:

- Windows VM: `C:\Users\oldgeorge\source\repos\chris-oldgeorge\rvtportal-spa-alpha`
- Mac mount: `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtportal-spa-alpha`

Existing reporting files:

- `RvtPortal.Spa/Api/ReportsController.cs`
- `RvtPortal.Spa/Api/ReportRulesController.cs`
- `RvtPortal.Spa/Api/ReportApiContracts.cs`
- `RvtPortal.Client/src/operations/ReportPanels.tsx`
- `RvtPortal.Client/src/api/client.ts`
- `RvtPortal.Client/src/dtos.ts`
- `RvtPortal.Client/src/App.tsx`
- `RvtPortal.Client/src/App.test.tsx`

Existing Help CMS files to reuse:

- `RvtPortal.Spa/Api/HelpController.cs`
- `RvtPortal.Spa/Api/HelpApiContracts.cs`
- `RvtPortal.Client/src/admin/HelpAdminPanel.tsx`
- `RvtPortal.Client/src/help/HelpPage.tsx`
- Help CMS EF entities under `RVT.DataAccess/EntityModels/Models`

## File Ownership

### Backend API

- Modify `RvtPortal.Spa/Api/ReportsController.cs`
  - Move reports list filtering/sorting/paging into database queries.
- Modify `RvtPortal.Spa/Api/ReportRulesController.cs`
  - Move report-rule list filtering/sorting/paging into database queries.
  - Add paged assigned-recipient and available-user endpoints.
  - Add generation-request endpoint.
- Modify `RvtPortal.Spa/Api/ReportApiContracts.cs`
  - Add generation request/response DTOs.
  - Add recipient query DTOs.
  - Add wizard DTOs if needed.
- Create `RvtPortal.Spa/Api/ReportGenerationClient.cs`
  - Add `IReportGenerationClient`, reporting-service HTTP client, and response models.
- Modify `RvtPortal.Spa/ServiceCollectionExtensions.cs`
  - Register report generation client abstraction.
- Modify `RvtPortal.Spa/Api/HelpApiContracts.cs`
  - Add alert guideline content type constants or response fields if extending Help CMS.
- Modify `RvtPortal.Spa/Api/HelpController.cs`
  - Add filtering/helpers for guideline content if needed.

### Frontend

- Modify `RvtPortal.Client/src/operations/ReportPanels.tsx`
  - Add manual generation buttons.
  - Add report wizard route/panel.
  - Replace recipient dropdown with paged searchable grids.
  - Surface guideline content in wizard.
- Modify `RvtPortal.Client/src/api/client.ts`
  - Add functions for generation request, paged recipient queries, and guideline reads.
- Modify `RvtPortal.Client/src/dtos.ts`
  - Add TypeScript DTOs matching backend contracts.
- Modify `RvtPortal.Client/src/App.tsx`
  - Add wizard route awareness if `ReportPanels` needs route-level entry.
- Modify `RvtPortal.Client/src/admin/HelpAdminPanel.tsx`
  - Add `AlertRuleGuideline` content type option or create guideline-specific entry points.

### Tests and Documentation

- Modify `RvtPortal.Client/src/App.test.tsx`
  - Add report generation, recipient grid, wizard, and guideline display tests.
- Add/modify backend tests under `RvtPortal.Spa.Tests`
  - Add report API query and mutation tests following existing API test style.
- Modify `docs/release/PARITY_MATRIX.md`
  - Update report functionality notes.
- Modify `docs/release/FUNCTIONALITY_READINESS_MATRIX.md`
  - Add upgraded reporting readiness notes.
- Modify root `project_state.md`
  - Add completion state, verification results, and known follow-up work.

## Task 1: Backend Report Query Refactor

**Files:**

- Modify `RvtPortal.Spa/Api/ReportsController.cs`
- Modify `RvtPortal.Spa/Api/ReportRulesController.cs`
- Test `RvtPortal.Spa.Tests`

- [ ] Add focused tests proving `/api/reports` applies search, sort, and paging without returning all rows.
- [ ] Replace `ToListAsync()` before filtering in `ReportsController.Query` with `IQueryable<ReportSearch>` filtering.
- [ ] Use a sort whitelist that maps requested fields to `OrderBy`/`OrderByDescending` expressions.
- [ ] Apply `CountAsync()` before `Skip`/`Take`.
- [ ] Project only the requested page to `ReportListItem`.
- [ ] Repeat the pattern for `ReportRulesController.Query`, preferring `ReportRuleSearches` or a direct EF projection that includes site name.
- [ ] Run backend API tests.

Expected behavior:

- Invalid sort fields still return `400`.
- Admin report visibility remains unchanged.
- `/api/reports` and `/api/report-rules` return the same DTO shapes as before.
- Large datasets are paged by the database rather than filtered in process memory.

## Task 2: Manual Report Generation API and SPA Button

**Files:**

- Create `RvtPortal.Spa/Api/ReportGenerationClient.cs`
- Modify `RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify `RvtPortal.Spa/Api/ReportApiContracts.cs`
- Modify `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify `RvtPortal.Client/src/api/client.ts`
- Modify `RvtPortal.Client/src/dtos.ts`
- Modify `RvtPortal.Client/src/operations/ReportPanels.tsx`
- Test `RvtPortal.Client/src/App.test.tsx`

- [ ] Add DTOs:
  - `ReportGenerationRequest` with `ReportDate`, `Frequency`, and `SendToRecipients`.
  - `ReportGenerationRequestResponse` with `Id`, `ReportRuleId`, `Status`, and `Message`.
- [ ] Add `IReportGenerationClient.RequestGenerationAsync(...)`.
- [x] Implement `ReportingServiceReportGenerationClient` that calls the containerized report service with `X-RVT-Internal-Key`.
- [ ] Add `POST /api/report-rules/{id}/generation-requests`.
- [ ] Add client API function `requestReportGeneration(ruleId, request)`.
- [ ] Add a row action to `/reports/rules`: `Generate report`.
- [ ] Add a button to the edit rule panel: `Generate now`.
- [ ] Show success/error notice from the API response.
- [ ] Add tests covering the button and accepted/deferred response.

Expected behavior:

- SPA never calls the reporting Azure Function directly.
- The backend API provides a stable contract for the report service.
- When the report service is configured and reachable, manual generation requests are forwarded to the service and return the generated/accepted status.

## Task 3: Paged Searchable Recipient Assignment

**Files:**

- Modify `RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify `RvtPortal.Spa/Api/ReportApiContracts.cs`
- Modify `RvtPortal.Client/src/api/client.ts`
- Modify `RvtPortal.Client/src/dtos.ts`
- Modify `RvtPortal.Client/src/operations/ReportPanels.tsx`
- Test `RvtPortal.Client/src/App.test.tsx`

- [ ] Add recipient query DTOs:
  - `QueryReportUsersRequest`
  - `QueryReportUsersResponse : PagedResponse<UserListItem>`
  - `ReportUserAssignmentSummaryResponse`
- [ ] Add endpoint `GET /api/report-rules/{id}/available-users`.
- [ ] Update endpoint `GET /api/report-rules/{id}/users` to support search, page, page size, sort, and sort direction while preserving the existing assignment summary where needed.
- [ ] Keep `POST /users` and `DELETE /users/{userId}` unchanged for mutations.
- [ ] Replace the available-user dropdown in `ReportRuleUsersPanel` with a `DataGrid`.
- [ ] Add a second `DataGrid` for assigned recipients.
- [ ] Add live/debounced search inputs for both grids.
- [ ] Refresh only the affected grids after add/remove.
- [ ] Add tests for searching, paging, adding, and removing recipients.

Expected behavior:

- Large report sites remain usable.
- Assigned recipients are still visible even if the user is no longer an active site user.
- Admin users can still be selected as report recipients.

## Task 4: Report Generation Wizard

**Files:**

- Modify `RvtPortal.Client/src/operations/ReportPanels.tsx`
- Modify `RvtPortal.Client/src/api/client.ts`
- Modify `RvtPortal.Client/src/dtos.ts`
- Modify `RvtPortal.Spa/Api/ReportRulesController.cs` if a wizard summary endpoint is needed.
- Test `RvtPortal.Client/src/App.test.tsx`

- [ ] Add route parsing for `/reports/rules/wizard`.
- [ ] Add entry button `Guided Setup` from `/reports` and `/reports/rules`.
- [ ] Build wizard steps:
  - Site.
  - Report scope.
  - Schedule.
  - Recipients.
  - Guidelines.
  - Review.
- [ ] Reuse existing report-rule create API for final save.
- [ ] Reuse new recipient APIs for recipient selection.
- [ ] Add optional `Generate first report now` checkbox that calls the generation request endpoint after successful rule creation.
- [ ] Keep form state local to the wizard until final save.
- [ ] Add tests for required validation, schedule branching, recipient selection, and successful save.

Expected behavior:

- The wizard creates the same `ReportRule` and `ReportUser` records as the existing form.
- The existing quick edit form remains available for experienced admins.
- Users get explanatory text during setup without adding in-app instruction clutter elsewhere.

## Task 5: Managed Alert Rule Guidelines

**Files:**

- Modify `RvtPortal.Spa/Api/HelpApiContracts.cs`
- Modify `RvtPortal.Spa/Api/HelpController.cs`
- Modify `RvtPortal.Client/src/admin/HelpAdminPanel.tsx`
- Modify `RvtPortal.Client/src/api/client.ts`
- Modify `RvtPortal.Client/src/dtos.ts`
- Modify `RvtPortal.Client/src/operations/ReportPanels.tsx`
- Test `RvtPortal.Client/src/App.test.tsx`

- [ ] Add supported Help CMS content type `AlertRuleGuideline`.
- [ ] Add monitor-type metadata support for guidelines using a stable field in the mutation/response contract.
- [ ] Add admin filtering for `AlertRuleGuideline`.
- [ ] Add guideline API read helper for report setup, filtered by monitor type.
- [ ] Add guideline editor support in `HelpAdminPanel` so admins can create/publish guideline blocks.
- [ ] Show published guideline blocks in the wizard Guidelines step based on selected site's monitor types.
- [ ] Add tests for admin guideline editing and wizard guideline display.

Expected behavior:

- RVT admins manage guideline content in the same CMS style as Help/FAQ.
- Report setup can show dynamic guidance for Dust, Noise, Vibration, and future monitor types.
- Draft guidelines do not appear in report setup.

## Task 6: Verification and Documentation

**Files:**

- Modify `docs/release/PARITY_MATRIX.md`
- Modify `docs/release/FUNCTIONALITY_READINESS_MATRIX.md`
- Modify `/Users/oldgeorge/Library/CloudStorage/OneDrive-aileron.gr/Aileron/IKH/project_state.md`

- [ ] Run backend tests:
  - `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release -v minimal`
- [ ] Run client tests:
  - `npm test -- --run`
- [ ] Run solution build:
  - `dotnet build RvtPortal.Spa.sln --configuration Release -v minimal`
- [ ] Update parity matrix with reporting upgrade details.
- [ ] Update functionality readiness matrix with manual generation request, recipient grids, wizard, and guideline CMS.
- [ ] Update root `project_state.md` with changed files, verification results, and follow-up items.

Expected behavior:

- Existing reporting routes still work.
- New routes and APIs are covered by focused tests.
- Documentation reflects the new reporting management scope.

## Plane Work Items

Create a Plane cycle named `Reporting upgrades` and add these items:

1. Reporting API production query refactor.
2. Manual report generation request API and SPA action.
3. Searchable/paged report recipient assignment.
4. Report generation setup wizard.
5. Managed alert-rule guideline CMS content.
6. Reporting upgrade tests and documentation.

## Open Decisions

- Whether the first manual generation implementation should persist requests in a new database table or return a stateless deferred response until the report service is wired.
- Whether guideline monitor type should be stored as a structured Help CMS field or encoded in a general metadata field.
- Whether the report wizard should be admin-only like the existing report route, or prepared for future company-user self-service.

# ID18 Customer Logo Reporting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add customer logo upload/preview on the Site admin page and make the logo available to generated reports alongside the RVT logo.

**Architecture:** Store customer logos in SPA application content storage under `App_Data/customer-logos` using a deterministic `siteId` filename, avoiding a database migration. The SPA backend exposes admin upload/delete and protected read endpoints, plus an internal service-to-service fetch endpoint for `rvt-reporting-new`. The reporting service fetches the logo through the SPA backend and passes it into the PDF renderer.

**Tech Stack:** ASP.NET Core controllers, React/Vite, FormData uploads, protected file streaming, HttpClient, QuestPDF, xUnit/Vitest.

---

## Tasks

### Task 1: SPA backend logo storage and API

**Files:**
- Create `RvtPortal.Spa/Api/CustomerLogoStorage.cs`
- Modify `RvtPortal.Spa/Api/SitesController.cs`
- Modify `RvtPortal.Spa/Api/ContractSiteApiContracts.cs`
- Modify `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Test `RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`

- [x] Write failing tests for upload, protected read, detail logo URL, delete, and internal fetch.
- [x] Add `ICustomerLogoStorage` with local `App_Data/customer-logos` save/open/delete behavior.
- [x] Add site endpoints:
  - `POST /api/sites/{id}/customer-logo`
  - `DELETE /api/sites/{id}/customer-logo`
  - `GET /api/sites/{id}/customer-logo`
  - `GET /api/report-content/sites/{id}/customer-logo`
- [x] Add `CustomerLogoUrl` to `SiteDetailResponse`.
- [x] Register storage and internal API key options.
- [x] Run focused backend tests.

### Task 2: SPA site admin UI

**Files:**
- Modify `RvtPortal.Client/src/api/client.ts`
- Modify `RvtPortal.Client/src/dtos.ts`
- Modify `RvtPortal.Client/src/operations/ContractSitePanels.tsx`
- Test `RvtPortal.Client/src/App.test.tsx`

- [x] Write failing client test for site edit logo upload and current logo link.
- [x] Add `uploadSiteCustomerLogo` and `deleteSiteCustomerLogo` API functions.
- [x] Add `customerLogoUrl` to `SiteDetailResponse`.
- [x] Add upload/replace/delete controls to the Site edit panel.
- [x] Run focused Vitest coverage.

### Task 3: Reporting service customer logo fetch/render

**Files:**
- Modify `Source Code/rvt-reporting-new/src/Rvt.Reporting.Core/Models/ReportModels.cs`
- Modify `Source Code/rvt-reporting-new/src/Rvt.Reporting.Core/Reports/ReportGenerationContracts.cs`
- Modify `Source Code/rvt-reporting-new/src/Rvt.Reporting.Core/Reports/ReportGenerationService.cs`
- Create `Source Code/rvt-reporting-new/src/Rvt.Reporting.Storage/PortalContent/SpaCustomerLogoClient.cs`
- Modify `Source Code/rvt-reporting-new/src/Rvt.Reporting.Pdf/Documents/QuestPdfReportRenderer.cs`
- Modify `Source Code/rvt-reporting-new/src/Rvt.Reporting.Service/Program.cs`
- Test `Source Code/rvt-reporting-new/tests/Rvt.Reporting.Core.Tests/Reports/ReportGenerationServiceTests.cs`

- [x] Write failing reporting-service test proving a logo fetch is attempted for generated reports.
- [x] Add a `CustomerLogo` model and `ICustomerLogoProvider`.
- [x] Fetch logo data after site data load and pass it to PDF renderer.
- [x] Render customer logo beside the report header when present.
- [x] Register SPA logo client with base URL/internal key configuration.
- [x] Run reporting service tests.

### Task 4: Plane/docs/verification

**Files:**
- Modify `README.md`
- Modify `docs/release/FUNCTIONALITY_READINESS_MATRIX.md`
- Modify workspace `project_state.md`

- [x] Create/update Plane item `ID18 - Customer Logo`.
- [x] Update docs with customer-logo configuration and verification notes.
- [x] Run backend tests/build and reporting service tests/build.
- [ ] Commit and push both repositories if verification passes.

## Verification

- SPA backend: `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -v minimal` passed, 198/198 tests.
- SPA frontend: `npm test -- --run` passed, 42/42 tests.
- SPA frontend build: `npm run build` passed inside the Windows VM; Vite reported the existing large chunk warning.
- Reporting service: `dotnet build Rvt.Reporting.New.slnx -v minimal` passed with 0 warnings and 0 errors.
- Reporting service tests: `dotnet test Rvt.Reporting.New.slnx -v minimal` passed, 12/12 tests.

# Hexagonal Edges Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adopt a pragmatic ports-and-adapters structure around the RVT Portal SPA backend edges without changing frontend-facing endpoints.

**Architecture:** Keep controllers as inbound HTTP adapters, keep business/application services as use-case coordinators, and keep external systems/storage/database-specific behavior behind explicit ports. This is an incremental refactor, not a whole-system rewrite.

**Tech Stack:** .NET 10, ASP.NET Core controllers, MediatR, EF Core, RVT.BusinessLogic, RVT.DataAccess, xUnit.

## Global Constraints

- Existing API routes and response DTOs must not change.
- C# types remain PascalCase.
- Database identifiers and scripts are unchanged by this pass.
- Source comment summaries and major-update notes must be updated in touched source files.
- All architectural changes must be recorded in `docs/architecture/hexagonal-edges-change-log.md`.
- The final ports-to-adapters map must be recorded in `docs/architecture/ports-and-adapters-catalog.md`.

---

### Task 1: Baseline Architecture Guardrails

**Files:**
- Modify: `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

**Interfaces:**
- Consumes: current controller and adapter types.
- Produces: reflection guardrails proving report-rule orchestration and storage dependencies use ports.

- [x] Add failing reflection tests that require `ReportRulesController` to depend on `IReportRuleApplicationService` instead of `IReportGenerationClient`.
- [x] Add failing reflection tests that require monitor/customer storage ports to live outside the API namespace.
- [x] Run `dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter "FullyQualifiedName~CqrsArchitectureTests"`.

### Task 2: Storage Ports And Upload Adapter

**Files:**
- Create: `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs`
- Move/adapt: `RvtPortal.Spa/Api/CustomerLogoStorage.cs` to `RvtPortal.Spa/Adapters/Storage/CustomerLogoStorage.cs`
- Move/adapt: `RvtPortal.Spa/Api/MonitorPictureStorage.cs` to `RvtPortal.Spa/Adapters/Storage/MonitorPictureStorage.cs`
- Create: `RvtPortal.Spa/Adapters/Storage/FormFileUpload.cs`
- Modify: `RvtPortal.Spa/Api/SitesController.cs`
- Modify: `RvtPortal.Spa/Api/MonitorsController.cs`
- Modify: `RvtPortal.Spa/Application/Monitors/UploadMonitorPictureCommand.cs`
- Modify: `RvtPortal.Spa/Application/Monitors/MonitorDetailReader.cs`
- Modify: `RvtPortal.Spa/Api/ReportContentController.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`

**Interfaces:**
- Produces: `IUploadedContent`, `StoredContentFile`, `IMonitorPictureStorage`, `ICustomerLogoStorage`, and `StorageValidationException` in the business-layer port namespace.
- Consumes: ASP.NET `IFormFile` only in inbound adapters/controllers.

- [x] Introduce transport-neutral storage port models in `RVT.BusinessLogic`.
- [x] Wrap `IFormFile` with `FormFileUpload` at controller boundaries.
- [x] Update storage implementations to consume `IUploadedContent`.
- [x] Register storage adapters against business-layer ports.

### Task 3: Report Generation Adapter Boundary

**Files:**
- Move/adapt: `RvtPortal.Spa/Api/ReportGenerationClient.cs` to `RvtPortal.Spa/Adapters/Reporting/ReportGenerationClient.cs`
- Move/adapt: `RvtPortal.Spa/Api/ReportGenerationGateway.cs` to `RvtPortal.Spa/Adapters/Reporting/ReportGenerationGateway.cs`
- Modify: `RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify: `RvtPortal.Spa/Api/ApiResultMapper.cs`
- Modify: `RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify tests that override `IReportGenerationClient`.

**Interfaces:**
- Consumes: `IReportRuleApplicationService`, `IReportGenerationGateway`, and existing API DTOs.
- Produces: controller routing through the business use-case service while preserving the manual generation endpoint contract.

- [x] Register `IReportRuleApplicationService`, `IReportGenerationGateway`, `IPortalUserDirectory`, and current-user context adapters.
- [x] Route `POST /api/report-rules/{id}/generation-requests` through `IReportRuleApplicationService`.
- [x] Preserve downstream status codes carried by `ApplicationResult.StatusCode`.

### Task 4: Catalog And Verification

**Files:**
- Modify: `docs/architecture/hexagonal-edges-change-log.md`
- Create: `docs/architecture/ports-and-adapters-catalog.md`
- Modify: workspace `project_state.md` after verification.

**Interfaces:**
- Produces: durable documentation of inbound adapters, outbound ports, adapters, and unresolved follow-up boundaries.

- [x] Record all changed ports, adapters, and controller/business seams.
- [x] Run focused architecture/report/storage tests.
- [x] Run full backend test project if focused checks pass.
- [x] Update `project_state.md` with branch, affected paths, verification results, and follow-up work.

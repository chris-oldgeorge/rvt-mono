# Hexagonal Edges Change Log

This log records the incremental ports-and-adapters refactor for the RVT Portal SPA backend.

## 2026-07-08

- Started branch: `hexagonal-edges`.
- Scope selected: report-generation orchestration, customer-logo storage, monitor-picture storage, and DI/catalog documentation.
- Non-goals: no endpoint route changes, no DTO shape changes, no database schema changes, no broad controller rewrite.
- Added architecture guardrails in `RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`:
  - `ReportRulesController` must enter through `IReportRuleApplicationService`.
  - `ReportRulesController` must not depend directly on `RVTDbContext`, `RVTSearchContext`, or `IReportGenerationClient`.
  - Storage abstractions must live in `RVT.BusinessLogic.Ports.Storage`.
  - Storage implementations must live in `RvtPortal.Spa.Adapters.Storage`.
- Added storage ports in `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs`:
  - `IUploadedContent`
  - `StoredContentFile`
  - `ICustomerLogoStorage`
  - `IMonitorPictureStorage`
  - `StorageValidationException`
- Moved customer-logo and monitor-picture implementations from `RvtPortal.Spa.Api` into `RvtPortal.Spa.Adapters.Storage`.
- Added `FormFileUpload` to keep ASP.NET Core `IFormFile` at the HTTP adapter boundary.
- Updated monitor picture command handling to use `IUploadedContent` instead of `IFormFile`.
- Moved report-generation HTTP client and gateway into `RvtPortal.Spa.Adapters.Reporting`.
- Routed `ReportRulesController` through `IReportRuleApplicationService`, `ICurrentUserContextFactory`, and `IApiResultMapper`.
- Registered report-rule application services, user-directory adapter, current-user adapter, storage ports, and report-generation gateway in `ServiceCollectionExtensions`.
- Preserved archived report-rule site behavior inside the business layer:
  - Archived current sites remain present as disabled edit options.
  - New mutations targeting archived sites keep the archived-specific validation message.
- Updated report workflow/client tests to target the reporting adapter namespace.
- Cleanup pass:
  - Removed the reporting gateway dependency on API mapper types so the outbound adapter no longer points back into the inbound API mapping layer.
  - Replaced archive-service console exception output with trace logging.
  - Centralized legacy synchronous lookup repository reads behind one helper and removed stale lookup comment/semicolon noise.
  - Removed console prompts and async-without-await warnings from the legacy blob utility.
- Transaction-boundary pass:
  - Added `docs/superpowers/plans/2026-07-08-transaction-boundaries.md`.
  - Extended `EfCoreUnitOfWork` to coordinate `RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext`.
  - Registered one scoped provider `DbConnection` for the portal EF contexts so Identity/domain/search writes can share a relational transaction.
  - Added relational SQLite tests that prove normal saves persist all three contexts and immediate Identity/search writes roll back when a later domain save fails.
  - Made customer-logo replacement write to a temporary file before replacing the old logo.
  - Added monitor-picture cleanup to `IMonitorPictureStorage` and compensating delete behavior when picture upload persistence fails.
  - Added `Microsoft.EntityFrameworkCore.Sqlite` test coverage with `SQLitePCLRaw.lib.e_sqlite3` pinned to `3.53.3` so the test project stays free of the 2.1.11 native SQLite advisory.
- Planned verification:
  - Focused architecture tests in `CqrsArchitectureTests`.
  - Existing report workflow tests.
  - Existing customer-logo and monitor-picture workflow tests.
  - Full `RvtPortal.Spa.Tests` project after focused checks pass.

Verification so far:

- Red check: focused `CqrsArchitectureTests` initially failed because `RVT.BusinessLogic.Ports.Storage` and `RvtPortal.Spa.Adapters.*` did not exist.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~CqrsArchitectureTests'` passed 6/6.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~ReportGenerationClientTests|FullyQualifiedName~ReportWorkflowTests'` passed 11/11 after restoring archived-site validation wording.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --no-build --filter 'FullyQualifiedName~ContractSiteOperationsTests|FullyQualifiedName~MonitorWorkflowTests'` passed 21/21.
- Green check: `dotnet format RvtPortal.Spa.sln --verify-no-changes --no-restore --verbosity minimal` passed.
- Green check: `dotnet list RvtPortal.Spa.sln package --vulnerable --include-transitive` found no vulnerable packages.
- Green check: `dotnet build RvtPortal.Spa.sln -m:1` passed with 0 warnings and 0 errors.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --no-build` passed 232/232.
- Red check: focused transaction/storage tests first failed because `EfCoreUnitOfWork` did not accept `ApplicationDbContext`.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~EfCoreUnitOfWorkTests|FullyQualifiedName~StorageAdapterTests|FullyQualifiedName~MonitorPictureCommandTests'` passed 5/5.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --no-build --filter 'FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~DatabaseNamingConventionTests|FullyQualifiedName~CutoverReadinessTests|FullyQualifiedName~DatabaseProviderConfigurationTests'` passed 87/87.
- Green check: `dotnet build RvtPortal.Spa.sln -m:1 --no-restore` passed with 0 warnings and 0 errors.
- Green check: `dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --no-build` passed 237/237.
- Green check: `dotnet list RvtPortal.Spa.sln package --vulnerable --include-transitive` found no vulnerable packages.
- Green check: `npm run test:run` in `RvtPortal.Client` passed 66/66; existing React `act(...)` warnings remain in `src/App.test.tsx`.

# RVT Portal Ports And Adapters Catalog

This catalog records the current pragmatic "hexagonal at the edges" structure. The goal is not a textbook rewrite; it is to make volatile boundaries explicit so controllers stay thin, business workflows stay testable, and infrastructure choices remain swappable.

## Rule Of Thumb

- `RvtPortal.Spa.Api`: inbound HTTP adapter. It owns routes, auth attributes, request normalization, response status codes, and DTO mapping.
- `RVT.BusinessLogic`: application/business layer. It owns use-case orchestration, validation, business result models, and outbound port interfaces.
- `RVT.DataAccess`: persistence adapter. It owns EF contexts, repositories, provider-specific database plumbing, and canonical database mappings.
- `RvtPortal.Spa.Adapters`: host-owned outbound adapters for systems that need ASP.NET Core, HTTP, file paths, Blob clients, or Identity.

## Inbound Adapters

| Inbound adapter | Use-case boundary | Notes |
|---|---|---|
| `RvtPortal.Spa.Api.ReportRulesController` | `RVT.BusinessLogic.Reports.IReportRuleApplicationService` | Thin HTTP adapter for report-rule list/detail/options/mutations/recipients/manual generation. Keeps API routes and DTOs unchanged. |
| `RvtPortal.Spa.Api.SitesController` | Existing MediatR commands plus storage port calls | Customer-logo upload remains an HTTP endpoint, but upload content is wrapped as `IUploadedContent` before it crosses into storage. |
| `RvtPortal.Spa.Api.MonitorsController` | MediatR monitor commands/readers plus storage port calls | Monitor picture upload wraps `IFormFile` in `FormFileUpload`; command handlers no longer depend on ASP.NET upload types. |
| Other API controllers | Existing service/MediatR boundaries | These remain candidates for future incremental controller thinning, especially where controllers still query EF directly. |

## Outbound Ports

| Port | Location | Adapter | Purpose |
|---|---|---|---|
| `IReportGenerationGateway` | `RVT.BusinessLogic/Reports/IReportGenerationGateway.cs` | `RvtPortal.Spa.Adapters.Reporting.ReportGenerationGateway` | Business-layer port for manual report generation orchestration. The adapter maps reporting-service responses locally so it does not depend on API-layer DTO mappers. |
| `IReportGenerationClient` | `RvtPortal.Spa/Adapters/Reporting/ReportGenerationClient.cs` | `ReportingServiceReportGenerationClient` | Adapter-internal HTTP client for the containerized reporting service. This is not injected into controllers. |
| `ICustomerLogoStorage` | `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs` | `RvtPortal.Spa.Adapters.Storage.CustomerLogoStorage` | Stores and streams customer logos without exposing file-system details to API/business workflows. |
| `IMonitorPictureStorage` | `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs` | `RvtPortal.Spa.Adapters.Storage.MonitorPictureStorage` | Stores, streams, and deletes monitor pictures through local App_Data or Azure Blob-backed storage so handlers can compensate failed database persistence. |
| `IUploadedContent` | `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs` | `RvtPortal.Spa.Adapters.Storage.FormFileUpload` | Keeps ASP.NET Core `IFormFile` out of application command/storage port signatures. |
| `IPortalUserDirectory` | `RVT.BusinessLogic/Application/Users/IPortalUserDirectory.cs` | `RvtPortal.Spa.Api.PortalUserDirectory` | Business-layer Identity user lookup port used by report-recipient and site workflows. |

## Persistence Adapters

| Persistence boundary | Location | Notes |
|---|---|---|
| EF contexts | `RVT.DataAccess/Context` | `RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext` remain the persistence adapters. The portal host registers one scoped provider `DbConnection` for these contexts so database-backed command handlers can share a transaction. |
| Unit of Work | `RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs` | Coordinates domain, search, and Identity persistence for MediatR commands. Non-database side effects stay outside this transaction and need compensation or post-commit dispatch. |
| Provider selection | `RVT.DataAccess/Configuration` | `RvtDatabaseProvider`, shared-connection EF options, `IRvtDatabaseConnectionFactory`, and related provider options keep SQL Server/Postgres differences outside controllers. |
| Repositories/search projections | `RVT.DataAccess` | Existing repository interfaces are still registered by `InitBusinessLogic`. Future work should avoid adding new controller-owned persistence queries where a business use case already exists. |

## Current Dependency Direction

```text
React SPA
  -> RvtPortal.Spa.Api controllers
    -> RVT.BusinessLogic use cases and ports
      -> RVT.DataAccess EF/repository adapters
      -> RvtPortal.Spa.Adapters.Reporting
      -> RvtPortal.Spa.Adapters.Storage
```

## Follow-Up Candidates

- Move remaining EF-heavy site/monitor/report read paths out of controllers only where there is real business logic or provider-specific behavior.
- Consider moving `PortalUserDirectory` and `CurrentUserContextFactory` from `Api` into `Adapters.Identity` once the next Identity-related boundary is touched.
- Keep simple CRUD endpoints simple; do not add ports just to satisfy architecture symmetry.

# RVT Portal Ports And Adapters Catalog

This catalog records the current pragmatic "hexagonal at the edges" structure. The goal is not a textbook rewrite; it is to make volatile boundaries explicit so controllers stay thin, business workflows stay testable, and infrastructure choices remain swappable.

## Rule Of Thumb

- `RvtPortal.Spa.Api`: inbound HTTP adapter. It owns routes, auth attributes, request normalization, response status codes, and DTO mapping.
- `RvtPortal.Application`: BCL-only application boundary for extracted slices. It owns Sites use cases, policies, transport-neutral contracts, results, and inward-facing ports.
- `RVT.BusinessLogic`: legacy application/business boundary for slices not yet extracted. Move those slices deliberately and incrementally; do not move them opportunistically while changing unrelated behavior.
- `RVT.DataAccess`: persistence adapter. It owns EF contexts, repositories, provider-specific database plumbing, and canonical database mappings.
- `RvtPortal.Spa.Adapters`: host-owned outbound adapters for systems that need ASP.NET Core, HTTP, file paths, Blob clients, or Identity.

## Inbound Adapters

| Inbound adapter | Use-case boundary | Notes |
|---|---|---|
| `RvtPortal.Spa.Api.ReportRulesController` | `RVT.BusinessLogic.Reports.IReportRuleApplicationService` | Thin HTTP adapter for report-rule list/detail/options/mutations/recipients/manual generation. Keeps API routes and DTOs unchanged. |
| `RvtPortal.Spa.Api.SitesController` | `RvtPortal.Application.Sites.ISiteApplicationService` | Complete Sites HTTP adapter. Routes, authorization attributes, payloads, request normalization, ProblemDetails mapping, and file responses remain at the HTTP edge. |
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
| `ISiteReadPort` | `RvtPortal.Application/Sites/Ports/ISiteReadPort.cs` | `RvtPortal.Spa.Adapters.Sites.EfSiteReadAdapter` | Materialized Sites reads with SQL-side filtering, counting, sorting, paging, and projection. |
| `ISiteWritePort` | `RvtPortal.Application/Sites/Ports/ISiteWritePort.cs` | `RvtPortal.Spa.Adapters.Sites.EfSiteWriteAdapter` | Explicit staged Sites mutations and the relational conditional contract claim. |
| `IApplicationUnitOfWork` | `RvtPortal.Application/Common/IApplicationUnitOfWork.cs` | `RvtPortal.Spa.Application.Common.EfCoreUnitOfWork` | Application-facing execute/save transaction semantics backed by the existing shared three-context transaction adapter. |
| `IPortalUserDirectory` | `RvtPortal.Application/Identity/IPortalUserDirectory.cs` | `RvtPortal.Spa.Api.PortalUserDirectory` | Application-owned Identity lookup port used by Sites and remaining legacy report-recipient workflows. |
| `ISiteArchivePort` | `RvtPortal.Application/Sites/Ports/ISiteArchivePort.cs` | `RvtPortal.Spa.Adapters.Sites.SiteArchiveAdapter` | Creates the external site archive after the application management gate and before the application transaction records archive state. |
| `ISiteLogoPort` | `RvtPortal.Application/Sites/Ports/ISiteLogoPort.cs` | `RvtPortal.Spa.Adapters.Sites.SiteLogoAdapter` | Saves, deletes, checks, and opens protected customer-logo content through BCL stream contracts. |

## Persistence Adapters

| Persistence boundary | Location | Notes |
|---|---|---|
| EF contexts | `RVT.DataAccess/Context` | `RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext` remain the persistence adapters. The portal host registers one scoped provider `DbConnection` for these contexts so database-backed command handlers can share a transaction. |
| Unit of Work | `RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs` | Implements both the legacy host `IUnitOfWork` and application-owned `IApplicationUnitOfWork`. It coordinates domain, search, and Identity persistence over the existing scoped provider connection. Non-database side effects stay outside this transaction and need compensation or post-commit dispatch. |
| Provider selection | `RVT.DataAccess/Configuration` | Shared-connection Npgsql EF options, `IRvtDatabaseConnectionFactory`, and related database options keep PostgreSQL infrastructure outside controllers. |
| Repositories/search projections | `RVT.DataAccess` | Existing repository interfaces are still registered by `InitBusinessLogic`. Future work should avoid adding new controller-owned persistence queries where a business use case already exists. |

## Current Dependency Direction

```text
RvtPortal.Spa.Api.SitesController
  -> RvtPortal.Application.Sites.ISiteApplicationService
    -> ISiteReadPort / ISiteWritePort
    -> IApplicationUnitOfWork
    -> IPortalUserDirectory
    -> ISiteArchivePort / ISiteLogoPort
      -> RvtPortal.Spa.Adapters.Sites
        -> EF Core / Identity / archive export / customer-logo storage
```

`SiteApplicationService.ArchiveAsync` applies
`SiteAuthorizationPolicy.CanManage` before archive-state reads, external
export, transaction entry, persistence, or response enrichment. The host role
attribute remains an HTTP-edge defense, while the application boundary
independently protects direct callers.

## Follow-Up Candidates

- Evaluate remaining monitor, report, company/user, contract, notification, dashboard, and alert-level slices independently; this catalog does not select the next extraction.
- Consider moving `PortalUserDirectory` and `CurrentUserContextFactory` from `Api` into `Adapters.Identity` once the next Identity-related boundary is touched.
- Keep simple CRUD endpoints simple; do not add ports just to satisfy architecture symmetry.

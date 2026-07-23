# RVT Portal Sites Application-Boundary Design

## Decision

Introduce a new `apps/portal/RvtPortal.Application` project and use the Sites
feature as the first complete vertical slice moved behind that boundary.

`RvtPortal.Application` will own the Sites use cases, request/result contracts,
authorization policies, and outbound ports. `RvtPortal.Spa` will remain the
composition root and HTTP adapter. It will also temporarily contain the EF
Core, ASP.NET Identity, archive, and customer-logo adapters that implement the
new application-owned ports.

This is an incremental extraction, not a rewrite. Other portal slices remain
where they are until they are moved separately.

## Goals

- Establish a compile-time application boundary that does not depend on
  ASP.NET Core, EF Core, `RVT.DataAccess`, the web host, or vendor SDKs.
- Move the complete Sites behavior behind application interfaces, including
  reads, mutations, authorization, archive orchestration, notification
  settings, and customer-logo workflows.
- Keep every existing `/api/sites` route, request payload, response payload,
  status code, paging rule, sort rule, and not-found authorization behavior.
- Retain the optimized provider-specific EF queries and the existing
  three-context shared transaction implementation as outward adapters.
- Create a repeatable extraction pattern for later portal slices.

## Non-goals

- Do not reorganize every existing portal application service.
- Do not introduce a generic repository or expose `IQueryable` through a port.
- Do not replace MediatR across the portal.
- Do not replace `EfCoreUnitOfWork`, the shared scoped database connection, or
  the database schema.
- Do not change React client contracts, OpenAPI shapes, routes, roles, storage
  providers, or archive formats.
- Do not move API DTO mapping, `ControllerBase`, `IFormFile`, `FileResult`,
  `ClaimsPrincipal`, or ASP.NET Identity into the application project.

## Current State

The current Sites slice is split across:

- `RvtPortal.Spa/Api/SitesController.cs`, which owns HTTP mapping and directly
  invokes customer-logo storage;
- `RvtPortal.Spa/Application/Sites/SiteApplicationService.cs`, which directly
  uses `RVTDbContext`, EF LINQ, `IPortalUserDirectory`,
  `ISiteArchiveService`, and `TimeProvider`;
- `RvtPortal.Spa/Application/Sites/SiteCommands.cs`, which contains a second
  set of site write rules and MediatR handlers but is not the controller's
  Sites facade;
- `RVT.BusinessLogic/Sites/SiteApplicationModels.cs`, which contains the
  transport-neutral Sites contracts;
- `RvtPortal.Spa/Application/Sites/ActiveSiteAssignment.cs`, which contains
  the inclusive assignment-window rule but expresses it over an EF entity.

The primary service is over 1,200 lines and mixes use-case decisions with EF
query construction and entity mutation. The extraction must separate those
responsibilities without throwing away the efficient SQL projections already
present.

## Target Dependency Direction

```text
RvtPortal.Spa
  ├── HTTP controllers and API DTO mappers
  ├── DI composition
  ├── EF/Identity/storage/archive adapters
  └── references RvtPortal.Application
                         │
                         ▼
RvtPortal.Application
  ├── Sites use cases
  ├── Sites contracts and results
  ├── current-user and assignment policies
  └── outbound ports

RVT.DataAccess / ASP.NET Identity / Azure and storage SDKs
  are used only by RvtPortal.Spa adapters.
```

The new application project will target `net10.0`, use nullable reference
types and implicit usings, and have no NuGet package references. Its initial
production dependency surface is BCL-only. If a later slice requires a domain
project reference, that is a separate reviewed decision rather than an
implicit expansion of this boundary.

## Proposed Project Layout

```text
apps/portal/
  RvtPortal.Application/
    RvtPortal.Application.csproj
    Common/
      UseCaseResult.cs
      PageRequest.cs
      PagedResult.cs
      IApplicationUnitOfWork.cs
    Identity/
      PortalUserContext.cs
    Sites/
      ISiteApplicationService.cs
      SiteContracts.cs
      SiteAuthorizationPolicy.cs
      ActiveSiteAssignment.cs
      SiteApplicationService.cs
      Ports/
        ISiteReadPort.cs
        ISiteWritePort.cs
        ISiteArchivePort.cs
        ISiteLogoPort.cs
        IPortalUserDirectory.cs
  RvtPortal.Application.Tests/
    RvtPortal.Application.Tests.csproj
    Sites/
  RvtPortal.Spa/
    Adapters/Sites/
      EfSiteReadAdapter.cs
      EfSiteWriteAdapter.cs
      SiteArchiveAdapter.cs
      SiteLogoAdapter.cs
```

The precise file split may be refined during implementation, but ownership and
dependency direction may not.

## Application Contracts

`ISiteApplicationService` remains the controller-facing facade. It retains the
existing list, options, detail, monitors, open notifications, create, update,
archive, authorization, and notification-setting operations. It gains the
three customer-logo use cases that currently bypass the facade:

- save or replace a logo;
- delete a logo;
- open a protected logo.

Application-owned inputs and outputs use only BCL types. A logo upload is
represented by a stream, length, content type, and file name; the controller
maps `IFormFile` into that shape. A logo download result contains the stream
and response metadata; the controller maps it to `File(...)`.

The Sites slice receives its own application result and paging primitives so
the first extraction does not force a cross-portal migration of the legacy
`RVT.BusinessLogic.Application` types. Later slices should converge on the new
primitives and remove the legacy copies incrementally.

## Port Design

### Read port

`ISiteReadPort` exposes focused, materialized operations shaped around the
Sites use cases. It performs database filtering, counting, sorting, paging, and
projection inside the adapter. It never returns EF entities, `DbSet`,
`IQueryable`, provider expressions, or a `DbContext`.

It covers:

- visible site list queries and counters;
- site form options;
- site detail data;
- monitor and open-notification pages;
- notification settings;
- site existence and active-assignment checks;
- validation lookups for site name, company, and contract;
- the post-write detail reads needed for compatible responses.

### Write port

`ISiteWritePort` exposes explicit site operations rather than CRUD:

- create a site and attach its initial contract;
- update mutable fields and operating hours;
- mark a site archived with archive metadata;
- upsert one site's notification setting.

The EF adapter owns entity tracking and translation from application contracts
to `RVT.Entities`. Persistence remains controlled by the unit-of-work port.

### External ports

- `ISiteArchivePort` creates the archive and returns its stored URL.
- `ISiteLogoPort` saves, deletes, and opens the protected customer logo.
- `IPortalUserDirectory` resolves the assigned users required by notification
  setting responses without exposing ASP.NET Identity.
- `IApplicationUnitOfWork` defines the existing execute/save transaction
  semantics inward; `EfCoreUnitOfWork` remains the adapter.

These are use-case-specific ports. No generic storage, repository, or service
locator is introduced.

## Authorization and Time

`PortalUserContext` moves to the new application boundary as the
transport-neutral authenticated-user facts required by use cases. The existing
ASP.NET Identity factory remains in the host and constructs this record.

The active-assignment policy becomes a pure application rule over user id,
assignment start/end, and an explicit UTC instant. Inclusive semantics remain:

```text
StartDate <= nowUtc
and (EndDate is null or EndDate >= nowUtc)
```

The EF read adapter translates that rule into its provider query. Use cases
obtain the instant from injected `TimeProvider`; they do not call
`DateTime.Now` or `DateTime.UtcNow`.

Unauthorized or expired company-user access continues to be returned as
not-found where the current API deliberately hides resource existence. Admin
management remains admin-only. Notification-setting updates retain the rule
that a company user may update only their own assigned setting.

## Transactions and Failure Semantics

The existing scoped connection and three-context `EfCoreUnitOfWork` remain
unchanged. It implements the new inward-facing unit-of-work interface.

Each database mutation runs in one explicit application transaction:

1. re-check the required authorization and validation data;
2. ask the write port to stage the change;
3. save once through the unit of work;
4. read and return the compatible detail/result model.

Archive export remains outside the database transaction because it streams
data and performs remote blob operations. If export fails, the use case returns
the existing external-service-unavailable result and does not mark the site
archived. After a successful export, the archive flag and metadata are saved
atomically.

Logo storage remains an external operation. Application-owned storage outcomes
are mapped to validation/not-found results; storage exceptions and SDK types do
not cross the port. The controller retains HTTP request-size enforcement and
response file construction.

The unused duplicate Sites MediatR command implementation is removed only
after call-site and behavior tests prove the new facade owns every write.
Other portal MediatR commands and the transaction pipeline are untouched.

## HTTP Compatibility

`SitesController` remains responsible for:

- route and role attributes;
- query normalization and invalid-sort problems;
- API request-to-application mapping;
- application-result-to-HTTP mapping;
- response envelopes and `CreatedAtAction`;
- `IFormFile` and `FileResult` mapping;
- protected customer-logo links.

All current routes remain:

```text
GET    /api/sites
GET    /api/sites/options
GET    /api/sites/{id}
POST   /api/sites
PUT    /api/sites/{id}
POST   /api/sites/{id}/archive
GET    /api/sites/{id}/monitors
GET    /api/sites/{id}/notifications/open
GET    /api/sites/{id}/notification-settings
PUT    /api/sites/{siteId}/notification-settings/{siteUserId}
POST   /api/sites/{id}/customer-logo
DELETE /api/sites/{id}/customer-logo
GET    /api/sites/{id}/customer-logo
```

The React client and generated API contracts require no change.

## Testing Strategy

### Application tests

Create `RvtPortal.Application.Tests` with deterministic in-memory fakes for
ports and `TimeProvider`. Cover:

- admin and active/expired assignment visibility;
- not-found masking;
- site mutation validation and time-pair rules;
- create/update single-transaction behavior;
- archive success, idempotence, and export failure;
- notification-setting ownership;
- logo authorization and storage outcomes;
- cancellation propagation.

These tests must not start ASP.NET Core or EF Core.

### Adapter and HTTP tests

Keep provider-aware query and transaction tests in `RvtPortal.Spa.Tests`.
Existing authenticated Sites integration tests remain the compatibility gate.
Add or retain assertions for route, status, envelope, paging, sort, and
authorization behavior. PostgreSQL-gated tests continue to validate provider
translation where SQLite or in-memory EF cannot.

### Architecture tests

Add guards that:

- require both new projects in `Rvt.Mono.slnx` and the portal solution;
- reject package references from `RvtPortal.Application`;
- reject project references from `RvtPortal.Application` during this slice;
- reject ASP.NET Core, EF Core, `RVT.DataAccess`, `RvtPortal.Spa`, Azure,
  SendGrid, raw configuration, HTTP-client factory, and vendor SDK namespaces;
- reject `IQueryable` and EF entity types in application port signatures;
- require `SitesController` to depend on `ISiteApplicationService` and forbid
  direct EF, MediatR, archive, or logo-storage dependencies;
- require the host to register every application port and adapter.

## Implementation Sequence

1. Add failing solution and dependency-boundary tests, then scaffold the two
   projects and solution entries.
2. Move/create the Sites contracts, `PortalUserContext`, explicit UTC
   assignment policy, result primitives, and port interfaces.
3. Extract read use cases and implement EF read adapters while preserving the
   existing SQL-side filtering, sorting, counting, paging, and projections.
4. Extract create, update, archive, notification-setting, and logo workflows;
   adapt `EfCoreUnitOfWork` and implement the focused write/external ports.
5. Reduce `SitesController` to HTTP mapping, remove its direct storage
   dependency, and remove the superseded host Sites service/commands after
   equivalence tests pass.
6. Run application, adapter, controller, architecture, client, portal, and
   monorepo verification; update the architecture catalog and project state.

Keep these as independently reviewable commits: scaffold, policies/contracts,
reads, writes, controller cutover, and documentation.

## Verification Gate

Before merge:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
dotnet build apps/portal/RvtPortal.Spa.sln --no-restore --nologo
npm run test:run --prefix apps/portal/RvtPortal.Client
npm run build --prefix apps/portal/RvtPortal.Client
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
tests/verify-rvt-common-source-boundary.test.sh
git diff --check
```

Provider-gated PostgreSQL/TimescaleDB tests must run before production rollout;
an unavailable provider remains an explicit evidence gap, never an implicit
pass.

## Risks and Controls

- **Behavior drift:** preserve controller and provider integration tests before
  moving implementations.
- **Query regression:** keep optimized EF projections in focused adapters and
  inspect generated/provider queries for changed hot paths.
- **Transaction regression:** retain the existing unit of work and add
  single-save/rollback tests around every write.
- **Temporary duplicate primitives:** limit the new primitives to the Sites
  boundary and migrate later slices deliberately.
- **Boundary erosion:** make project graph and forbidden namespace checks
  executable from the first scaffold commit.
- **Oversized change:** stop after the complete Sites slice; do not opportunistically
  move Auth, Monitors, Companies, or other application services.


# Task 6 Implementation Report

## Status

Implemented the `/api/sites` controller cutover on
`codex/sites-application-boundary` from base
`7235e3d9ea5dc3ed9c5d3c08ffd4524722410bd7`.

## Scope Delivered

- Repointed `SitesController` to the complete application-owned
  `ISiteApplicationService`.
- Removed direct customer-logo storage and legacy Sites service dependencies
  from the controller.
- Added HTTP mapping for application-owned `UseCaseResult<T>` without changing
  the existing legacy result overload.
- Repointed `SiteApiMapper` to application contracts while retaining the host's
  existing page/sort normalization.
- Kept route, authorization, request-size, response metadata, create
  `Location`, protected file streaming, and masked logo not-found behavior.
- Cut DI over to the application service and removed the duplicate host Sites
  service/commands plus the BusinessLogic Sites application models.
- Added an EF InMemory-only conditional contract-claim path after host
  compatibility tests exposed the provider's lack of `ExecuteUpdateAsync`.
  Relational providers retain the accepted atomic conditional update.

## TDD Evidence

RED:

```text
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~SitesController_DependsOnlyOnSiteUseCasesAndHttpMappers
```

Failed 1/1 because `SitesController` still contained
`RvtPortal.Spa.Application.Sites.ISiteApplicationService` and
`ICustomerLogoStorage`, and did not contain the application-owned interface.

Initial compatibility RED:

```text
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~ContractSiteOperationsTests"
```

Passed 35, skipped one PostgreSQL-gated test, and failed two site-create
workflows. Detailed console logs identified the root cause:
`EfSiteWriteAdapter.TryClaimContractAsync` called relational-only
`ExecuteUpdateAsync` under EF InMemory, producing the safely masked HTTP 500.

GREEN:

- Architecture constructor test: 1 passed.
- Prescribed architecture/contract slice: 37 passed, 1 PostgreSQL-gated skip.
- Relational `SiteWriteAdapterTests`: 4 passed.
- Complete `RvtPortal.Spa.Tests`: 380 passed, 8 provider-gated skips.

## Contract Review

- The complete controller route/authorization/response-attribute sequence
  matches the Task 5 base.
- `PageRequestFactory.Create` and `IsInvalidSort` still operate on the legacy
  normalized request before conversion to the application page contract.
- Fixed-sort monitor/notification panels retain all three request
  normalization methods.
- Every `SiteApiMapper` API DTO assignment is retained.
- Customer logos are streamed with `File(Content, ContentType, FileName)`.
- Create still supplies `Location: /api/sites/{siteId}`.
- Protected logo absence after deletion remains HTTP 404.

## Independent Review Fixes

- Restored the legacy upload failure ordering. The controller creates the
  current-user context and verifies masked site-manage visibility/existence
  before checking whether the multipart `logo` field is absent. A missing site
  with a syntactically valid multipart body and no logo now returns the
  endpoint's `Site not found` ProblemDetails response with HTTP 404.
- Restored the legacy invalid-logo wire contract at this endpoint only.
  `UseCaseResultKind.Validation` from logo storage is returned as plain
  ProblemDetails with title `Invalid customer logo`, the storage validation
  message in `detail`, and HTTP 400. The generic `IApiResultMapper` validation
  behavior remains unchanged for all other endpoints and result kinds.
- Restored the legacy delete-missing-site wire contract at this endpoint only.
  `DeleteCustomerLogo` now handles `UseCaseResultKind.NotFound` with the
  controller's `SiteNotFound(id)` response before delegating every other result
  kind to `IApiResultMapper`. The response remains HTTP 404 with title
  `Site not found` and detail `Site '{id}' was not found.`.
- Strengthened `SiteCustomerLogo_RejectsNonImagePayload` to assert the
  serialized title and detail and to reject the `errors` member that
  distinguishes ValidationProblemDetails. Added a null-logo/missing-site
  ordering regression and a delete/missing-site payload regression.
- RED verification failed both new contracts as intended: the missing site
  returned HTTP 400 before authorization, and the invalid image returned the
  ValidationProblemDetails title `One or more validation errors occurred.`
- Delete/missing-site RED returned HTTP 404 but failed the title assertion with
  generic mapper title `Resource not found.` instead of `Site not found`.
- GREEN verification passes all focused regressions, the prescribed
  architecture/contract slice with 39 passed and one PostgreSQL-gated skip,
  and the complete SPA host suite with 382 passed and eight provider-gated
  skips.
- A metadata-only diff of every `SitesController` action attribute against
  Task 5 base `7235e3d9ea5dc3ed9c5d3c08ffd4524722410bd7` is empty.

## Intentional Brief Variance

`apps/portal/RvtPortal.Spa/Application/Sites/ActiveSiteAssignment.cs` was not
deleted. The prescribed namespace scan identified seven live consumers outside
the Sites slice:

- notification close authorization;
- dashboard visibility;
- monitor list visibility;
- monitor read authorization;
- two monitor-administration queries;
- alert-level authorization.

Repointing or relocating those EF expression consumers is outside Task 6.
Retaining the helper preserves the accepted explicit UTC, inclusive assignment
window behavior. The duplicate service, commands, and BusinessLogic models were
safe to remove and were deleted.

## Known Environment Notes

- The focused suite discovers one PostgreSQL-gated contract test and skips it
  because its connection variable is not configured.
- Existing `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories remain
  outside this task.
- Generated `.codegraph/`, `apps/.nuget-packages/`, and the progress ledger
  were not modified or staged.

# Help Admin Application Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Help Admin as an approved Portal capability while moving the complete Help vertical slice behind a BCL-only application boundary with EF Core adapters.

**Architecture:** `RvtPortal.Application.Help` owns authorization, validation, use cases, models, and read/write ports. `RvtPortal.Spa` owns the ASP.NET controller, API DTO mapping, dependency injection, EF Core adapters, and PostgreSQL entities. The React client continues to expose `/admin/help`, uses the canonical admin API, and keeps immutable asset-row identity and deterministic focus.

**Tech Stack:** .NET 10.0.302, ASP.NET Core controllers, EF Core 10 with PostgreSQL, xUnit, React 19, TypeScript, Vitest/Testing Library, Playwright, Vite.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-28-help-admin-application-boundary-design.md`.
- Read `docs/development/portal/development-guidelines.md` before each implementation task and change a rule and its guard together when introducing a new invariant.
- Keep `RvtPortal.Application` BCL-only: no ASP.NET Core, EF Core, MediatR, `RVT.Entities`, `RVT.DataAccess`, or `RvtPortal.Spa` reference.
- Preserve published `/help` and report-rule guideline behavior.
- Help assets remain URL metadata; do not add upload or object-storage behavior.
- Permit only absolute HTTPS asset URLs or root-relative `/help-assets/` paths.
- Use `TimeProvider.GetUtcNow().UtcDateTime`; do not add `DateTime.Now`, `DateTime.Today`, or `DateTime.UtcNow`.
- EF adapters stage changes and never call `SaveChanges`; the application service owns one unit-of-work save per mutation.
- Preserve both RVT administrator roles and deny Company User and Installer roles from administrative operations.
- Normalize article creation to `POST /api/help/admin/articles`; do not retain `POST /api/help/articles`.
- Use immutable persisted asset IDs and client-only random keys for new asset rows.
- Add every new invariant to an executable guard or focused regression and prove RED before GREEN.
- Do not modify or stage unrelated `project_state.md`, `eng/`, or engineering-standards worktree changes.
- Use `/private/tmp/rvt-dotnet-sdk-10.0.302/dotnet` for local .NET commands when the system SDK cannot resolve the repository pin.

## Baseline Evidence

Before implementation:

```text
Backend focused baseline:
  5 passed, 0 failed
  HelpCmsOperationsTests
  ApplicationBoundaryArchitectureTests
  CqrsArchitectureTests.HelpController*

Frontend focused baseline:
  1 passed, 43 skipped, 0 failed
  "lets RVT admins manage Help FAQ content from the Admin menu"
```

The existing five `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories
are baseline noise and are not part of this feature.

## File Structure

New production files:

```text
apps/portal/RvtPortal.Application/Help/
├── HelpApplicationService.cs
├── HelpAuthorizationPolicy.cs
├── HelpContracts.cs
├── HelpMutationValidator.cs
├── IHelpApplicationService.cs
└── Ports/
    ├── IHelpReadPort.cs
    └── IHelpWritePort.cs

apps/portal/RvtPortal.Spa/Adapters/Help/
├── EfHelpReadAdapter.cs
└── EfHelpWriteAdapter.cs
```

New focused test files:

```text
apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs
apps/portal/RvtPortal.Spa.Tests/HelpAdapterTests.cs
apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.tsx
apps/portal/RvtPortal.Client/tests/e2e/help-admin.spec.ts
apps/portal/docs/release/validate-help-asset-urls.sql
```

Removed files:

```text
apps/portal/RvtPortal.Spa/Application/Help/HelpApplicationService.cs
apps/portal/RvtPortal.Spa/Application/Help/HelpArticleCommands.cs
```

Modified integration files:

```text
apps/portal/RvtPortal.Spa/Api/HelpApiContracts.cs
apps/portal/RvtPortal.Spa/Api/HelpController.cs
apps/portal/RvtPortal.Spa/Api/Mappers/HelpApiMapper.cs
apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs
apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs
apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs
apps/portal/RvtPortal.Spa.Tests/DataAccessWriteBoundaryTests.cs
apps/portal/RvtPortal.Spa.Tests/HelpCmsOperationsTests.cs
apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx
apps/portal/RvtPortal.Client/src/api/client.ts
apps/portal/RvtPortal.Client/src/api/types.ts
apps/portal/RvtPortal.Client/src/App.test.tsx
docs/architecture/portal/hexagonal-edges-change-log.md
docs/architecture/portal/ports-and-adapters-catalog.md
docs/development/portal/development-guidelines.md
docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md
docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
project_state.md
```

---

### Task 1: Define Help application contracts, policy, and validation

**Files:**
- Create: `apps/portal/RvtPortal.Application/Help/HelpContracts.cs`
- Create: `apps/portal/RvtPortal.Application/Help/HelpAuthorizationPolicy.cs`
- Create: `apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs`

**Interfaces:**
- Produces: `HelpAdminQuery`, `HelpArticleMutation`, `HelpAssetMutation`,
  `ValidatedHelpArticleMutation`, `HelpMutationValidationData`,
  `HelpOverviewModel`, `HelpAdminOverviewModel`, `HelpSectionModel`,
  `HelpArticleSummaryModel`, `HelpArticleModel`, `HelpAssetModel`,
  `HelpDeleteResult`, `HelpMutationValidationResult`,
  `HelpAuthorizationPolicy`, and `HelpMutationValidator`.
- Consumes: `PortalUserContext` and `UseCaseError`.

- [ ] **Step 1: Write authorization and validator tests that reference the new application types**

Add focused facts to `HelpApplicationServiceTests.cs`:

```csharp
[Fact]
public void AuthorizationPolicy_PreservesPublishedAndAdminRoleContracts()
{
    var admin = Actor(isAdmin: true);
    var companyUser = Actor(isCompanyUser: true);
    var installer = Actor(isInstaller: true);

    Assert.True(HelpAuthorizationPolicy.CanReadPublished(admin));
    Assert.True(HelpAuthorizationPolicy.CanReadPublished(companyUser));
    Assert.False(HelpAuthorizationPolicy.CanReadPublished(installer));
    Assert.True(HelpAuthorizationPolicy.CanManage(admin));
    Assert.False(HelpAuthorizationPolicy.CanManage(companyUser));
}

[Theory]
[InlineData("https://docs.rvt.test/guide.pdf", true)]
[InlineData("/help-assets/guides/guide.pdf", true)]
[InlineData("http://docs.rvt.test/guide.pdf", false)]
[InlineData("//docs.rvt.test/guide.pdf", false)]
[InlineData("javascript:alert(1)", false)]
[InlineData("data:text/html,test", false)]
[InlineData("/other/path.pdf", false)]
[InlineData("/help-assets\\guide.pdf", false)]
public void MutationValidator_EnforcesSafeAssetUrls(string url, bool valid)
{
    var result = HelpMutationValidator.ValidateShape(
        ValidMutation() with
        {
            Assets =
            [
                new HelpAssetMutation(null, "Guide", "Document", url, 0)
            ]
        });

    Assert.Equal(valid, result.IsValid);
}

[Fact]
public void MutationValidator_CanonicalizesValuesAndPreservesAssetIds()
{
    var assetId = Guid.NewGuid();
    var result = HelpMutationValidator.ValidateShape(
        ValidMutation() with
        {
            ContentType = "faq",
            Assets =
            [
                new HelpAssetMutation(
                    assetId,
                    " Guide ",
                    "document",
                    "https://docs.rvt.test/guide.pdf",
                    2)
            ]
        });

    Assert.True(result.IsValid);
    Assert.Equal("FAQ", result.Value!.Source.ContentType);
    Assert.Equal(assetId, result.Value.Source.Assets.Single().Id);
    Assert.Equal("Document", result.Value.Source.Assets.Single().AssetType);
    Assert.Equal("Guide", result.Value.Source.Assets.Single().Title);
}
```

Add table-driven cases for every required field, maximum length, slug regex,
negative order, unsupported content type, unsupported asset type, URL user-info,
control characters, duplicate slug data, and foreign asset IDs.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~HelpApplicationServiceTests \
  --no-restore -v minimal
```

Expected: compilation fails because `RvtPortal.Application.Help` types do not
exist.

- [ ] **Step 3: Add transport-neutral contracts**

Implement immutable records in `HelpContracts.cs` with these signatures:

```csharp
public sealed record HelpAdminQuery(
    string? SearchText,
    string? Status,
    string? ContentType);

public sealed record HelpAssetMutation(
    Guid? Id,
    string Title,
    string AssetType,
    string Url,
    int SortOrder);

public sealed record HelpArticleMutation(
    string SectionTitle,
    string SectionSlug,
    string Title,
    string Slug,
    string? Summary,
    string Body,
    string ContentType,
    bool IsPublished,
    int SectionSortOrder,
    int SortOrder,
    IReadOnlyList<HelpAssetMutation> Assets);

public sealed record HelpMutationValidationData(
    bool ArticleExists,
    bool SlugBelongsToAnotherArticle,
    IReadOnlySet<Guid> ExistingAssetIds);

public sealed record HelpDeleteResult(Guid ArticleId);
```

Move all Help response models from the old host service into this file as
application-owned models. Keep `CreatedAtUtc`, `UpdatedAtUtc`, section ordering,
and asset IDs.

- [ ] **Step 4: Implement the pure authorization policy**

Add:

```csharp
public static class HelpAuthorizationPolicy
{
    public static bool CanReadPublished(PortalUserContext actor) =>
        actor.IsAdmin || actor.IsCompanyUser;

    public static bool CanManage(PortalUserContext actor) =>
        actor.IsAdmin;
}
```

- [ ] **Step 5: Implement shape and business validation**

Implement:

```csharp
public sealed record ValidatedHelpArticleMutation(HelpArticleMutation Source);

public sealed record HelpMutationValidationResult(
    IReadOnlyList<UseCaseError> Errors,
    ValidatedHelpArticleMutation? Value)
{
    public bool IsValid => Errors.Count == 0 && Value is not null;
}

public static HelpMutationValidationResult ValidateShape(
    HelpArticleMutation mutation);

public static HelpMutationValidationResult ValidateBusinessRules(
    HelpMutationValidationResult shape,
    HelpMutationValidationData data,
    bool requireExistingArticle);
```

Use the exact limits and canonical values from the design. URL validation must
use `Uri.TryCreate`, require HTTPS for absolute URLs, accept only paths beginning
`/help-assets/`, and explicitly reject `//`, backslashes, user-info, and control
characters.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the same focused command. Expected: all policy and validator tests pass.

- [ ] **Step 7: Run the standalone application boundary guard**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~ApplicationBoundaryArchitectureTests \
  --no-restore -v minimal
```

Expected: 2/2 pass and no forbidden framework import is introduced.

- [ ] **Step 8: Commit Task 1**

```bash
git add apps/portal/RvtPortal.Application/Help \
  apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs
git commit -m "feat: define Help application contracts"
```

---

### Task 2: Add Help ports and transactional application service

**Files:**
- Create: `apps/portal/RvtPortal.Application/Help/Ports/IHelpReadPort.cs`
- Create: `apps/portal/RvtPortal.Application/Help/Ports/IHelpWritePort.cs`
- Create: `apps/portal/RvtPortal.Application/Help/IHelpApplicationService.cs`
- Create: `apps/portal/RvtPortal.Application/Help/HelpApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 Help contracts, `IApplicationUnitOfWork`, `PortalUserContext`,
  and `TimeProvider`.
- Produces: application-owned `IHelpReadPort`, `IHelpWritePort`,
  `IHelpApplicationService`, and `HelpApplicationService`.

- [ ] **Step 1: Write failing service tests with in-memory fake ports**

Add tests proving:

```csharp
[Fact]
public async Task CreateAsync_UsesOneTransactionOneSaveAndInjectedUtc()
{
    var clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 7, 28, 9, 30, 0, TimeSpan.Zero));
    var writes = new RecordingHelpWritePort();
    var unitOfWork = new RecordingUnitOfWork();
    var service = CreateService(writes: writes, unitOfWork: unitOfWork, clock: clock);

    var result = await service.CreateAsync(
        Actor(isAdmin: true),
        ValidMutation(),
        TestContext.Current.CancellationToken);

    Assert.Equal(UseCaseResultKind.Success, result.Kind);
    Assert.Equal(1, unitOfWork.TransactionCount);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.Equal(
        new DateTime(2026, 7, 28, 9, 30, 0, DateTimeKind.Utc),
        writes.CreateTimestampUtc);
}
```

Cover:

- published reads allowed for admin/company user and forbidden for installer;
- every admin operation forbidden for company user and installer without a port
  call;
- create/update validation before staging;
- update not found;
- foreign asset ID validation;
- set-publication not found;
- delete not found;
- every successful mutation uses one transaction and one save;
- post-write detail is re-read before returning;
- cancellation reaches every port and the unit of work.

- [ ] **Step 2: Run the service slice and verify RED**

Expected: compilation fails because the service and ports do not exist.

- [ ] **Step 3: Define the read port**

Implement:

```csharp
public interface IHelpReadPort
{
    Task<HelpOverviewModel> QueryPublishedAsync(
        string? searchText,
        CancellationToken cancellationToken);

    Task<HelpArticleModel?> GetPublishedArticleAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<HelpAdminOverviewModel> QueryAdminAsync(
        HelpAdminQuery query,
        CancellationToken cancellationToken);

    Task<HelpArticleModel?> GetAdminArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken);

    Task<HelpMutationValidationData> GetMutationValidationDataAsync(
        string slug,
        Guid? articleId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Define the write port**

Implement:

```csharp
public interface IHelpWritePort
{
    Task<Guid> CreateAsync(
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        Guid articleId,
        ValidatedHelpArticleMutation mutation,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> SetPublicationAsync(
        Guid articleId,
        bool isPublished,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid articleId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement the application interface and service**

Use the exact service signatures from the design. Enforce policy before port
access. For mutations:

```csharp
return await unitOfWork.ExecuteInTransactionAsync(
    async token =>
    {
        var data = await reads.GetMutationValidationDataAsync(
            shape.Value!.Source.Slug,
            articleId,
            token);
        var validation = HelpMutationValidator.ValidateBusinessRules(
            shape,
            data,
            requireExistingArticle: articleId.HasValue);
        if (!validation.IsValid)
        {
            return UseCaseResult<HelpArticleModel>.Validation(
                [.. validation.Errors]);
        }

        // Stage through writes, save once, then re-read through reads.
    },
    cancellationToken);
```

Do not depend on HTTP DTOs, EF, MediatR, or entities.

- [ ] **Step 6: Run service tests and verify GREEN**

Expected: all Help application tests pass.

- [ ] **Step 7: Mutation-test transaction ownership**

Temporarily remove the service `SaveChangesAsync` call and run the create/update
transaction tests. Expected: tests fail on `SaveCount`. Restore the call and
rerun GREEN.

- [ ] **Step 8: Run application boundary tests and commit**

Expected: boundary tests pass.

```bash
git add apps/portal/RvtPortal.Application/Help \
  apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs
git commit -m "feat: add transactional Help use cases"
```

---

### Task 3: Implement the EF Help read adapter

**Files:**
- Create: `apps/portal/RvtPortal.Spa/Adapters/Help/EfHelpReadAdapter.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/HelpAdapterTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`

**Interfaces:**
- Consumes: `IHelpReadPort`, Help application models, and `RVTDbContext`.
- Produces: server-side filtered, deterministically ordered Help projections.

- [ ] **Step 1: Write failing read-adapter tests**

Seed sections, published/draft articles, mixed content types, assets, and
duplicate search terms. Assert:

- published queries exclude draft articles and unpublished sections;
- search is case-insensitive across title, summary, body, and content type;
- admin filters support only canonical `All`, `Published`, and `Draft`;
- content-type filtering is canonical;
- section/article/asset ordering is deterministic;
- published detail excludes hidden articles;
- admin detail includes drafts;
- mutation validation reports article existence, slug ownership, and exact
  existing asset IDs.

Add an architecture assertion:

```csharp
Assert.Equal(
    "RvtPortal.Spa.Adapters.Help",
    typeof(EfHelpReadAdapter).Namespace);
```

- [ ] **Step 2: Run the adapter slice and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~HelpAdapterTests|FullyQualifiedName~ApplicationBoundaryArchitectureTests" \
  --no-restore -v minimal
```

Expected: compilation fails because `EfHelpReadAdapter` does not exist.

- [ ] **Step 3: Implement direct EF projection helpers**

Use `AsNoTracking`, SQL-translatable predicates, and projection to application
models. Do not return `IQueryable` from the adapter. Apply all ordering before
`ToListAsync`.

Use a shared private projection expression for article detail so query and
detail shapes cannot drift.

- [ ] **Step 4: Run read-adapter tests and verify GREEN**

Expected: all read-adapter and architecture tests pass.

- [ ] **Step 5: Prove filters are not in-memory**

Add a source guard asserting `EfHelpReadAdapter` does not call `ToListAsync`
before its search/status/content predicates. Mutate the adapter by moving a
filter after materialization, verify the guard fails, restore, and rerun GREEN.

- [ ] **Step 6: Commit Task 3**

```bash
git add apps/portal/RvtPortal.Spa/Adapters/Help/EfHelpReadAdapter.cs \
  apps/portal/RvtPortal.Spa.Tests/HelpAdapterTests.cs \
  apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs \
  apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs
git commit -m "feat: add Help read adapter"
```

---

### Task 4: Implement the EF Help write adapter and immutable asset reconciliation

**Files:**
- Create: `apps/portal/RvtPortal.Spa/Adapters/Help/EfHelpWriteAdapter.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/HelpAdapterTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/DataAccessWriteBoundaryTests.cs`

**Interfaces:**
- Consumes: `IHelpWritePort`, validated application mutations, UTC timestamps,
  and `RVTDbContext`.
- Produces: staged entity changes with stable asset IDs and no commit calls.

- [ ] **Step 1: Write failing write-adapter tests**

Cover:

- create reuses an existing section by slug;
- create adds a canonical section when absent;
- update moves an article to a different section;
- persisted asset IDs remain unchanged after title/URL edits;
- new asset mutations receive server-generated IDs;
- omitted assets are removed;
- foreign asset IDs cause no staged update;
- root-relative URL derives matching `InternalPath`;
- HTTPS URL stores `InternalPath = null`;
- publication and deletion return false for missing articles;
- supplied UTC timestamps populate article timestamps; and
- adapter source contains no `SaveChanges` or `SaveChangesAsync`.

- [ ] **Step 2: Run the adapter slice and verify RED**

Expected: compilation fails because `EfHelpWriteAdapter` does not exist.

- [ ] **Step 3: Implement section and article staging**

Use tracked EF entities. Create new entity IDs explicitly with `Guid.NewGuid()`
for articles/assets when the entity base does not already guarantee non-empty
IDs before save.

Derive:

```csharp
private static string? InternalPath(string url) =>
    url.StartsWith("/help-assets/", StringComparison.Ordinal)
        ? url
        : null;
```

- [ ] **Step 4: Implement immutable asset reconciliation**

Build the existing assets dictionary by ID. For each mutation:

- `Id == null`: add a new `HelpAsset`;
- known ID: update the tracked entity in place;
- unknown ID: return `false` without staging a partial change.

After processing, remove tracked assets whose IDs were omitted.

- [ ] **Step 5: Run write-adapter tests and verify GREEN**

Expected: all adapter tests pass.

- [ ] **Step 6: Mutation-test the no-save guard**

Temporarily add `await domainContext.SaveChangesAsync(cancellationToken)` to one
adapter method. Run the boundary test and expect failure. Restore and rerun
GREEN.

- [ ] **Step 7: Commit Task 4**

```bash
git add apps/portal/RvtPortal.Spa/Adapters/Help/EfHelpWriteAdapter.cs \
  apps/portal/RvtPortal.Spa.Tests/HelpAdapterTests.cs \
  apps/portal/RvtPortal.Spa.Tests/DataAccessWriteBoundaryTests.cs
git commit -m "feat: add Help write adapter"
```

---

### Task 5: Rewire the HTTP adapter and remove the host-side Help application

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Api/HelpApiContracts.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/HelpController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/Mappers/HelpApiMapper.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Delete: `apps/portal/RvtPortal.Spa/Application/Help/HelpApplicationService.cs`
- Delete: `apps/portal/RvtPortal.Spa/Application/Help/HelpArticleCommands.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/HelpCmsOperationsTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs`

**Interfaces:**
- Consumes: `IHelpApplicationService`, `ICurrentUserContextFactory`,
  `IApiResultMapper`, API contracts, and application/API mappers.
- Produces: canonical HTTP routes and complete DI wiring.

- [ ] **Step 1: Change API integration tests first**

Update create calls to:

```csharp
var create = await adminClient.PostAsJsonAsync(
    "/api/help/admin/articles",
    articleRequest);
```

Add tests that:

- both RVT administrator roles can use every admin endpoint;
- Company User and Installer receive `403` from every admin endpoint;
- `POST /api/help/articles` returns `404` and does not create content;
- unsafe URLs return `400` Validation Problem Details;
- foreign asset IDs return `400`;
- persisted asset IDs survive update;
- cancellation and not-found results preserve API semantics.

- [ ] **Step 2: Run Help API tests and verify RED**

Expected: canonical create route returns `404` or `405`, and the old route still
creates an article.

- [ ] **Step 3: Add bidirectional API mapping**

Add nullable `Id` to `HelpAssetMutationRequest` and remove browser-controlled
`InternalPath`. Implement:

```csharp
public static HelpAdminQuery ToAdminQuery(
    string? searchText,
    string? status,
    string? contentType);

public static HelpArticleMutation ToMutation(
    HelpArticleMutationRequest request);
```

Keep response mapping unchanged at the HTTP contract.

- [ ] **Step 4: Make `HelpController` a pure HTTP adapter**

Inject:

```csharp
IHelpApplicationService help,
ICurrentUserContextFactory currentUserContextFactory,
IApiResultMapper resultMapper
```

Every action creates `PortalUserContext`, calls the application service, and
maps `UseCaseResult`. Change creation to:

```csharp
[HttpPost("admin/articles")]
[Authorize(Roles = RoleAuthorization.AdminRoles)]
```

Return `CreatedAtAction(nameof(GetAdminArticle), new { id = item.Id }, response)`.

- [ ] **Step 5: Rewire dependency injection**

Replace the old registration with:

```csharp
services.AddScoped<
    RvtPortal.Application.Help.IHelpApplicationService,
    RvtPortal.Application.Help.HelpApplicationService>();
services.AddScoped<IHelpReadPort, EfHelpReadAdapter>();
services.AddScoped<IHelpWritePort, EfHelpWriteAdapter>();
```

Add the exact Help application/adapter imports and update the file summary.

- [ ] **Step 6: Delete the old host-side service and command handlers**

Delete both files only after DI compiles against the new application service.
Update architecture tests to resolve:

```csharp
typeof(RvtPortal.Application.Help.IHelpApplicationService)
```

Add a source guard asserting no `.cs` file remains under
`RvtPortal.Spa/Application/Help`.

- [ ] **Step 7: Run API, architecture, and complete backend Help tests**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~Help|FullyQualifiedName~ApplicationBoundaryArchitectureTests|FullyQualifiedName~DataAccessWriteBoundaryTests" \
  --no-restore -v minimal
```

Expected: all selected tests pass.

- [ ] **Step 8: Commit Task 5**

```bash
git add apps/portal/RvtPortal.Spa \
  apps/portal/RvtPortal.Spa.Tests
git commit -m "refactor: route Help through application ports"
```

---

### Task 6: Stabilize Help Admin client identity, focus, and API routes

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/api/types.ts`
- Modify: `apps/portal/RvtPortal.Client/src/api/client.ts`
- Modify: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx`
- Create: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.tsx`
- Modify: `apps/portal/RvtPortal.Client/src/App.test.tsx`

**Interfaces:**
- Consumes: canonical Help admin API and response asset IDs.
- Produces: stable `HelpAssetFormRow`, canonical create request, deterministic
  focus, and accessible validation behavior.

- [ ] **Step 1: Write failing focused client tests**

Keep the new focused fetch fixtures private to `HelpAdminPanel.test.tsx`; do not
move existing `App.test.tsx` fixtures or create a shared test-support module.
Add tests:

```typescript
it('keeps an asset row mounted while its editable title changes', async () => {
  renderHelpAdmin();
  const title = await screen.findByDisplayValue('Dust monitoring guide');
  title.focus();
  fireEvent.change(title, { target: { value: 'Updated guide' } });

  expect(screen.getByDisplayValue('Updated guide')).toHaveFocus();
});

it('focuses the saved article edit action after create', async () => {
  renderHelpAdmin();
  await createArticle('New FAQ');

  expect(
    await screen.findByRole('button', { name: 'Edit New FAQ' })
  ).toHaveFocus();
});
```

Also cover update, publish, delete next/previous/empty-list focus, add-asset
focus, remove-asset next/previous/Add Asset focus, asset ID submission, and
canonical create URL.

- [ ] **Step 2: Run panel/App tests and verify RED**

Run:

```bash
npm test -- --run src/admin/HelpAdminPanel.test.tsx src/App.test.tsx
```

Expected: stable-key/focus and canonical-route assertions fail.

- [ ] **Step 3: Update API types and client**

Add:

```typescript
export type HelpAssetMutationRequest = {
  id?: string | null;
  title: string;
  assetType: string;
  url: string;
  sortOrder: number;
};
```

Remove mutation `internalPath`. Change `createHelpArticle` to
`/api/help/admin/articles`.

- [ ] **Step 4: Add stable form-row identity**

Implement:

```typescript
type HelpAssetFormRow = HelpAssetMutationRequest & {
  clientKey: string;
};
```

Use persisted `asset.id` or `crypto.randomUUID()`. Render with:

```tsx
<div className="asset-row" key={asset.clientKey}>
```

Strip `clientKey` before API submission and keep persisted `id`.

- [ ] **Step 5: Implement ref-based focus restoration**

Use maps keyed by article ID and asset `clientKey`, not titles. Schedule focus
after React commits with an effect driven by a discriminated pending-focus
state. Add `data-*` attributes only where they aid stable test selection and do
not replace accessible roles/names.

- [ ] **Step 6: Run focused client tests and verify GREEN**

Expected: all Help panel/App tests pass.

- [ ] **Step 7: Mutation-test stable keys**

Temporarily restore `key={`${asset.title}-${index}`}`. Run the title-edit focus
test and expect failure. Restore `asset.clientKey` and rerun GREEN.

- [ ] **Step 8: Run client lint and build**

```bash
npm run lint
npm run build
```

Expected: both pass. The production bundle must contain `/admin/help` and the
canonical `/api/help/admin/articles` route, and must not contain the old create
route as a mutation literal.

- [ ] **Step 9: Commit Task 6**

```bash
git add apps/portal/RvtPortal.Client/src
git commit -m "feat: complete Help Admin client workflow"
```

---

### Task 7: Add browser, persisted-data readiness, and release evidence

**Files:**
- Create: `apps/portal/RvtPortal.Client/tests/e2e/help-admin.spec.ts`
- Create: `apps/portal/docs/release/validate-help-asset-urls.sql`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Modify: `docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md`
- Modify: `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
- Modify: `docs/architecture/portal/hexagonal-edges-change-log.md`
- Modify: `docs/architecture/portal/ports-and-adapters-catalog.md`
- Modify: `docs/development/portal/development-guidelines.md`

**Interfaces:**
- Consumes: completed server/client feature.
- Produces: executable browser evidence, persisted-data cutover query, guarded
  release decision, and architecture documentation.

- [ ] **Step 1: Write the failing Playwright journey**

Mock authenticated admin and Help API responses. Cover:

1. open Help/FAQ from Admin navigation;
2. create a draft with one HTTPS asset;
3. publish it;
4. preview the published article;
5. edit the article and asset title;
6. delete it; and
7. verify Company User cannot see or directly render Help Admin.

Run:

```bash
npm run test:e2e -- --grep "Help Admin"
```

Expected: fails until route fixtures and canonical mutations are fully wired.

- [ ] **Step 2: Add the persisted asset URL readiness query**

Create a read-only PostgreSQL query that returns incompatible rows:

```sql
SELECT id, help_article_id, url
FROM public.help_asset
WHERE
    url IS NULL
    OR length(url) > 512
    OR url ~ '[[:cntrl:]\\]'
    OR (
        url NOT LIKE '/help-assets/%'
        AND url !~ '^https://[^/@[:space:]]+(?:/|$)'
    )
ORDER BY help_article_id, id;
```

Include comments stating that release requires zero rows and that the script
does not mutate data.

- [ ] **Step 3: Guard the readiness script**

Add a `CutoverReadinessTests` fact that proves the script:

- exists;
- selects from canonical `public.help_asset`;
- is read-only;
- checks HTTPS and `/help-assets/`;
- rejects control characters/backslashes; and
- orders output deterministically.

Mutation-test by removing the HTTPS check, expect RED, restore, and rerun GREEN.

- [ ] **Step 4: Complete the browser journey and verify GREEN**

Update only fixtures/selectors needed by the approved flow. Run the focused
Playwright test until green, then run the full e2e suite.

- [ ] **Step 5: Update release and architecture decisions**

Change Help Admin from `EXCLUDED` to `READY` only after all evidence exists.
Mark architecture review R2 resolved by the application-boundary extraction and
approved shipment. Document:

- application-owned ports;
- EF adapters;
- admin/public authorization;
- canonical routes;
- URL metadata policy;
- rollback by disabling admin route/endpoints while preserving published Help;
- the readiness SQL zero-row requirement; and
- the stable asset-key/focus regression.

Add the new safe-URL and immutable-row invariants to development guidelines and
name their guard tests.

- [ ] **Step 6: Run release/export guards**

Run the repository's Portal release-export dry run and focused readiness tests.
Confirm required client source and release evidence remain included and internal
`docs/superpowers` files remain excluded.

- [ ] **Step 7: Commit Task 7**

```bash
git add apps/portal/RvtPortal.Client/tests/e2e/help-admin.spec.ts \
  apps/portal/docs/release/validate-help-asset-urls.sql \
  apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs \
  docs/architecture \
  docs/development/portal/development-guidelines.md \
  docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
git commit -m "docs: approve Help Admin release"
```

---

### Task 8: Complete verification, state capture, and independent review

**Files:**
- Modify: `project_state.md`

**Interfaces:**
- Consumes: all completed tasks.
- Produces: reproducible verification evidence and merge-ready branch state.

- [ ] **Step 1: Run all backend tests**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -v minimal
```

Expected: all tests pass, with only previously recorded external baseline
warnings.

- [ ] **Step 2: Run backend Release build**

```bash
dotnet build apps/portal/RvtPortal.Spa.sln \
  -c Release --no-restore -v minimal
```

Expected: build passes. Record warning counts and distinguish pre-existing
advisories from new warnings.

- [ ] **Step 3: Run all client gates**

```bash
cd apps/portal/RvtPortal.Client
npm test -- --run
npm run lint
npm run build
npm run test:e2e
```

Expected: all commands pass.

- [ ] **Step 4: Run repository architecture and engineering guards**

Run these exact root guards:

```bash
bash tests/verify-mono-layout.test.sh
bash tests/verify-mono-solution.test.sh
bash tests/verify-postgresql-only.test.sh
bash tests/verify-rvt-common-source-boundary.test.sh
bash tests/verify-rvt-common-source-boundary-regression.test.sh
bash tests/verify-documentation-layout.test.sh
bash tests/verify-documentation-layout-regression.test.sh
bash tests/verify-engineering-configuration.test.sh
bash tests/verify-engineering-standards.test.sh
```

Run the engineering-standards verifier for the committed Help range using the
documented `--base 96fa359 --head HEAD` mode after Task 7 is committed.

- [ ] **Step 5: Inspect the production bundle**

Verify the built bundle contains:

```text
/admin/help
/api/help/admin/articles
Help/FAQ Management
```

Verify it does not retain a client create call to:

```text
POST /api/help/articles
```

Do not require the public GET `/api/help/articles/{slug}` string to disappear.

- [ ] **Step 6: Review repository hygiene**

Run:

```bash
git diff --check
git status --short
git log --oneline --decorate -10
```

Confirm generated `dist`, `bin`, `obj`, test results, local SDK files, and
unrelated engineering-standard artifacts are not staged.

- [ ] **Step 7: Update project state**

Append:

- branch and commit list;
- created/removed/modified file structure;
- application and adapter interface signatures;
- validation constants and URL policy;
- verification counts and commands;
- release decision and rollback;
- known pre-existing warnings; and
- exact resume instruction.

The final line remains:

```text
Next-session instruction: Read project_state.md to get up to speed
```

- [ ] **Step 8: Commit state and final evidence**

Stage only the Help integration state/evidence:

```bash
git add project_state.md
git commit -m "docs: record Help Admin integration"
```

- [ ] **Step 9: Perform an independent whole-branch review**

Review `96fa359..HEAD` for:

- application-boundary violations;
- authorization gaps;
- unsafe URL bypasses;
- partial transaction behavior;
- asset identity loss;
- stale or title-based focus;
- API compatibility mistakes;
- public Help/report regression;
- inadequate release evidence; and
- unrelated changes.

Repair only validated Help-scoped findings, rerun the affected focused tests,
then rerun all final gates.

- [ ] **Step 10: Prepare integration handoff**

Use `superpowers:verification-before-completion` before claiming completion and
`superpowers:finishing-a-development-branch` for the merge/push decision. Do not
merge or push until explicitly authorized by the user.

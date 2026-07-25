# Site Write Concurrency Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serialize site archive claims and notification-setting writes while making archive cleanup retryable and safe under unknown transaction outcomes.

**Architecture:** Relational uniqueness and provider-native upserts own database serialization. New site archives use one deterministic blob key per site, while the relational archive row remains canonical; retries derive and reconcile the stable candidate from the site id and canonical URL. One-time duplicate-row cleanup stays in the EF migration, while rerunnable SchemaDeploy SQL only detects duplicates and repairs indexes on clean data.

**Tech Stack:** .NET 10, C# 14, EF Core 10.0.7, Npgsql, SQL Server, SQLite, Azure.Storage.Blobs 12.26.0, xUnit.

## Global Constraints

- Preserve the public Sites HTTP routes, response envelopes, authorization behavior, and one-archive-per-site domain contract.
- Keep `RvtPortal.Application` BCL-only.
- Temporary archive workspaces remain unique; the new blob key is exactly `<site-id-N>/site-archive.zip`.
- Unknown-commit verification uses `CancellationToken.None` and no code path may delete the canonical URL.
- `RVT.SchemaDeploy` performs no table-data deletion and remains safe to rerun.
- Deterministic relational duplicate cleanup runs only in migration `20260723234806_EnforceSiteWriteUniqueness`.
- PostgreSQL/Npgsql is the canonical checked-in migration/snapshot provider; SQL Server runtime DML is structurally covered, but SQL Server migration deployment is separate work.
- Do not touch `.codegraph/`, `apps/.nuget-packages/`, `.superpowers/sdd/progress.md`, or historical task reports.
- Preserve the requested single final commit: `fix: serialize site archive and notification writes`.

---

### Task 1: Lock the archive lifecycle behavior with RED application tests

**Files:**
- Modify: `apps/portal/RvtPortal.Application.Tests/Sites/SiteExternalWorkflowTests.cs`

**Interfaces:**
- Consumes: current `SiteApplicationService.ArchiveAsync`, `SiteArchiveState`, `ISiteArchivePort`, and test doubles.
- Produces: regression coverage for unknown commit verification and archived-retry cleanup rediscovery.

- [x] **Step 1: Add an unknown-commit RED test**

Add a unit-of-work mode that executes the operation and then throws. Queue an
initial active state followed by durable archived state for the same export URL:

```csharp
[Fact]
public async Task ArchiveAsync_UnknownCommitWithDurableSameUrlReturnsSuccessWithoutCleanup()
{
    var fixture = SiteExternalFixture.ReadableAdmin();
    fixture.UnitOfWork.TransactionExceptionAfterOperation =
        new IOException("connection dropped during commit");
    fixture.Reads.ArchiveStates.Enqueue(
        new SiteArchiveState(
            fixture.SiteId,
            true,
            "https://archive.example/site.zip"));

    var result = await fixture.Service.ArchiveAsync(
        fixture.Admin,
        fixture.SiteId,
        "admin",
        CancellationToken.None);

    Assert.Equal(UseCaseResultKind.Success, result.Kind);
    Assert.Equal(2, fixture.Reads.ArchiveStateReadCount);
    Assert.Equal(0, fixture.Archive.DeleteCount);
    Assert.False(fixture.Reads.LastArchiveStateToken.CanBeCanceled);
}
```

The test double must throw `TransactionExceptionAfterOperation` only after it
awaits `operation`, and `ExternalReadPort.GetArchiveStateAsync` must dequeue
states while recording the supplied token.

- [x] **Step 2: Add a failed-cleanup retry RED test**

```csharp
[Fact]
public async Task ArchiveAsync_FailedLoserCleanupIsRediscoveredAfterSiteIsArchived()
{
    var fixture = SiteExternalFixture.ReadableAdmin();
    fixture.Writes.ArchiveClaimResult =
        new SiteArchiveClaimResult(false, "https://archive.example/legacy.zip");
    fixture.Archive.CleanupResults.Enqueue(
        SiteArchiveCleanupResult.Failed("cleanup failed"));
    fixture.Archive.CleanupResults.Enqueue(
        SiteArchiveCleanupResult.Success());

    var first = await fixture.Service.ArchiveAsync(
        fixture.Admin, fixture.SiteId, "admin", CancellationToken.None);
    fixture.Reads.ArchiveState =
        new SiteArchiveState(
            fixture.SiteId,
            true,
            "https://archive.example/legacy.zip");
    var retry = await fixture.Service.ArchiveAsync(
        fixture.Admin, fixture.SiteId, "admin", CancellationToken.None);

    Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, first.Kind);
    Assert.Equal(UseCaseResultKind.Success, retry.Kind);
    Assert.Equal(1, fixture.Archive.ExportCount);
    Assert.Equal(2, fixture.Archive.DeleteCount);
}
```

- [x] **Step 3: Run the two tests and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~UnknownCommitWithDurableSameUrl|FullyQualifiedName~FailedLoserCleanupIsRediscovered'
```

Expected: both fail because current exception handling deletes/rethrows and the
already-archived branch never reconciles cleanup.

---

### Task 2: Lock the deterministic storage key and non-destructive deployment with RED guards

**Files:**
- Create: `apps/portal/RvtPortal.Spa.Tests/SiteArchiveWorkspaceFactoryTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SchemaDeployTests.cs`

**Interfaces:**
- Consumes: internal `SiteArchiveWorkspaceFactory.Create(Guid)` and PostgreSQL post-load source.
- Produces: exact stable-key compatibility contract and SchemaDeploy safety guard.

- [x] **Step 1: Add the workspace factory RED test**

```csharp
public sealed class SiteArchiveWorkspaceFactoryTests
{
    [Fact]
    public async Task Create_UsesUniqueWorkspacesAndOneStableBlobKeyPerSite()
    {
        var siteId = Guid.NewGuid();
        var factory = new SiteArchiveWorkspaceFactory();
        await using var first = factory.Create(siteId);
        await using var second = factory.Create(siteId);

        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.Equal($"{siteId:N}/site-archive.zip", first.BlobName);
        Assert.Equal(first.BlobName, second.BlobName);
    }
}
```

- [x] **Step 2: Replace the destructive script assertion with a RED safety guard**

`SiteWriteUniquenessScript_DetectsDuplicatesBeforeNonDestructiveIndexRepair`
must assert:

```csharp
Assert.DoesNotContain("DELETE FROM", source, StringComparison.OrdinalIgnoreCase);
Assert.Contains("HAVING COUNT(*) > 1", source, StringComparison.Ordinal);
Assert.Contains("RAISE EXCEPTION", source, StringComparison.Ordinal);
Assert.Contains(
    "20260723234806_EnforceSiteWriteUniqueness",
    source,
    StringComparison.Ordinal);
Assert.True(duplicateGuard >= 0 && duplicateGuard < archiveIndexRepair);
Assert.True(duplicateGuard >= 0 && duplicateGuard < notificationIndexRepair);
```

- [x] **Step 3: Run the guards and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SiteArchiveWorkspaceFactoryTests|FullyQualifiedName~SiteWriteUniquenessScript'
```

Expected: stable-key test fails on two unique blob names; script guard fails
because the current post-load SQL deletes duplicate rows.

---

### Task 3: Implement deterministic archive reconciliation and commit verification

**Files:**
- Modify: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteReadPort.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteArchivePort.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/SiteApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Application.Tests/Sites/SiteExternalWorkflowTests.cs`
- Modify: `apps/portal/RvtPortal.Application.Tests/Sites/SiteTestDoubles.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteReadAdapter.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Sites/SiteArchiveAdapter.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveWorkspaceFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SpaTestApplicationFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SiteArchiveServiceSecurityTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SiteConcurrencyTests.cs`

**Interfaces:**
- Produces:
  - `SiteArchiveState(Guid SiteId, bool Archived, string? ArchiveUrl)`.
  - `ISiteArchivePort.CleanupSupersededAsync(Guid siteId, string durableArchiveUrl, CancellationToken)`.
  - `ISiteArchiveService.DeleteSupersededAsync(Guid siteId, string durableArchiveUrl, CancellationToken)`.

- [x] **Step 1: Extend archive state and project the canonical URL**

Use a correlated, no-tracking projection:

```csharp
.Select(site => new SiteArchiveState(
    site.Id,
    site.Archived,
    domainContext.SiteArchived
        .Where(item => item.SiteId == site.Id)
        .Select(item => item.PictureLink)
        .SingleOrDefault()))
```

Update all test-state constructors with `ArchiveUrl: null` or their canonical
URL.

- [x] **Step 2: Make the blob key stable while preserving local isolation**

In `SiteArchiveWorkspaceFactory.Create`, retain the unique `archiveId` for
`rootPath` and `zipPath`, but set:

```csharp
var blobName = $"{siteId:N}/site-archive.zip";
```

- [x] **Step 3: Replace arbitrary URL deletion with guarded reconciliation**

Change the application and host ports to
`CleanupSupersededAsync`/`DeleteSupersededAsync`. Derive the candidate with the
same stable blob name. Parse and validate the durable URL before any delete. If
the candidate URI identifies the durable blob, return without calling
`DeleteIfExistsAsync`; otherwise delete the candidate with snapshots included.
Malformed or unverifiable canonical URLs fail closed and are mapped to
`SiteArchiveCleanupResult.Failed`.

- [x] **Step 4: Refactor `ArchiveAsync` around durable state**

For `state.Archived && state.ArchiveUrl is not null`, reconcile before reading
detail. After an export and normal claim, reconcile only when the durable URL
differs from the export URL.

In the transaction catch, call `GetArchiveStateAsync(id,
CancellationToken.None)`. If its canonical URL equals the export URL, treat the
transaction as committed. If it contains a different canonical URL, reconcile
the stable candidate and treat the request as a loser. If no canonical URL is
available, or verification fails, retain the stable blob and rethrow the
original persistence exception.

- [x] **Step 5: Update concurrency coverage for one shared candidate**

Make `CoordinatedArchivePort.ExportAsync` return the same
`https://archive.example/<site-id>/site-archive.zip` URL for both requests.
Assert one metadata row, one active URL, two successful results, and zero
physical deletes. Add a separate legacy-URL loser case if needed to retain
cleanup coverage.

- [x] **Step 6: Run focused application and SPA tests**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --no-restore -m:1
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SiteConcurrencyTests|FullyQualifiedName~SiteArchiveWorkspaceFactoryTests|FullyQualifiedName~SiteArchiveServiceSecurityTests'
```

Expected: all pass; PostgreSQL-gated tests may skip only for the documented
missing environment.

---

### Task 4: Make canonical PostgreSQL deployment reject duplicates without deleting data

**Files:**
- Modify: `apps/portal/database/postgres/post-load/06_site_write_uniqueness.sql`
- Modify: `apps/portal/RvtPortal.Spa.Tests/SchemaDeployTests.cs`
- Keep: `apps/portal/RVT.DataAccess/Migrations/20260723234806_EnforceSiteWriteUniqueness.cs`

**Interfaces:**
- Consumes: the one-time EF migration for deterministic cleanup.
- Produces: a rerunnable clean-data guard and canonical unique-index repair.

- [x] **Step 1: Replace cleanup CTEs with duplicate detection**

Under the existing table locks, use one `DO` block:

```sql
IF EXISTS
(
    SELECT 1
    FROM public.notification_setting
    GROUP BY site_user_id
    HAVING COUNT(*) > 1
)
OR EXISTS
(
    SELECT 1
    FROM public.site_archived
    GROUP BY site_id
    HAVING COUNT(*) > 1
)
THEN
    RAISE EXCEPTION
        'Cannot enforce site write uniqueness while duplicate owner rows exist.'
        USING HINT =
            'Apply EF migration 20260723234806_EnforceSiteWriteUniqueness '
            'or resolve duplicates manually, then rerun RVT.SchemaDeploy.';
END IF;
```

After the guard, drop only the two named indexes if present and recreate them as
unique. Do not update or delete table rows in this script.

- [x] **Step 2: Run SchemaDeploy and migration tests**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~SchemaDeployTests|FullyQualifiedName~UniquenessMigration'
```

Expected: all selected tests pass.

- [x] **Step 3: Regenerate and inspect the canonical PostgreSQL migration script**

Run PostgreSQL `dotnet ef migrations script` from
`20260714132042_CanonicalBaseline` to
`20260723234806_EnforceSiteWriteUniqueness`, writing only to `/private/tmp`.
Expected: the script locks, deterministically deduplicates, reconciles
`site.archived`, and creates both unique indexes. SQL Server adapter DML remains
under structural/runtime-unit coverage; do not report SQL Server
migration-deployment closure.

---

### Task 5: Final verification, state handoff, review, and single commit

**Files:**
- Modify: `project_state.md`
- Review: every file in `git diff --name-only`

**Interfaces:**
- Produces: verified handoff documentation and the requested intentional commit.

- [x] **Step 1: Run the complete verification matrix**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj --no-restore -m:1
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -m:1
dotnet build apps/portal/RvtPortal.Spa.sln --no-restore -m:1
RVT_EF_PROVIDER=postgres \
RVT_EF_CONNECTION='Host=localhost;Database=rvt_design_time;Username=rvt;Password=not-a-secret' \
dotnet ef migrations has-pending-model-changes \
  --project apps/portal/RVT.DataAccess/RVT.DataAccess.csproj \
  --context RVTDbContext --no-build
git diff --check
```

Run the `RequiresPostgresFact` case when
`RVT_TEST_POSTGRES_CONNECTION` is set; otherwise record the explicit skip.

- [x] **Step 2: Update `project_state.md`**

Record the current branch/base, modified file structure, new state/port
signatures, provider SQL decisions, deterministic blob-key compatibility,
unknown-commit behavior, migration/deployment split, exact test counts,
warnings, PostgreSQL canonical-provider scope, the separate SQL Server migration
gap, and PostgreSQL skip reason. Preserve the instruction that a future session
starts by reading `project_state.md`.

- [x] **Step 3: Obtain an independent diff review**

Reviewers must inspect archive safety, provider SQL, migration ordering,
SchemaDeploy non-destructiveness, and scope cleanliness. Resolve every critical
or important finding and rerun affected gates.

- [x] **Step 4: Stage only intended files and create one commit**

Exclude `.codegraph/`, `apps/.nuget-packages/`,
`.superpowers/sdd/progress.md`, and historical reports. Then:

```bash
git add <explicit intended paths>
git diff --cached --check
git commit -m "fix: serialize site archive and notification writes"
```

Expected: one commit containing the repair, tests, migration, approved design,
implementation plan, and state handoff.

---

### Final review fix closure

The review of commit `c9295f0ff087275b8129e18bfeeb99357f430a1a`
identified two Important gaps and one Minor coverage gap. This fix wave closes
them while preserving the plan's single amended commit and parent
`19e8dbe0e98664b4bb05c2dd571dfca7c41abf5e`.

- [x] Primary transaction failures survive rollback and disposal faults.
  `EfCoreUnitOfWork` captures with `ExceptionDispatchInfo`, rolls back with
  `CancellationToken.None`, explicitly disposes application, search, and
  domain wrappers in reverse order, retains secondary failures as best-effort
  diagnostics, and applies the same rule to ambient enlistments.
- [x] SQL Server runtime DML is exercised through `UseSqlServer` command
  interception without a live server. The captured/suppressed archive batch
  covers the locked owner predicate, parameterized complete metadata insert,
  and winner-only site archived update; the notification batch covers the
  locked complete update, owner predicate, `@@ROWCOUNT` gate, and conditional
  complete insert.
- [x] Archive cleanup explicitly covers percent-encoded and query/SAS
  equivalents plus same-account/container wrong-effective-port failure.
- [x] Strict controls were observed:
  - UoW primary RED failed 1/1 with rollback replacing commit; GREEN passed
    1/1. A normal-throw mutation then failed 1/1 on lost stack, and restored EDI
    passed 1/1. A throwing-`Exception.Data` RED failed 1/1 before guarded
    diagnostics; the full focused UoW class passed 10/10.
  - SQL Server RED passed notification but failed archive 1/2 on its real
    follow-up reader. The compound archive batch passed 2/2. Removing
    `HOLDLOCK` from both branches failed 2/2; restoration passed 2/2. Swapping
    archive owner/URL and notification email/SMS values failed 2/2 on exact
    placeholder-to-column mapping assertions before restoration.
  - URL safety mutation RED failed 3/3 by attempting the two equivalent-URL
    deletes and accepting the wrong port; restored production passed 3/3.
- [x] Fresh gates passed:
  - combined UoW/SQL Server/archive-security slice: 28/28;
  - `RvtPortal.Application.Tests`: 48/48;
  - `RvtPortal.Spa.Tests`: 415 passed, 9 provider-gated skipped, 424 total;
  - `RvtPortal.Spa.sln`: 0 errors, five existing NU1903 advisories;
  - PostgreSQL `has-pending-model-changes`: no changes;
  - `git diff --check`: clean.

`RVT_TEST_POSTGRES_CONNECTION` remains unset; live PostgreSQL concurrency and
deployed-schema closure are not claimed. SQL Server command interception closes
runtime DML structure only; live execution and SQL Server migration deployment
remain separate and unclosed. The final read-only re-review reported no
remaining Critical, Important, or Minor findings.

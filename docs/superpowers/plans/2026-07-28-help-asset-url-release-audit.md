# Help Asset URL Release Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the non-authoritative Help asset URL SQL preflight with one
BCL-only application policy and a focused read-only .NET release-audit
executable that produces secret-safe, deterministic receipts.

**Architecture:** `RvtPortal.Application` owns all URL semantics.
`HelpMutationValidator` delegates to that policy, while the separate
`RVT.ReleaseAudit` Npgsql adapter reads every `public.help_asset` row inside a
repeatable-read, read-only transaction and applies the same persisted-value
validation. The executable does not start the Portal host and does not depend
on `RvtPortal.Spa`, `RVT.DataAccess`, or EF Core. Tests share one corpus source
between the application-policy and audit/parity suites so drift is detectable.

**Tech Stack:** .NET 10, C# 14, xUnit 2.9.3, Npgsql 9.0.3,
`System.Text.Json`, PostgreSQL `pg_temp`, Bash, Git.

## Global Constraints

- Begin implementation from current `main` on the approved branch
  `codex/help-asset-url-release-audit`. The plan-only branch is not the
  implementation branch.
- Treat
  `docs/superpowers/specs/2026-07-28-help-asset-url-release-audit-design.md`
  as the decision authority. Do not change its approved interface, command
  shape, exit codes, or dependency direction without a new design decision.
- Do not use the production connection string supplied in the historical
  conversation. Do not access any production, release, shared-development, or
  externally hosted database during implementation.
- PostgreSQL verification is opt-in and may use only
  `RVT_TEST_POSTGRES_CONNECTION` against a disposable, connection-scoped
  `pg_temp.help_asset` table. If the variable is absent, the integration test
  must report skipped.
- `RVT_RELEASE_AUDIT_CONNECTION` is the only supported runtime source for the
  release connection. Never accept a connection string or credential as a
  command-line argument, checked-in setting, receipt field, log message, or
  exception detail.
- `RvtPortal.Application` must remain BCL-only. It may not gain Npgsql,
  ASP.NET Core, EF Core, or adapter references.
- Production row reads always use the hard-coded `public.help_asset` relation.
  The `pg_temp.help_asset` selection is internal and test-only; no arbitrary
  SQL identifier or query text crosses the CLI boundary.
- Delete `apps/portal/docs/release/validate-help-asset-urls.sql` and its two
  source-contract tests. Do not retain, rename, or rewrite its regular
  expression as a policy artifact.
- Preserve the mutation endpoint's current field name and all three
  user-visible message cases: `Assets[index].Url is required.`,
  `Assets[index].Url must be 512 characters or fewer.`, and
  `Asset URL must be an absolute HTTPS URL or a /help-assets/ path.`
- The receipt must never serialize a raw URL, a connection string, a password,
  a host credential, or `Uri.UserInfo`. Per-row evidence is limited to asset
  ID, article ID, and stable violation code.
- A database/read/cancellation/receipt-write failure exits `3` and must never
  be reported as a complete pass. Invalid command input or a missing release
  connection exits `2`; complete findings exit `10`; a complete zero-finding
  scan exits `0`.
- Help Admin and R2 remain `CONDITIONAL`/unchecked after tool implementation.
  Only complete zero-finding receipts for every targeted release database may
  support a later, separately reviewed `READY` decision.
- Do not change the Help schema, Help roles, HTTP routes, client behavior,
  object storage, Portal host startup, or unrelated content validation.
- Blob storage client/service unification remains approved future pending work
  under R4 and is outside this plan.
- Every implementation task follows focused RED, minimal GREEN, regression
  verification, `git diff --check`, and a dedicated commit.

## Planned File Structure

Create:

```text
apps/portal/RvtPortal.Application/Help/HelpAssetUrlPolicy.cs
apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyCases.cs
apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyTests.cs
apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj
apps/portal/RVT.ReleaseAudit/Program.cs
apps/portal/RVT.ReleaseAudit/ReleaseAuditOptions.cs
apps/portal/RVT.ReleaseAudit/HelpAssetUrlAudit.cs
apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs
```

Modify:

```text
apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs
apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs
apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs
apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
apps/portal/RvtPortal.Spa.sln
apps/portal/scripts/verify-backend.sh
apps/portal/README.md
Rvt.Mono.slnx
docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md
docs/release/portal/CUTOVER_RUNBOOK.md
docs/development/portal/development-guidelines.md
docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
project_state.md
```

Delete:

```text
apps/portal/docs/release/validate-help-asset-urls.sql
```

---

### Task 1: Define the shared URL corpus and pure application policy

**Files:**

- Create:
  `apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyCases.cs`
- Create:
  `apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyTests.cs`
- Create:
  `apps/portal/RvtPortal.Application/Help/HelpAssetUrlPolicy.cs`
- Verify, do not modify:
  `apps/portal/RvtPortal.Application/RvtPortal.Application.csproj`

**Interfaces:**

```csharp
public static class HelpAssetUrlPolicy
{
    public const int MaximumLength = 512;

    public static HelpAssetUrlValidationResult ValidateMutationValue(
        string? value);

    public static HelpAssetUrlValidationResult ValidatePersistedValue(
        string? value);
}

public sealed record HelpAssetUrlValidationResult(
    string? CanonicalValue,
    string? ViolationCode)
{
    public bool IsValid => ViolationCode is null;
}
```

The test corpus record is test support, not production policy:

```csharp
namespace RvtPortal.Testing.Help;

public sealed record HelpAssetUrlCase(
    string Name,
    string? Input,
    string? MutationCanonicalValue,
    string? MutationViolation,
    string? PersistedCanonicalValue,
    string? PersistedViolation);
```

Its `All` collection must include null, empty, whitespace-only, 512/513
characters, leading/trailing whitespace, embedded spaces, tabs, controls,
backslashes, protocol-relative input, valid and invalid `/help-assets/` paths,
HTTP and other schemes, user-info, uppercase HTTPS,
`https://:443/guide.pdf`, IPv4, bracketed IPv6, IDN, query, and fragment
samples. Every stable code must have at least one explicit case:
`required`, `too_long`, `not_canonical`, `unsafe_character`,
`unsupported_relative_path`, `absolute_https_required`, `host_required`,
`user_info_forbidden`, and `malformed_uri`.

- [ ] **Step 1: Add the shared corpus and failing pure-policy tests**

Create `HelpAssetUrlPolicyCases.All` as the single table of
`HelpAssetUrlCase` records. Add tests that iterate every case through both
policy methods and assert all four expected outputs. Add focused facts proving:

```csharp
Assert.Equal(512, HelpAssetUrlPolicy.MaximumLength);
Assert.All(validResults, result => Assert.NotNull(result.CanonicalValue));
Assert.All(invalidResults, result => Assert.Null(result.CanonicalValue));
Assert.Equal(
    "https://docs.rvt.test/guide.pdf",
    HelpAssetUrlPolicy.ValidateMutationValue(
        "  https://docs.rvt.test/guide.pdf  ").CanonicalValue);
Assert.Equal(
    "not_canonical",
    HelpAssetUrlPolicy.ValidatePersistedValue(
        "  https://docs.rvt.test/guide.pdf  ").ViolationCode);
```

Run:

```bash
dotnet test \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~HelpAssetUrlPolicyTests' \
  --nologo
```

Expected: FAIL because `HelpAssetUrlPolicy` does not exist.

- [ ] **Step 2: Implement the BCL-only policy**

Implement a private validation path with this fixed precedence:

1. mutation input is trimmed once; persisted input is not;
2. null/empty canonical input is `required`;
3. persisted input that differs from `Trim()` is `not_canonical`;
4. more than 512 characters is `too_long`;
5. whitespace/control/backslash is `unsafe_character`;
6. protocol-relative and disallowed relative paths are
   `unsupported_relative_path`;
7. a non-HTTPS absolute URI is `absolute_https_required`;
8. an unparseable HTTPS-shaped value is `malformed_uri`;
9. a parsed HTTPS URI without a host is `host_required`;
10. a parsed HTTPS URI with user-info is `user_info_forbidden`.

Use `Uri.TryCreate`; do not introduce regular expressions or a URI package.
For a valid `/help-assets/` value, require
`value.StartsWith("/help-assets/", StringComparison.Ordinal)` and a successful
`Uri.TryCreate(value, UriKind.Relative, out _)`. For an absolute value, compare
the scheme to `Uri.UriSchemeHttps` with
`StringComparison.OrdinalIgnoreCase`.

Run the focused test again. Expected: PASS.

- [ ] **Step 3: Prove the application dependency stays BCL-only**

Run:

```bash
dotnet list \
  apps/portal/RvtPortal.Application/RvtPortal.Application.csproj \
  package
dotnet test \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --configuration Release \
  --nologo
git diff --check
```

Expected: the application project has no package references; all application
tests pass; diff hygiene passes.

- [ ] **Step 4: Commit the pure policy**

```bash
git add \
  apps/portal/RvtPortal.Application/Help/HelpAssetUrlPolicy.cs \
  apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyCases.cs \
  apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyTests.cs
git commit -m "feat: centralize Help asset URL policy"
```

---

### Task 2: Make Help mutations delegate to the policy

**Files:**

- Modify:
  `apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs`
- Modify:
  `apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs`
- Modify:
  `apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Modify:
  `apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`

**Interfaces:**

- `HelpMutationValidator.ValidateShape` calls
  `HelpAssetUrlPolicy.ValidateMutationValue`.
- Valid mutations persist `validation.CanonicalValue`.
- Invalid URLs retain the current `Assets[index].Url` error field and message.
- `RvtPortal.Spa.Tests.csproj` links the corpus source:

```xml
<Compile Include="..\RvtPortal.Application.Tests\Help\HelpAssetUrlPolicyCases.cs"
         Link="Help\HelpAssetUrlPolicyCases.cs" />
```

- [ ] **Step 1: Convert mutation tests to the shared corpus and prove RED**

Replace the current hand-written accepted/rejected URL theories with one theory
fed from `HelpAssetUrlPolicyCases.All`. For valid mutation cases, assert the
stored `HelpAssetMutation.Url` equals `MutationCanonicalValue`. For invalid
cases, assert the exact legacy message selected from the policy code:

```csharp
var expectedMessage = testCase.MutationViolation switch
{
    "required" => "Assets[0].Url is required.",
    "too_long" => "Assets[0].Url must be 512 characters or fewer.",
    _ => "Asset URL must be an absolute HTTPS URL or a /help-assets/ path."
};
Assert.Contains(
    result.Errors,
    error => error.Field == "Assets[0].Url" &&
        error.Message == expectedMessage);
```

Add
`HelpMutationValidator_DelegatesUrlParsingToApplicationPolicy` to
`CutoverReadinessTests`. Read the validator source and assert it contains
`HelpAssetUrlPolicy.ValidateMutationValue` and contains neither
`IsSafeAssetUrl` nor `Uri.TryCreate`. This guard enforces the approved
single-semantic-authority boundary instead of relying only on equivalent
black-box results.

Run:

```bash
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter \
  'FullyQualifiedName~MutationValidator|FullyQualifiedName~HelpMutationValidator_DelegatesUrlParsingToApplicationPolicy' \
  --nologo
```

Expected: the characterization corpus passes where old and new mutation
behavior is equivalent, and the architecture guard FAILS because the old
private `IsSafeAssetUrl` method still owns `Uri.TryCreate`.

- [ ] **Step 2: Delegate mutation URL behavior**

Replace the `Required(..., 512, ...)` plus `IsSafeAssetUrl` block with:

```csharp
var assetUrlValidation =
    HelpAssetUrlPolicy.ValidateMutationValue(asset.Url);
var assetUrl = assetUrlValidation.CanonicalValue ?? asset.Url?.Trim() ?? "";
if (!assetUrlValidation.IsValid)
{
    var message = assetUrlValidation.ViolationCode switch
    {
        "required" => $"{prefix}.Url is required.",
        "too_long" =>
            $"{prefix}.Url must be {HelpAssetUrlPolicy.MaximumLength} characters or fewer.",
        _ => "Asset URL must be an absolute HTTPS URL or a /help-assets/ path."
    };
    errors.Add(new UseCaseError(
        $"{prefix}.Url",
        message));
}
```

Delete `IsSafeAssetUrl`. Keep every non-URL validation path unchanged.

Run the focused mutation tests and the complete application and SPA Help test
sets:

```bash
dotnet test \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --configuration Release \
  --nologo
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~Help' \
  --nologo
git diff --check
```

Expected: PASS; valid mutation results contain the policy's exact canonical
value; HTTP-visible validation remains unchanged.

- [ ] **Step 3: Commit the mutation delegation**

```bash
git add \
  apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs \
  apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs \
  apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
git commit -m "refactor: reuse Help asset URL policy"
```

---

### Task 3: Add the audit classifier and deterministic receipt model

**Files:**

- Create: `apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj`
- Create: `apps/portal/RVT.ReleaseAudit/HelpAssetUrlAudit.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`

**Project shape:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>RVT.ReleaseAudit</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\RvtPortal.Application\RvtPortal.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>RvtPortal.Spa.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

Add a project reference from `RvtPortal.Spa.Tests` to `RVT.ReleaseAudit` for
adapter tests. Do not add a reverse reference from production code.

**Audit model:**

```csharp
public sealed record HelpAssetUrlAuditRow(
    Guid AssetId,
    Guid HelpArticleId,
    string? Url);

public sealed record HelpAssetUrlViolation(
    Guid AssetId,
    Guid HelpArticleId,
    string ViolationCode);

public sealed record HelpAssetUrlAuditReceipt(
    string Environment,
    string Database,
    DateTimeOffset ExecutedAtUtc,
    string Revision,
    string AuditVersion,
    int RowsScanned,
    int ViolationCount,
    string Outcome,
    IReadOnlyList<HelpAssetUrlViolation> Violations);
```

`HelpAssetUrlAudit.Classify` must call only
`HelpAssetUrlPolicy.ValidatePersistedValue`; it may not parse a URI itself.

- [ ] **Step 1: Add failing classifier/receipt tests**

Feed the exact shared corpus through `HelpAssetUrlAudit.Classify` and assert
the persisted expected code for every case. Add facts proving:

- rows scanned counts every input row;
- valid rows are absent from violations;
- violations order by article ID, asset ID, then code;
- `ViolationCount == Violations.Count`;
- outcome is exactly `pass` for zero findings and `blocked` otherwise;
- receipt JSON has stable property/row order;
- serialized JSON contains no raw input URL and no supplied credential marker.

Run:

```bash
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~HelpAssetUrlAuditTests' \
  --nologo
```

Expected: FAIL because the audit project and classifier do not exist.

- [ ] **Step 2: Implement classification and receipt creation**

Implement `Classify` as a pure enumerable transformation. Materialize the
input exactly once, evaluate every row, discard the URL before constructing a
violation, and order the resulting evidence deterministically. Construct the
receipt with caller-supplied environment/database/time/revision/version values
so tests do not depend on the clock or assembly metadata.

Use one shared serializer:

```csharp
internal static readonly JsonSerializerOptions ReceiptJsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
```

Append exactly one final newline when writing JSON. Do not add extension-data,
exception, URL, connection, or diagnostic-message fields.

Run the focused audit tests. Expected: PASS.

- [ ] **Step 3: Verify dependency direction**

Run:

```bash
dotnet list apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj reference
dotnet list apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj package
dotnet list apps/portal/RvtPortal.Application/RvtPortal.Application.csproj reference
git diff --check
```

Expected: the audit references only `RvtPortal.Application` and Npgsql; the
application does not reference the audit or any adapter.

- [ ] **Step 4: Commit the audit core**

```bash
git add \
  apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj \
  apps/portal/RVT.ReleaseAudit/HelpAssetUrlAudit.cs \
  apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj
git commit -m "feat: add Help URL audit core"
```

---

### Task 4: Add fail-closed CLI, database reader, and receipt writing

**Files:**

- Create: `apps/portal/RVT.ReleaseAudit/ReleaseAuditOptions.cs`
- Create: `apps/portal/RVT.ReleaseAudit/Program.cs`
- Modify: `apps/portal/RVT.ReleaseAudit/HelpAssetUrlAudit.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs`

**CLI contract:**

```text
help-asset-urls
--environment <nonblank label>
--revision <nonblank deployed git SHA>
--receipt <nonblank output path>
```

The connection comes only from `RVT_RELEASE_AUDIT_CONNECTION`.

**Exit contract:**

```csharp
internal const int Passed = 0;
internal const int InvalidInput = 2;
internal const int AuditFailure = 3;
internal const int ViolationsFound = 10;
```

**Production query:**

```sql
SELECT id, help_article_id, url
FROM public.help_asset
ORDER BY help_article_id, id;
```

- [ ] **Step 1: Write failing option and orchestration tests**

Add tests for missing/unknown/duplicate command names, flags, and values;
missing `RVT_RELEASE_AUDIT_CONNECTION`; success; findings; database exception;
cancellation; and receipt-directory/write failure. Inject environment lookup,
UTC time, assembly version, row reader, and receipt writer into an internal
`ReleaseAuditProgram.RunAsync` seam.

Assertions:

```csharp
Assert.Equal(2, invalidInputExit);
Assert.Equal(3, databaseFailureExit);
Assert.Equal(3, receiptFailureExit);
Assert.Equal(10, findingsExit);
Assert.Equal(0, passingExit);
Assert.DoesNotContain(secretMarker, stdout + stderr, StringComparison.Ordinal);
Assert.DoesNotContain(rawRejectedUrl, stdout + stderr, StringComparison.Ordinal);
```

Run the focused audit tests. Expected: FAIL because parsing/orchestration is
absent.

- [ ] **Step 2: Implement exact option parsing**

`ReleaseAuditOptions.Parse` accepts exactly one command and each required flag
exactly once. The environment is 1-64 ASCII letters, digits, `.`, `_`, or `-`.
The deployed revision is 7-64 hexadecimal characters. Reject unknown flags,
duplicate flags, missing values, blank values, values outside those shapes,
and a receipt path resolving to a directory. Resolve the receipt with
`Path.GetFullPath` and convert any path exception into invalid input. Do not
print argument values in usage errors.

The usage text is:

```text
Usage: RVT.ReleaseAudit help-asset-urls --environment <label> --revision <git-sha> --receipt <path>
Set RVT_RELEASE_AUDIT_CONNECTION in the process environment.
```

- [ ] **Step 3: Implement the read-only Npgsql row reader**

Open `NpgsqlConnection`, begin
`IsolationLevel.RepeatableRead`, execute:

```sql
SET TRANSACTION READ ONLY;
```

then execute the production query with sequential access. Read all three
columns, allowing `url` to be null, and do not commit. Roll back after a
complete read; disposal remains the fallback. Any open, transaction, command,
read, cancellation, rollback, or disposal exception makes the orchestration
return `3`.

The internal test seam selects only one of two constants:

```csharp
internal const string ProductionRelation = "public.help_asset";
internal const string TestRelation = "pg_temp.help_asset";
```

Build the query from a private switch over an internal enum; never concatenate
caller input. Keep the connection/transaction code in an internal overload:

```csharp
internal static Task<IReadOnlyList<HelpAssetUrlAuditRow>> ReadRowsAsync(
    NpgsqlConnection openConnection,
    HelpAssetRelation relation,
    Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task>?
        transactionProbe,
    CancellationToken cancellationToken);
```

The production path opens the connection, passes `Production`, and supplies no
probe. The opt-in test passes the already-open connection that owns the
temporary table, selects `Temporary`, and uses `transactionProbe` only to
assert transaction settings before the row query. The probe receives no URL or
credential and is never reachable from CLI input.

- [ ] **Step 4: Implement deterministic, secret-safe receipt writing**

Create the receipt parent directory only when it does not exist. Write UTF-8
without BOM to a sibling temporary file, flush and close it, then atomically
move it over the requested path. If serialization, directory creation, write,
flush, or move fails, remove only the known sibling temporary file and return
`3`; never claim a complete audit.

Use `connection.Database` after opening for the database receipt field. Use
`DateTimeOffset.UtcNow` and the audit assembly's informational version in the
production composition root. Neither value is inferred from CLI text.

On database or receipt failure print only:

```text
FAILED: Help asset URL audit did not complete.
```

Do not print exception messages.

- [ ] **Step 5: Implement `Program.cs` as a thin composition root**

Top-level code calls `ReleaseAuditProgram.RunAsync(args, ...)` and returns its
exit code. It reads `RVT_RELEASE_AUDIT_CONNECTION` once through the injected
environment lookup. It must not build a Portal host, load `appsettings.json`,
or resolve services from the SPA container.

Run:

```bash
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~HelpAssetUrlAuditTests' \
  --nologo
dotnet run \
  --project apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj \
  --configuration Release -- \
  help-asset-urls \
  --environment test \
  --revision test-revision \
  --receipt /tmp/rvt-help-audit-should-not-exist.json
test "$?" -eq 2
test ! -e /tmp/rvt-help-audit-should-not-exist.json
git diff --check
```

Run the CLI command with `RVT_RELEASE_AUDIT_CONNECTION` unset. Expected: exit
`2`, usage only, and no receipt. All focused tests and diff hygiene pass.

- [ ] **Step 6: Commit CLI and row reading**

```bash
git add \
  apps/portal/RVT.ReleaseAudit/Program.cs \
  apps/portal/RVT.ReleaseAudit/ReleaseAuditOptions.cs \
  apps/portal/RVT.ReleaseAudit/HelpAssetUrlAudit.cs \
  apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs
git commit -m "feat: run read-only Help URL release audit"
```

---

### Task 5: Prove PostgreSQL behavior with a disposable `pg_temp` table

**Files:**

- Modify:
  `apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs`
- Reuse, do not modify unless the namespace must be imported:
  `apps/portal/RvtPortal.Spa.Tests/Support/RequiresPostgresFactAttribute.cs`

**Interfaces:**

- Test connection variable: `RVT_TEST_POSTGRES_CONNECTION`.
- Test relation: internal fixed `pg_temp.help_asset`.
- Production relation remains fixed `public.help_asset`.

- [ ] **Step 1: Add the opt-in integration test and prove RED**

Add `[RequiresPostgresFact]` test
`RowReader_ReadsCompleteCorpusInsideReadOnlyRepeatableReadTransaction`.
Open one connection, create:

```sql
CREATE TEMP TABLE help_asset (
    id uuid PRIMARY KEY,
    help_article_id uuid NOT NULL,
    url text NULL
) ON COMMIT PRESERVE ROWS;
```

Insert the complete shared corpus with parameters before the audit transaction.
Use the same open connection so its `pg_temp` schema remains visible. Exercise
the production transaction factory and real row reader using the internal
test-relation selection. Before rollback, use a test callback that runs inside
that transaction to query:

```sql
SHOW transaction_read_only;
SHOW transaction_isolation;
```

Assert `on` and `repeatable read`, assert every inserted ID was scanned, and
assert every persisted expected violation code. Roll back.

Temporarily make the reader select `public.help_asset` in the test path. Run:

```bash
RVT_TEST_POSTGRES_CONNECTION='<disposable-test-connection>' \
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter \
  'FullyQualifiedName~RowReader_ReadsCompleteCorpusInsideReadOnlyRepeatableReadTransaction' \
  --nologo
```

Expected: FAIL because the connection-local temporary relation is not used.
Never use the historical production credential for this command.

- [ ] **Step 2: Select the fixed test relation and rerun**

Restore the enum-controlled `pg_temp.help_asset` selection. Run the same
command. Expected: PASS.

With `RVT_TEST_POSTGRES_CONNECTION` unset, run it once more. Expected: one
reported skip, not a silent pass and not a connection attempt.

- [ ] **Step 3: Commit PostgreSQL proof**

```bash
git add apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs
git commit -m "test: prove Help URL audit transaction"
```

---

### Task 6: Remove the SQL authority and register the audit project

**Files:**

- Delete:
  `apps/portal/docs/release/validate-help-asset-urls.sql`
- Modify:
  `apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Modify:
  `apps/portal/RvtPortal.Spa.sln`
- Modify:
  `Rvt.Mono.slnx`

**Interfaces:**

- Remove only:
  `HelpAssetUrlReadinessQuery_IsReadOnlyAndComplete` and
  `HelpAssetUrlReadinessQuery_FlagsApplicationPolicySamples`.
- Preserve unrelated cutover/database tests and their PostgreSQL support.
- Add `RVT.ReleaseAudit` to the Portal solution and `/Apps/Portal/` in the root
  solution.

- [ ] **Step 1: Add a failing no-SQL/source-authority assertion**

Add to the audit test suite a repository contract that asserts:

```csharp
Assert.False(File.Exists(
    Path.Combine(
        repositoryRoot,
        "apps",
        "portal",
        "docs",
        "release",
        "validate-help-asset-urls.sql")));
```

Also assert the audit project is listed by both solution files. Run the focused
test. Expected: FAIL because the SQL exists and the project is unregistered.

- [ ] **Step 2: Delete SQL tests and artifact**

Remove the two named methods from `CutoverReadinessTests`, delete the SQL file,
and remove any `using` made dead solely by those methods. Confirm no policy-like
SQL survives:

```bash
rg -n \
  'validate-help-asset-urls|url !~|url NOT LIKE .*/help-assets' \
  apps/portal docs \
  --glob '*.sql' \
  --glob '*.cs'
```

Expected: no source-contract or SQL-policy match.

- [ ] **Step 3: Add the project to both solutions**

Use:

```bash
dotnet sln apps/portal/RvtPortal.Spa.sln add \
  apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj
```

Add this root solution entry under `/Apps/Portal/`:

```xml
<Project Path="apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj" />
```

Run:

```bash
dotnet sln apps/portal/RvtPortal.Spa.sln list
dotnet sln Rvt.Mono.slnx list
dotnet build apps/portal/RvtPortal.Spa.sln \
  --configuration Release \
  --nologo \
  --disable-build-servers
git diff --check
```

Expected: both catalogs list `RVT.ReleaseAudit`; the Portal solution builds.

- [ ] **Step 4: Commit SQL retirement and project registration**

```bash
git add \
  apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj \
  apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs \
  apps/portal/RvtPortal.Spa.sln \
  Rvt.Mono.slnx
git add -u apps/portal/docs/release/validate-help-asset-urls.sql
git commit -m "build: replace Help URL SQL preflight"
```

---

### Task 7: Publish the audit artifact and document operator use

**Files:**

- Modify: `apps/portal/scripts/verify-backend.sh`
- Modify: `apps/portal/README.md`
- Modify: `docs/release/portal/CUTOVER_RUNBOOK.md`
- Modify: `docs/development/portal/development-guidelines.md`
- Modify:
  `docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md`
- Modify:
  `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`

**Release artifact paths:**

```text
apps/portal/artifacts/backend/
apps/portal/artifacts/release-audit/
```

- [ ] **Step 1: Prove the current release build omits the audit artifact**

Before changing `verify-backend.sh`, run from `apps/portal`:

```bash
./scripts/verify-backend.sh
test -f artifacts/backend/RvtPortal.Spa.dll
test -f artifacts/release-audit/RVT.ReleaseAudit.dll
```

Expected: the existing backend artifact assertion passes, but the final audit
artifact assertion FAILS because the release build does not publish it.

- [ ] **Step 2: Publish and assert the audit artifact**

Extend `verify-backend.sh` to publish the audit after the solution build/tests:

```bash
dotnet publish \
  RVT.ReleaseAudit/RVT.ReleaseAudit.csproj \
  --configuration Release \
  --no-build \
  --output artifacts/release-audit \
  --nologo \
  --disable-build-servers
test -f artifacts/release-audit/RVT.ReleaseAudit.dll
```

Run the three Step 1 commands again. Expected: PASS with both artifacts.

- [ ] **Step 3: Document the release sequence and secret boundary**

Update the README and cutover runbook with this order:

1. publish/deploy the SPA and `RVT.ReleaseAudit` artifacts from one revision;
2. apply EF migrations and `RVT.SchemaDeploy`;
3. provide a least-privilege read-only connection through
   `RVT_RELEASE_AUDIT_CONNECTION`;
4. run `help-asset-urls` with environment, deployed revision, and receipt path;
5. require exit `0` and a complete zero-finding receipt for every target
   database before Help Admin enablement;
6. treat exit `10`, `2`, `3`, or a missing receipt as blocked;
7. retain receipts in the release evidence store, never in source control.

Include the approved command:

```bash
RVT_RELEASE_AUDIT_CONNECTION='<secret connection string>' \
dotnet apps/portal/artifacts/release-audit/RVT.ReleaseAudit.dll \
  help-asset-urls \
  --environment production \
  --revision '<deployed git sha>' \
  --receipt 'artifacts/release/help-asset-urls-production.json'
```

State that operators must not put the connection in shell history, command
arguments, logs, or receipts. The placeholder above is documentation only.

- [ ] **Step 4: Update policy, review, and readiness evidence**

Replace the guideline's SQL test enforcement references with:

- `HelpAssetUrlPolicyTests`;
- shared-corpus mutation tests;
- `HelpAssetUrlAuditTests`; and
- the opt-in PostgreSQL audit-row-reader test.

Update the architecture review and matrix to say the policy/audit tooling is
implemented, but keep R2 unchecked and Help Admin `CONDITIONAL` because no
release-database receipts were produced during implementation. Preserve the
R4 future item to unify Portal blob client/service usage.

- [ ] **Step 5: Verify release publishing and docs**

From `apps/portal` run:

```bash
./scripts/verify-backend.sh
test -f artifacts/backend/RvtPortal.Spa.dll
test -f artifacts/release-audit/RVT.ReleaseAudit.dll
```

From the repository root run:

```bash
rg -n \
  'RVT_RELEASE_AUDIT_CONNECTION|help-asset-urls|CONDITIONAL|blob client/service' \
  apps/portal/README.md \
  docs/release/portal/CUTOVER_RUNBOOK.md \
  docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md \
  docs/development/portal/development-guidelines.md \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
git diff --check
```

Expected: both publish outputs exist, all operational contracts are documented,
Help remains conditional, and diff hygiene passes.

- [ ] **Step 6: Commit publishing and documentation**

```bash
git add \
  apps/portal/scripts/verify-backend.sh \
  apps/portal/README.md \
  docs/release/portal/CUTOVER_RUNBOOK.md \
  docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md \
  docs/development/portal/development-guidelines.md \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
git commit -m "docs: operationalize Help URL audit"
```

---

### Task 8: Run final ratchets and save the implementation checkpoint

**Files:**

- Modify: `project_state.md`
- Verify all files changed by Tasks 1-7.

- [ ] **Step 1: Run focused and aggregate .NET verification**

Run serially on this host:

```bash
dotnet restore apps/portal/RvtPortal.Spa.sln \
  --nologo \
  --disable-build-servers
dotnet build apps/portal/RvtPortal.Spa.sln \
  --configuration Release \
  --no-restore \
  --nologo \
  --disable-build-servers
dotnet test \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore \
  --nologo \
  --disable-build-servers
dotnet test \
  apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore \
  --nologo \
  --disable-build-servers
```

Expected: restore/build pass; both test projects pass except the existing
`RVT_TEST_POSTGRES_CONNECTION` cases report skipped when the variable is
absent. Do not set a production/release connection to remove skips.

- [ ] **Step 2: Run architecture and engineering ratchets**

```bash
tests/verify-rvt-mono-solution.test.sh
tests/verify-rvt-common-source-boundary.test.sh
scripts/verify-engineering-standards.sh --working-tree
git diff --check
git status --short
```

Expected: all guards pass, there is no diagnostic increase, diff hygiene
passes, and only the deliberate implementation/state files are modified.
If new files introduce legitimate style diagnostics, fix the source; do not
weaken policy, update the baseline, or add an exception.

- [ ] **Step 3: Perform the no-secret and no-production audit**

Run:

```bash
git grep -n \
  -e 'RVT_RELEASE_AUDIT_CONNECTION=' \
  -e 'Password=' \
  -- \
  ':!docs/superpowers/plans/2026-07-28-help-asset-url-release-audit.md'
git grep -n \
  -e 'validate-help-asset-urls' \
  -e \"FROM public.help_asset\" \
  -- \
  ':!docs/superpowers/specs/2026-07-28-help-asset-url-release-audit-design.md' \
  ':!docs/superpowers/plans/2026-07-28-help-asset-url-release-audit.md'
```

Review every match. The production query may appear only in
`RVT.ReleaseAudit`; no committed connection value may appear anywhere. Confirm
the implementation record explicitly states that no production/release
database or credential was used.

- [ ] **Step 4: Save the authoritative project state**

Prepend a new top checkpoint to `project_state.md` recording:

- branch and implementation commit sequence;
- policy, audit project, test, solution, artifact, and documentation paths;
- `RVT_RELEASE_AUDIT_CONNECTION` and `RVT_TEST_POSTGRES_CONNECTION` meanings;
- stable violation and exit-code contracts;
- exact verification totals/skips and any non-secret limitations;
- confirmation that the SQL artifact is deleted;
- confirmation that no production/release database was accessed;
- Help Admin/R2 remain `CONDITIONAL`/unchecked pending one zero-finding receipt
  per target release database; and
- the next step is an operator-owned release-database audit, not R3 and not
  blob unification.

- [ ] **Step 5: Commit the verified checkpoint**

```bash
git add project_state.md
git commit -m "docs: record Help URL audit implementation"
git status --short --branch
```

Expected: clean implementation branch.

- [ ] **Step 6: Obtain review before integration**

Request an independent code review covering policy equivalence, dependency
direction, transaction semantics, receipt secrecy, exit behavior, test-only
relation isolation, SQL removal, release documentation, and unchanged
conditional status. Address findings with focused tests and dedicated commits,
then rerun Steps 1-3.

Do not merge or push to `main` until the user explicitly approves integration.

## Completion Boundary

This plan completes implementation and verification of the shared policy and
audit tooling. It does **not** make Help Admin `READY`.

After implementation is integrated, a release operator must run the published
audit from the deployed revision against every targeted release database using
an approved least-privilege read-only principal. Only exit `0` plus a complete
zero-finding receipt for every database may trigger a separate release-readiness
review that checks R2 and changes the matrix from `CONDITIONAL` to `READY`.

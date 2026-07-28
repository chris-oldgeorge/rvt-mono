# Engineering Standards Enforcement

**Status:** Active

**Normative standard:**
[RVT Engineering Standards](engineering-standards.md)

The repository starts in `Ratchet` mode. Existing diagnostics are recorded by
exact tool, rule, and repository-relative path, while new files, changed
surfaces, and baseline increases fail. `Strict` is the zero-baseline
destination; it is not a shortcut around the ratchet.

## Prerequisites and exit codes

Restore .NET dependencies and install the Portal client dependencies before a
full or committed-range run:

```bash
dotnet restore Rvt.Mono.slnx --disable-parallel
npm --prefix apps/portal/RvtPortal.Client ci
```

The verifier returns `0` for compliance, `1` for a policy violation, and `2`
for invalid invocation, missing prerequisites, malformed tool output, or
another tool failure. A tool failure is never treated as a clean result.

## Verification modes

Check tracked and untracked current changes:

```bash
scripts/verify-engineering-standards.sh --working-tree
```

New source files are checked as complete files. Existing source files are
checked against their changed new-side ranges, and the repository baseline
must not increase.

Compare a committed branch with `main`:

```bash
scripts/verify-engineering-standards.sh --base auto --head HEAD
```

`auto` resolves the merge base with `origin/main`; when `HEAD` is exactly
`origin/main`, the comparison is with `HEAD^`. The verifier materializes the
requested head and refuses incompatible or dirty caller dependency inputs
rather than analyzing one revision with another revision's assets. Explicit
revisions are also supported:

```bash
scripts/verify-engineering-standards.sh --base <commit> --head <commit>
```

Inspect every tracked source file:

```bash
scripts/verify-engineering-standards.sh --all
```

All modes ignore generated output and dependency caches. They run .NET
whitespace, style, and analyzer checks for C# plus pinned Prettier and ESLint
checks for supported Portal sources.

## Baseline lifecycle

The legacy baseline is
[`eng/standards/baseline.json`](../../eng/standards/baseline.json). Its identity
is the exact `(tool, ruleId, path)` tuple and its value is the observed count.
It is deterministic, excludes generated paths, and is not a warning
suppression list.

After genuine cleanup, reduce it with:

```bash
scripts/verify-engineering-standards.sh --all --update-baseline
```

Updates are atomic and monotonic. An increase is refused before writing. A
concurrent update re-reads the live baseline under an ownership-checked lock
and also refuses any widened count. A no-op update preserves the existing file
bytes. Do not edit counts by hand and do not use an update to make a changed
surface pass.

`--initialize-baseline` is bootstrap-only, valid only with `--all`, and refuses
to run when the baseline already exists. Initialization and update flags are
mutually exclusive. The checked-in baseline therefore cannot be silently
recreated or widened.

## Exceptions

Exceptions live in
[`eng/standards/exceptions.json`](../../eng/standards/exceptions.json). They
must satisfy GOV-003 and use exactly one exact repository-relative path without
wildcards. The normative standard permits symbol scope only when a
rule-specific validator proves and applies that scope. R9 has no registered
symbol validator, so the current verifier rejects every symbol-scoped record
instead of accepting an exception it cannot apply. Review dates are UTC
calendar dates in ISO `YYYY-MM-DD` form, must be later than introduction dates,
and expired records fail verification.

A complete exact-path exception has this shape:

```json
{
  "version": 1,
  "exceptions": [
    {
      "id": "EXC-NAM-002-001",
      "ruleId": "NAM-002",
      "owner": "portal-platform",
      "path": "apps/portal/RVT.Entities/LegacyContract.cs",
      "justification": "The public type name is consumed by an external binary contract and cannot be renamed before its coordinated migration.",
      "introducedOn": "2026-07-28",
      "reviewOn": "2026-10-28",
      "removalCondition": "Remove after all external consumers deploy the replacement contract and the compatibility alias has no callers.",
      "validation": "The compatibility test resolves this exact type; no directory, wildcard, or second symbol is exempted."
    }
  ]
}
```

Approval requires an accountable owner, compatibility or technical evidence,
a measurable removal condition, and proof that the scope is no broader than
necessary. Inline suppressions must cite the exception ID.

## Local remediation

Run `--working-tree` before and after a change. Fix every diagnostic on a
changed line or in a new file, even if the same tool/rule/path identity is
already baselined elsewhere. When cleanup reduces an untouched legacy count,
review the reported decrease and use the all-scope update command in a
dedicated baseline change.

Useful focused commands are:

```bash
dotnet format Rvt.Mono.slnx whitespace --verify-no-changes --no-restore
dotnet format Rvt.Mono.slnx style --verify-no-changes --no-restore --severity info
dotnet format Rvt.Mono.slnx analyzers --verify-no-changes --no-restore --severity warn
npm --prefix apps/portal/RvtPortal.Client run lint
npm --prefix apps/portal/RvtPortal.Client exec -- prettier --check .
```

The repository aggregate build runs the PostgreSQL-only boundary, restore,
the working-tree standards verifier, compile, and tests in that order:

```bash
scripts/build-mono.sh
```

Do not point local verification at production services or credentials.
Integration tests that require PostgreSQL use the dedicated
`RVT__POSTGRES_INTEGRATION_CONNECTION` test contract.

## CI behavior

The manual Sonar workflow installs Portal dependencies once, restores .NET,
runs:

```bash
scripts/verify-engineering-standards.sh --base auto --head HEAD
```

and only then performs the Release build. The standards step is unconditional
and blocking. Workflow guards reject removed, reordered, conditional,
non-blocking, wrapped, redirected, duplicated, or otherwise disguised npm,
.NET, and verifier commands. Action references remain commit-SHA pinned.

## Ratchet-to-Strict promotion

`Ratchet` remains the bootstrap default while code-style baseline entries
exist. Promote only after the baseline for the promoted code-style scope is
empty and both commands pass:

```bash
scripts/verify-engineering-standards.sh --all
dotnet build Rvt.Mono.slnx --no-restore --nologo -m:1 \
  -p:RvtEngineeringStandardsMode=Strict
```

Change the default to `Strict` only in a dedicated commit after recording both
complete results. A partial run, a non-empty applicable baseline, a tool error,
or a build that succeeds only with omitted projects is invalid promotion
evidence and must be rejected.

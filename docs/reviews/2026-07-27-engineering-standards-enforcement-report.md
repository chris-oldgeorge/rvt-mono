# Engineering Standards Enforcement Report

**Date:** 2026-07-28

**Branch:** `codex/repository-engineering-standards`

**Implementation head before this report:** `2286532`

**Disposition:** R9 enforcement implemented; classified non-R9 aggregate
failures remain open

## Outcome

R9 now provides one normative repository standard, root and module
configuration hierarchy, deterministic diagnostic and exception models, a
changed-surface ratchet, monotonic baseline management, frontend formatting
and naming policy, package/test-framework policy, local aggregate enforcement,
and blocking CI enforcement.

R9 is marked implemented because Tasks 1–6 gates, all standards-specific
matrices, all root shell guards, compile, frontend verification, and real
mutation proofs pass. This status does not claim that every legacy diagnostic
is fixed or that the entire backend test inventory is green. The final
aggregate test run had 203 failures: 186 require the intentionally absent
dedicated PostgreSQL integration connection, and 17 are existing stale
repository-layout expectations assigned to R1. They are reported, not
suppressed, and no production database credential was used.

## Implemented commits

| Area | Commits |
| --- | --- |
| Standard and plan | `e083eda`, `9bd194b`, `1b05562`, `170b4d8` |
| Root configuration | `2212185` |
| Diagnostic and exception model | `3d01096`, `4f23da2`, `e7757de` |
| Changed-surface verifier | `988c0e9`, `6bc3b26`, `f5f1472`, `8d50a96`, `20c3bde`, `5c850cc`, `30acaca` |
| Frontend formatting and naming | `e8759f6`, `d0d659c` |
| Baseline and module policy | `604912d`, `71a84a0`, `20d89b0`, `331c181`, `d3ab241`, `76eecc4` |
| Local and CI integration | `bfa78d0`, `dd3910d`, `299d971`, `c03ba2e`, `2286532` |
| Documentation evidence | `docs: record engineering standards enforcement` (this commit) |

## Baseline

[`eng/standards/baseline.json`](../../eng/standards/baseline.json) was generated
on 2026-07-27. It contains 2,050 exact identities and 8,024 diagnostics.
Generated paths are absent and the exception register is empty.

| Tool | Identities | Diagnostics |
| --- | ---: | ---: |
| `dotnet-format-analyzers` | 27 | 61 |
| `dotnet-format-style` | 1,933 | 7,872 |
| `dotnet-format-whitespace` | 59 | 59 |
| `eslint` | 1 | 2 |
| `prettier` | 30 | 30 |
| **Total** | **2,050** | **8,024** |

Baseline updates are atomic, deterministic, no-op byte stable, and
decrease-only. Initialization refuses an existing baseline, ordinary updates
refuse increases, and concurrent updates revalidate against the live file.

## Requirement traceability

| Approved requirement | Code/configuration | Executable evidence | Command evidence |
| --- | --- | --- | --- |
| One authoritative standard | `docs/development/engineering-standards.md`; root `README.md`; `docs/index.md` | documentation layout guards | `tests/verify-documentation-layout.test.sh` |
| Ratcheted changed-scope enforcement | `scripts/engineering-standards/verify.mjs`; baseline model | new-file, changed-line, increase, decrease, and exact-range scenarios | `tests/verify-engineering-standards.test.sh` |
| Logical-unit compliance | GOV-001 review contract plus changed-range enforcement | stable-count diagnostic on a changed line fails; complete new file fails | model suite and verifier scenario suite |
| Root plus stricter module policy | `.editorconfig`; `Directory.Build.props`; four module imports | hierarchy mutation tests and evaluated project inventory | `tests/verify-engineering-configuration.test.sh`; policy suite |
| Ratchet-to-Strict promotion | evaluated `RvtEngineeringStandardsMode` properties | Ratchet/Strict evaluation guard and documented zero-baseline gate | `--all`; strict build command in the enforcement guide |
| Stable rule/evidence model | `scripts/engineering-standards/model.mjs` | deterministic keys, ordering, realistic parser fixtures, non-mutation | `node --test tests/engineering-standards-model.test.mjs` |
| Owned, expiring exceptions | `eng/standards/exceptions.json`; exact validator | expiry, ordering, exact path/symbol, wildcard, and scope RED cases | model and policy suites |
| .NET formatting/analyzers | three real `dotnet format` phases | changed C# scenarios and temporary real whitespace mutation | verifier suite; temporary `--working-tree` proof |
| TypeScript lint/format | pinned Prettier 3.9.6; ESLint naming policy | changed-file cases and real naming baseline increase | Portal lint; temporary `--working-tree` proof |
| No package/test-policy drift | `eng/standards/module-policy.json` | evaluated MSBuild framework, central-policy, lock, symlink, and solution census mutations | `node --test tests/verify-engineering-standards-policy.test.mjs` |
| Local aggregate enforcement | `scripts/build-mono.sh` | restore/verifier/build/test order and removed-gate mutation | `tests/verify-engineering-standards-integration.test.sh` |
| CI enforcement | `.github/workflows/sonarqube.yml` | blocking/unconditional order, wrappers, options, punctuation, duplicate install, and removed-gate mutations | integration and manual Sonar workflow guards |
| Guards can fail | verifier, hierarchy, source, workflow, and architecture mutation harnesses | nested root, increase, removed gate, C#, TS, shell punctuation, and forbidden reference mutations | complete root shell guard matrix |
| No regression | root configuration and baseline remain unchanged by proof | standards matrix 50/50; 12/12 root guards; compiler/frontend pass; zero ratchet increase | commands below |

## Verification evidence

### Standards and guards

- Model and policy suites: 50 passed, 0 failed.
- Engineering configuration hierarchy: passed.
- Engineering standards verifier scenarios: passed.
- Build/CI integration mutation harness: passed with 0 accepted mutations.
- Working-tree verifier: exit 0 with no baseline increase.
- Root shell guards: 12 passed, 0 failed.
- `git diff --check`: passed.

Commands:

```bash
node --test tests/engineering-standards-model.test.mjs \
  tests/verify-engineering-standards-policy.test.mjs
tests/verify-engineering-configuration.test.sh
tests/verify-engineering-standards.test.sh
tests/verify-engineering-standards-integration.test.sh
scripts/verify-engineering-standards.sh --working-tree
for test_script in $(find tests -maxdepth 1 -type f -name '*.test.sh' | sort); do
  "$test_script"
done
```

The first restricted configuration attempt could not create Roslyn named pipes.
The complete matrix was rerun outside that restriction with .NET SDK 10.0.302
and passed. One root-guard attempt used an over-restricted local `PATH` that
omitted `/usr/sbin/chown`; restoring standard system paths made the unchanged
Sonar runner guard pass. Neither environmental retry changed a gate.

### Backend and frontend

Locked restore passed. The backend build passed with 0 errors and 5 existing
NU1903 warnings for `System.Security.Cryptography.Xml` 10.0.7. These advisories
were not introduced by R9 and are not suppressed.

Backend test totals were:

| Outcome | Count |
| --- | ---: |
| Total | 2,109 |
| Passed | 1,896 |
| Failed | 203 |
| Skipped | 10 |

Failure classification:

- 186 require `RVT__POSTGRES_INTEGRATION_CONNECTION`; no integration database
  was configured for this run.
- 16 resolve source or SQL fixtures through stale pre-monorepo paths.
- 1 is the fixed-depth Mapperly package-reference expectation.
- The 17 path/Mapperly failures are R1 work. They are not R9 regressions.

Project totals for affected suites were AirQ 91/124, MyAtm 155/208, Omnidots
328/392, ReportingMonitor 83/93, Svantek 96/136, and the integration helper
3/6. Portal SPA passed 425 tests with 10 PostgreSQL-dependent skips. All other
backend suites in the aggregate were green.

Frontend lint passed with 0 errors and the two existing
`react-refresh/only-export-components` warnings in `DataViewPanels.tsx`.
Vitest passed 68/68. The Vite production build passed after transforming 1,606
modules.

Commands:

```bash
dotnet restore Rvt.Mono.slnx --locked-mode --disable-parallel
dotnet build Rvt.Mono.slnx --no-restore --nologo -m:1 \
  -p:UseSharedCompilation=false
dotnet test Rvt.Mono.slnx --no-build --nologo -m:1
npm --prefix apps/portal/RvtPortal.Client run lint
npm --prefix apps/portal/RvtPortal.Client run test:run
npm --prefix apps/portal/RvtPortal.Client run build
git diff --check
```

### Real ratchet rejection

A detached temporary worktree reused only local restored assets. One C#
indentation defect was added to
`libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageObjectKey.cs`, and
one invalid TypeScript variable name was added to
`apps/portal/RvtPortal.Client/src/safeUrl.ts`.

The real verifier exited 1 and reported:

- `CSH-002` with the exact C# path; and
- changed-surface and increase evidence for
  `eslint @typescript-eslint/naming-convention`, the exact TypeScript path,
  line 5, `baseline=0`, and `observed=1`.

The disposable worktree was removed. Production source and the baseline were
not modified.

## Remaining work

R9 establishes the enforcement floor; it does not erase the baselined legacy
debt. The ordered remediation sequence remains:

1. R1 — repair stale architecture guards and the shared repository-layout
   helper, including the Mapperly path-depth expectation.
2. R2 — align Help Admin with its release decision.
3. R3 — select the authoritative reporting lineage.
4. R4 — retire dead Portal infrastructure and unify blob storage usage.
5. R5 — continue Portal vertical extraction.
6. R6 — finish monitor narrow-port migration.
7. R7 — remove synchronous compatibility paths.
8. R8 — split independently selectable infrastructure from Common.
9. R9 — enforcement foundation implemented by this report.
10. R10 — reduce Portal client and host structural size.
11. R11 — dispose of ambient untracked configuration.

R1 is next. It must use a separate test-first plan and must not absorb R2
product behavior or R3 reporting-lineage decisions.

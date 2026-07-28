# Repository Engineering Standards Design

**Status:** Implemented

**Date:** 2026-07-27

**Normative standard:**
[`docs/development/engineering-standards.md`](../../development/engineering-standards.md)

## Purpose

This design establishes one professional engineering contract for the RVT
monorepository. It governs every subsequent remediation phase and every future
code-quality analysis. The contract covers architecture, naming, source style,
dependencies, behavior, testing, observability, documentation, review evidence,
and automated enforcement.

The repository currently has four imported module roots, four different style
baselines, mixed analyzer severities, mixed test frameworks, different package
management approaches, and transitional hexagonal boundaries. A single strict
configuration applied immediately would turn historical debt into an unrelated
delivery blocker. A prose-only guide would not prevent new drift.

The approved approach is therefore a layered standard with ratcheted
enforcement.

## Goals

1. Make expectations precise enough that two reviewers classify the same code
   consistently.
2. Keep dependencies pointing inward and infrastructure decisions at composition
   roots.
3. Prevent new style, analyzer, architecture, dependency, and test debt.
4. Reduce existing debt whenever its logical unit is changed.
5. Give every exception an owner, review date, and removal plan.
6. Make every reported finding traceable to a stable rule and exact evidence.
7. Turn fully remediated rule categories into ordinary build blockers.

## Non-goals

- Reformat or rename the entire legacy repository in one change.
- Split projects only to achieve visual symmetry.
- Force an immediate MSTest-to-xUnit or xUnit-to-MSTest migration.
- Replace stable public, serialized, or database contracts for style alone.
- Introduce dynamic provider loading without a deployment requirement.
- Treat line coverage, project count, or file size as standalone quality goals.

## Considered approaches

### Prose-only guide

This has low adoption cost but weak enforcement. It would leave the existing
module configurations authoritative in practice and would not prevent drift.

### Immediate strict root baseline

This produces rapid mechanical uniformity but makes unrelated work responsible
for the whole historical backlog. It encourages blanket suppressions and large,
risky formatting changes.

### Layered standard with ratcheted enforcement

This is the approved approach. One normative document defines repository rules.
Root tooling enforces common defaults. Module addenda may only add stricter or
domain-specific rules. Existing violations are baselined, new violations fail,
and the baseline can only decrease.

## Authority model

The normative standard uses `MUST`, `SHOULD`, and `MAY` in their ordinary
requirements sense:

- `MUST` and `MUST NOT` are mandatory unless an approved exception exists.
- `SHOULD` and `SHOULD NOT` require a documented reason when not followed.
- `MAY` identifies a permitted choice that must remain consistent within its
  logical scope.

The repository standard has precedence over module conventions. A module
addendum may strengthen a rule or describe a domain-specific invariant. It may
not silently weaken or contradict a repository rule.

## Change-surface model

Ratcheting is based on logical change surface rather than arbitrary changed
lines:

- A new file complies completely.
- A changed type, function, component, migration, or configuration block is the
  minimum compliance unit.
- Unrelated legacy code elsewhere in the same large file need not be rewritten.
- A change may not increase the module or repository violation baseline.
- Safe automated formatting may cover a whole file when it does not obscure
  behavioral review.
- Public, serialized, and persisted names remain compatibility contracts until
  an explicit migration is approved.

## Rule model

Rules use stable identifiers grouped by category:

| Prefix | Category |
| --- | --- |
| `GOV` | Governance, ratcheting, and exceptions |
| `ARC` | Architecture and dependency direction |
| `NAM` | Naming and source organization |
| `CSH` | C# language and .NET conventions |
| `WEB` | TypeScript and React conventions |
| `DAT` | SQL, persistence, dates, and data contracts |
| `ASY` | Asynchrony, cancellation, and concurrency |
| `ERR` | Results, exceptions, and API errors |
| `NET` | Outbound HTTP and external integrations |
| `RES` | Resource, stream, and storage ownership |
| `OBS` | Logging, metrics, tracing, and health |
| `CFG` | Configuration, secrets, and host boundaries |
| `TST` | Testing and executable architecture evidence |
| `BLD` | Build, package, analyzer, and dependency hygiene |
| `DOC` | Documentation and source comments |
| `REV` | Analysis, review, and completion evidence |

A reportable finding contains the rule ID, severity, exact location and
evidence, consequence, affected boundary, remediation, verification method, and
disposition. Subjective statements without a rule and example are observations,
not findings.

## Severity model

| Severity | Meaning | Required response |
| --- | --- | --- |
| Blocker | Corrupts data, breaks a required boundary, or makes the release unsafe | Stop the affected release/remediation phase |
| High | Likely production defect, major coupling, or missing critical verification | Fix in the current phase unless explicitly deferred |
| Medium | Material maintainability, consistency, or reliability debt | Plan and ratchet; fix when the logical unit is touched |
| Low | Local clarity or minor consistency issue | Fix opportunistically without obscuring behavior |

Security findings use the dedicated security workflow and severity model. The
engineering standard still requires secure configuration and secret-safe code,
but it does not replace a security assessment.

## Exception model

An exception record contains:

- stable exception ID and related rule ID;
- owner and affected exact path;
- technical and compatibility justification;
- introduced and next-review dates;
- measurable removal condition and remediation link; and
- validation proving the exception is no broader than required.

Blanket analyzer suppression, wildcard path exclusion, unexplained warning
lists, and ownerless TODO comments are not valid exceptions.

The normative standard also allows a symbol scope when a rule-specific
validator both proves and applies that exact scope. R9 registers no such
validator. Its executable exception model therefore fails closed on every
symbol-scoped record and supports exact repository-relative paths only.

## Enforcement design

Enforcement is introduced in ordered layers:

1. Publish the normative standard and rule catalog.
2. Inventory root and module configuration differences.
3. Add a root formatting and analyzer baseline that does not hide module debt.
4. Capture existing violations in machine-readable baselines by rule and path.
5. Fail CI on new violations and baseline increases.
6. Add dependency and architecture guards with mutation tests.
7. Normalize package and test conventions incrementally by module.
8. Remove baseline entries as remediation phases clean their logical units.
9. Promote a rule to an unconditional build blocker when its baseline reaches
   zero.

Tooling is expected to include root `.editorconfig` and shared MSBuild
properties, `dotnet format`, TypeScript strict mode and ESLint, repository shell
guards, architecture tests, and machine-readable baseline/exception files.
Exact file formats and CI integration belong in the implementation plan.

## Remediation integration

The project architecture and code-quality review remains the backlog authority.
Before each remediation phase begins, its implementation plan must map the
affected files and expected changes to applicable standard rule IDs. During
review, findings use the same IDs. Completion evidence records the baseline
delta and explains every remaining exception.

The standards foundation is a prerequisite for R1 through R11; it does not mark
those phases complete. R9 implements the shared tooling and baseline described
here. Other phases apply the normative rules immediately within their own
change surfaces.

## Verification strategy

The standards artifact is complete when:

- it contains no placeholders or contradictory rules;
- every approved design section is represented;
- the documentation index and remediation review link to it;
- repository guards still pass;
- Markdown links resolve within the repository;
- `git diff --check` passes; and
- the design and current state are committed on an isolated branch.

Subsequent implementation is complete only when automated enforcement proves
that new violations fail, existing baselines cannot increase, and at least one
representative architecture guard is mutation-tested.

## Approved design record

The user approved the following sections in sequence:

1. ratcheted governance and exception ownership;
2. hexagonal architecture and provider dependency isolation;
3. naming, source style, and structural thresholds;
4. async, error, HTTP, time, storage, observability, and host reliability; and
5. testing, analysis evidence, package hygiene, and automated enforcement.

No design question remains open. The approved implementation is recorded in
the
[engineering standards enforcement report](../../reviews/2026-07-27-engineering-standards-enforcement-report.md)
and operated through the
[engineering standards enforcement guide](../../development/engineering-standards-enforcement.md).

The R9 enforcement deliverable is implemented: its focused model, policy,
configuration, verifier, local-build, and workflow guards pass; every root
shell guard passes; the backend compiles; frontend lint, tests, and production
build pass; and temporary real C# and TypeScript mutations are rejected.
Repository-wide backend tests are not represented as globally green: the
completion evidence records 186 tests that require a dedicated PostgreSQL
integration connection and 17 existing architecture tests assigned to R1 for
stale monorepo layout assumptions. Those classified outcomes neither weaken
the R9 gates nor close R1.

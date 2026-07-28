# RVT Engineering Standards

**Status:** Normative

**Effective:** 2026-07-27

**Applies to:** All production code, tests, build logic, database assets,
configuration, documentation, and code analysis in this repository

**Enforcement:** Ratcheted

## 1. Purpose and authority

This document is the repository-wide engineering contract. It applies to every
new change and governs the remaining architecture and code-quality remediation
work. It is intentionally more precise than a general style guide: each rule is
written so it can guide implementation, review, static analysis, or an
executable guard.

`MUST` and `MUST NOT` are mandatory. `SHOULD` and `SHOULD NOT` require a written
reason when not followed. `MAY` identifies a permitted choice that must remain
consistent within its logical scope.

The nearest module guidance may add stricter or domain-specific rules. It MUST
NOT weaken or contradict this standard without an approved repository-level
exception.

## 2. Ratcheted enforcement

### GOV-001 — Compliance unit

- A new file MUST comply completely.
- When code changes, the entire logical unit containing the change MUST comply.
  A logical unit is a type, function, React component, migration, SQL script, or
  configuration block.
- Unrelated legacy code elsewhere in a large file MAY remain baselined.
- Safe automated formatting MAY cover the whole file when it does not obscure
  behavioral review.

### GOV-002 — No-regression rule

A change MUST NOT increase any compiler, analyzer, formatter, lint,
architecture, dependency, or test baseline. Every remediation phase MUST leave
its affected scope at least as compliant as it found it and SHOULD reduce one or
more applicable baselines.

### GOV-003 — Exceptions

An exception MUST record:

- a stable exception ID and affected rule ID;
- an owner and exact path or symbol scope;
- a technical or compatibility justification;
- the introduction and next-review dates;
- the condition under which it can be removed; and
- evidence that it is no broader than necessary.

Blanket suppressions, wildcard exclusions, unexplained warning lists, and
ownerless TODO comments are prohibited. An inline suppression MUST cite its
exception record.

### GOV-004 — Compatibility contracts

Public APIs, serialized names, environment keys, database identifiers, message
contracts, routes, and persisted values MUST be treated as compatibility
contracts. A style improvement MUST NOT change them without an explicit
consumer, rollout, and data-migration plan.

### GOV-005 — Rule precedence

Correctness, data integrity, security, compatibility, and operability take
precedence over style. A rule conflict MUST be resolved explicitly; it MUST NOT
be hidden with a broad suppression.

## 3. Architecture and dependencies

### ARC-001 — Dependency direction

New and remediated slices MUST use a ports-and-adapters dependency direction:

```text
inbound adapter -> application/domain -> port <- outbound adapter
                       ^                         ^
                       |                         |
                 provider-neutral        provider-specific
```

Application and domain code MUST NOT depend on ASP.NET Core, EF Core, cloud or
vendor SDKs, HTTP clients, filesystem implementation details, executable hosts,
or adapter projects.

### ARC-002 — Layer responsibilities

| Layer | Owns | Must not own |
| --- | --- | --- |
| Domain | Invariants, value objects, domain behavior | Transport, persistence, provider SDKs |
| Application | Use cases, policies, results, inward-owned ports, transaction intent | HTTP status codes, EF entities, SDK exceptions |
| Inbound adapter | Routes, authentication attributes, input normalization, transport DTO mapping | Business decisions and direct persistence |
| Outbound adapter | Protocols, SDKs, persistence, serialization, provider failure translation | Cross-use-case orchestration |
| Composition root | Configuration, DI, middleware, provider selection, lifecycle | Business rules |

### ARC-003 — Port ownership

A port MUST be owned by the inward layer that needs the capability. Its
signature MUST use domain/application or BCL types. Provider DTOs, EF entities,
`HttpResponseMessage`, `IFormFile`, and vendor exceptions MUST NOT cross the
port.

### ARC-004 — Composition

Executable hosts MUST be the composition roots. Business code MUST NOT use
service location, global mutable service registries, runtime container access,
or direct construction of infrastructure clients.

### ARC-005 — Project isolation

A dependency SHOULD receive a separate project when at least one of these real
boundaries exists:

1. independent provider selection;
2. volatile or conflicting SDK isolation;
3. independent deployment or versioning;
4. reuse by multiple composition roots; or
5. a separately enforceable dependency graph.

Small cohesive domain rules MUST NOT be split merely to increase project count.
Provider-neutral contracts and each selectable provider adapter SHOULD be
separate. Provider SDKs MUST remain in their provider-specific adapter project.

### ARC-006 — Forbidden coupling

Circular project references, host-to-host dependencies, adapter-to-adapter
coupling, application-to-adapter references, and infrastructure types leaking
through application contracts are prohibited.

### ARC-007 — Persistence ownership

Repositories stage changes; they MUST NOT commit. Application-owned units of
work define transaction boundaries. A use case that changes multiple aggregates
or contexts MUST define atomicity explicitly.

### ARC-008 — External side effects

Network, storage, email, and message side effects MUST NOT be assumed
transactional with a database. Their ordering MUST use one of: idempotency,
compensation, outbox/post-commit dispatch, or an explicitly documented
at-least-once/at-most-once contract.

### ARC-009 — Dynamic loading

Compile-time composition is the default. Dynamic provider discovery, assembly
scanning, or plugin loading MUST NOT be introduced without a demonstrated
deployment requirement and a compatibility/security design.

### ARC-010 — Boundary evidence

Important dependency rules MUST be executable. An architecture guard MUST have
a mutation/regression test proving that a representative forbidden dependency
causes it to fail.

## 4. Naming conventions

### NAM-001 — Domain intent

Names MUST describe domain capability or business intent. `Helper`, `Utils`,
`Manager`, `Processor`, `Common`, `Data`, and `Service` SHOULD NOT be introduced
unless the name has one precise architectural meaning. Prefer names such as
`SiteArchiveAdapter`, `ReportGenerationGateway`, `MonitorReadingParser`, or
`EmailDeliveryPort`.

### NAM-002 — C# identifiers

| Element | Convention | Example |
| --- | --- | --- |
| Namespace, type, record, enum | `PascalCase` | `Rvt.Storage.AzureBlob` |
| Interface | `I` + capability noun | `IObjectStorageClient` |
| Method, property, event | `PascalCase` | `OpenReadAsync` |
| Private instance field | `_camelCase` | `_httpClient` |
| Parameter and local | `camelCase` | `storageKey` |
| Constant | `PascalCase` | `DefaultTimeout` |
| Generic parameter | `T` or descriptive `TName` | `TResult` |

### NAM-003 — Acronyms

Use `Id`, `Uri`, `Url`, `Http`, `Api`, `Utc`, `Sql`, and `Pdf` in ordinary
identifiers. Preserve external or legacy contract casing only under GOV-004.

### NAM-004 — Behavioral names

- Async methods end in `Async`.
- Boolean members use a predicate such as `Is`, `Has`, `Can`, `Should`, or
  `Supports`.
- `Try*` methods return success explicitly and do not throw for the expected
  negative result.
- Commands use imperative verbs; queries describe what they return.
- Exceptions end in `Exception`; validated options end in `Options`.
- Adapters and gateways name the technology or capability they adapt.

### NAM-005 — TypeScript and React

- Components, classes, enums, and types use `PascalCase`.
- Functions, variables, properties, and non-component modules use `camelCase`.
- Hooks begin with `use` and follow the React hook rules.
- TypeScript interfaces MUST NOT use the C# `I` prefix.
- Component filenames match the exported component; test filenames mirror the
  production filename.

### NAM-006 — Database and configuration

- PostgreSQL application objects use lowercase singular `snake_case`.
- API JSON uses `camelCase`.
- .NET configuration sections and members use `PascalCase`.
- Environment keys use the documented `RVT__SECTION__KEY` convention.
- Any incompatible legacy name requires a documented alias or migration.

## 5. C# and .NET source style

### CSH-001 — File shape

Hand-written C# uses file-scoped namespaces, one primary type per file, and a
filename matching the primary type. Folders and namespaces SHOULD correspond.
Nested/private supporting types MAY remain with their sole owner.

### CSH-002 — Formatting

C# uses UTF-8, LF, a final newline, trimmed trailing whitespace, four-space
indentation, braces, and `System` imports first. Automated formatting is
authoritative. A 120-character line is a review target, not a reason to damage
readability.

### CSH-003 — Type clarity

Use `var` when the assigned type is evident and domain meaning remains clear.
Use an explicit type when it improves comprehension, documents a contract, or
prevents a surprising inferred type. A logical unit MUST NOT mix styles
arbitrarily.

### CSH-004 — Nullability

Nullable reference types MUST remain enabled. Nullability annotations are part
of the contract and MUST reflect runtime behavior. The null-forgiving operator
MUST have locally obvious proof or a documented exception.

### CSH-005 — Mutability

Prefer immutable values, `record`/`record struct` where value semantics are
real, `init`-only properties for construction-only state, and the narrowest
visibility. Mutable collections MUST NOT be exposed directly from public
contracts.

### CSH-006 — Language features

Modern language features MAY be used when they improve clarity and are
supported by the pinned SDK. Primary constructors, complex expressions, and
clever pattern matching SHOULD NOT be used when they hide lifetime,
dependencies, or business branches.

### CSH-007 — Source size

Rough review signals are 400 lines for a C# file and 40–60 lines for a method.
Crossing a signal requires a cohesion/testability review, not an automatic
split. Extract when a unit has multiple reasons to change, excessive branching,
unrelated dependencies, or cannot be tested independently.

## 6. TypeScript and React

### WEB-001 — Type safety

TypeScript strict mode MUST remain enabled. `any`, unchecked casts, and
non-null assertions require locally evident proof or an exception. External
data MUST be validated or generated from an authoritative schema before it is
trusted.

### WEB-002 — Component boundaries

Components SHOULD render one cohesive feature. Data access, date conversion,
cache identity, and business calculations SHOULD live in hooks or typed modules
that can be tested independently. Prop drilling SHOULD be replaced only when a
stable shared-state boundary exists.

### WEB-003 — React effects

Effects synchronize with external systems; they MUST NOT be used as a general
derived-state mechanism. Dependencies MUST be complete. Async effects MUST
handle cancellation or supersession so stale responses cannot overwrite newer
state.

### WEB-004 — Response identity

Client caches and in-flight response guards MUST include every value that
changes response meaning, including user/tenant identity, resource identity,
filters, local date, timezone, locale, and authorization scope as applicable.
A response for one identity or calendar context MUST NOT satisfy another.

### WEB-005 — Accessibility and semantics

Interactive UI MUST use semantic elements, keyboard-operable controls,
associated labels, visible focus, and meaningful accessible names. Visual-only
status MUST have a non-color equivalent.

### WEB-006 — Frontend size

Roughly 300 lines is a React component review signal. Extraction is required
when routing, data loading, state transitions, business calculation, and view
rendering cannot be understood or tested as separate responsibilities.

## 7. Data, persistence, and time

### DAT-001 — Query boundaries

Persistence adapters own ORM and SQL behavior. Application code MUST NOT depend
on `DbContext`, provider query types, database exceptions, or persistence
entities unless the entity is explicitly the domain model.

### DAT-002 — Query efficiency

Reads MUST project only needed data, avoid unbounded materialization, prevent
N+1 access, define ordering for pagination, and use no-tracking behavior when
identity tracking is unnecessary.

### DAT-003 — Transactions

Transaction scope MUST match the use-case invariant. Data-access methods MUST
NOT call `SaveChanges` independently. Retries MUST NOT repeat non-idempotent
external side effects.

### DAT-004 — Migrations and scripts

Database changes MUST be forward-compatible for the deployment sequence,
idempotent where scripts may rerun, and paired with validation and rollback or a
documented roll-forward strategy. Unmapped `NOT NULL` columns require a safe
default or an application write migration.

### DAT-005 — Instant versus calendar value

An instant, a local date, a local time, and a timezone are different concepts.

- Persist instants in UTC using an unambiguous type/contract.
- Use `DateOnly` for a calendar date that is not an instant.
- Use `TimeOnly` only when a wall-clock time is intended.
- Store the timezone/offset when future local interpretation matters.
- Convert at explicit boundaries; do not relabel `DateTime.Kind` to simulate
  conversion.

### DAT-006 — Time provider

Business logic MUST obtain current time from an injected provider. Tests MUST
control it. `DateTime.Now`, `DateTime.Today`, and direct system-clock reads are
prohibited in application/domain logic.

### DAT-007 — Serialization contracts

Serialization shape, enum representation, defaults, and versioning MUST be
explicit. Unknown fields SHOULD be tolerated where forward compatibility is
required. Silent lossy conversion is prohibited.

## 8. Asynchrony, cancellation, and concurrency

### ASY-001 — Async end to end

I/O call chains MUST remain asynchronous. `.Result`, `.Wait()`,
`GetAwaiter().GetResult()`, blocking I/O, and fire-and-forget tasks are
prohibited outside a documented process-boundary bridge.

### ASY-002 — Cancellation

Public async APIs that can block MUST accept a `CancellationToken` and propagate
it to downstream operations. Caller cancellation MUST be rethrown unchanged.
Timeouts and provider cancellation with an active caller token MUST be
classified separately.

### ASY-003 — Concurrency ownership

Shared mutable state MUST have an explicit synchronization or ownership model.
Do not rely on collection implementation details, request ordering, scheduler
timing, or singleton accident. Background work MUST be supervised and expose
shutdown behavior.

### ASY-004 — Idempotency

Operations that may be retried, duplicated, or concurrently invoked MUST define
their idempotency key or conflict behavior. Database uniqueness and conditional
writes SHOULD enforce invariants where races matter.

## 9. Results, exceptions, and API errors

### ERR-001 — Expected outcomes

Expected validation, not-found, conflict, authorization, and business-rule
outcomes SHOULD use an explicit result contract. Exceptions MUST represent
unexpected or boundary failures, not ordinary branching.

### ERR-002 — Translation boundary

Adapters MUST translate provider failures into stable application categories.
SDK exception types, raw response bodies, credentials, connection strings, and
internal topology MUST NOT cross or appear in public error contracts.

### ERR-003 — Catch discipline

Empty catches, catch-and-rethrow, broad catches without boundary translation,
and logging the same exception at multiple layers are prohibited. Preserve the
stack and inner exception for internal diagnostics.

### ERR-004 — HTTP errors

HTTP APIs MUST map outcomes consistently to status codes and `ProblemDetails`
or another documented machine-readable contract. Responses SHOULD carry a safe
correlation identifier and MUST NOT expose implementation details.

### ERR-005 — Fallbacks

A fallback MUST be deliberate, safe, observable, and tested. It MUST state what
quality is reduced and when normal behavior resumes. Silent stale data, silent
partial writes, and “success” after an unrecorded failure are prohibited.

## 10. Outbound HTTP and provider adapters

### NET-001 — Client lifecycle

Outbound HTTP MUST use typed clients or `IHttpClientFactory`. Code MUST NOT
construct a new `HttpClient` per call or mutate shared default headers for
request-specific state.

### NET-002 — Request contract

Every call defines method, base address, relative path, encoding, content type,
authentication, timeout, cancellation, success criteria, and maximum response
size as applicable. URI components MUST be encoded correctly.

### NET-003 — Resilience

Retries are allowed only for transient failures and idempotent or safely keyed
operations. Attempts and total duration are bounded. Timeout, retry, and circuit
behavior MUST be observable and MUST respect caller cancellation.

### NET-004 — Response handling

Responses and content streams MUST be disposed according to ownership.
Successful status alone is not sufficient when the body has a schema; required
fields MUST be validated. Error bodies MUST be bounded and sanitized before
diagnostic use.

### NET-005 — Provider neutrality

Provider adapters return application contracts and stable failure categories.
Provider selection and configuration remain in the composition root.

## 11. Resource and storage ownership

### RES-001 — Ownership

The creator owns a disposable resource unless ownership is explicitly
transferred. A method MUST NOT dispose a caller-owned stream. Returned streams
MUST document who disposes them and what lifetime they retain.

### RES-002 — Streaming

Large objects and attachments SHOULD be streamed. Buffering requires an
explicit size bound. Storage writes that replace an existing object SHOULD be
atomic or use a documented partial-write recovery strategy.

### RES-003 — Storage keys and URIs

Provider-neutral storage keys MUST be validated independently from provider
URIs. Persisted URI formats are compatibility contracts. Containers/buckets and
resource names MUST be configured, not inferred from untrusted input.

## 12. Observability and health

### OBS-001 — Structured logging

Logs MUST use message templates and named properties. They SHOULD record the
operation, stable resource identifiers, outcome, duration, and correlation
context. Secrets, tokens, full connection strings, raw personal data, and
unbounded payloads MUST NOT be logged.

### OBS-002 — Logging ownership

An exception SHOULD be logged once at the layer that has enough context to act
or terminate the operation. Lower layers either translate or propagate it.

### OBS-003 — Metrics and traces

Metrics measure outcomes, latency, throughput, and saturation. Metric labels
MUST be bounded-cardinality. Distributed traces SHOULD cross inbound, outbound,
database, messaging, and background-work boundaries with safe context.

### OBS-004 — Liveness and readiness

Liveness answers whether the process can continue running and MUST NOT depend
on optional or remote systems. Readiness answers whether the instance can
safely receive traffic and MAY check required dependencies with strict time
bounds. Degraded optional dependencies SHOULD have separate health reporting.

### OBS-005 — Actionability

Every production alert or health failure SHOULD identify the affected
capability and recovery action. Repeated expected failures SHOULD be aggregated
or sampled without hiding occurrence counts.

## 13. Configuration and host boundaries

### CFG-001 — Validated options

Configuration consumed by production code MUST use typed options or an
equivalent validated contract. Missing or invalid production-critical values
MUST fail before traffic is accepted.

### CFG-002 — Secrets

Real credentials MUST NOT be committed, logged, embedded in fixtures, placed in
exception messages, or copied into generated artifacts. Checked-in settings MAY
document key shape with safe placeholders.

### CFG-003 — Trusted network metadata

Forwarded headers, public origins, CORS, proxy networks, and allowed hosts MUST
use explicit deployment allowlists. Host and scheme derived from untrusted
requests MUST NOT be used to generate security-sensitive links.

### CFG-004 — Host pipeline

Middleware order is behavioral. Forwarded headers, exception handling,
redirects, routing, rate limiting, authentication, authorization, antiforgery,
and endpoints MUST be ordered deliberately and covered by host tests.

### CFG-005 — Environment parity

Development defaults MUST NOT silently weaken production validation. Every
environment-specific difference SHOULD be documented and testable.

## 14. Testing standards

### TST-001 — Executable proof first

A behavioral change starts with a failing test or equivalent executable proof.
The same focused proof passes after the implementation. Documentation-only or
mechanical changes record why behavioral TDD is inapplicable.

### TST-002 — Test layers

Use the cheapest test that proves the real risk:

| Risk | Preferred evidence |
| --- | --- |
| Domain rule | Unit test with no infrastructure |
| Application orchestration | Use-case test through ports |
| Provider translation | Adapter contract test with realistic provider behavior |
| ORM/SQL translation | Provider-backed or translation test |
| Dependency direction | Architecture guard plus mutation test |
| Host pipeline/configuration | In-process integration/smoke test |
| Critical user journey | Small end-to-end test |

### TST-003 — Test behavior

Tests MUST assert observable behavior and invariants, not private
implementation. Mocks belong at true external boundaries. Framework/provider
behavior that caused or could cause the defect MUST be exercised realistically.

### TST-004 — Determinism

Tests MUST control time, randomness, network, environment, and state. They MUST
NOT depend on execution order, production credentials, arbitrary sleeps, or an
unbounded external service.

### TST-005 — Naming and framework

Test names describe operation, scenario, and expected outcome. One test project
uses one framework consistently. Existing module-level MSTest/xUnit choices MAY
remain; a new project follows its module unless a separate migration decision
selects one repository-wide framework.

### TST-006 — Coverage

Coverage is evidence, not a target to game. Changed business branches, defect
paths, cancellation, and failure translation require meaningful coverage.
Generated and vendored exclusions MUST be narrow and documented.

### TST-007 — Guard validity

A new architecture, source, SQL, or configuration guard MUST be proven to fail
for a representative mutation. A guard that only passes is not accepted as
evidence.

## 15. Build, analyzers, and dependencies

### BLD-001 — Common baseline

Root formatting, analyzer, nullability, deterministic-build, and language
settings are authoritative. Module settings MAY strengthen them or encode a
documented domain requirement; they MUST NOT silently disable repository rules.

### BLD-002 — Warnings

New warnings are prohibited. Existing warnings MUST be baselined by rule and
scope, not copied into an ever-growing global suppression list. Once a baseline
category reaches zero, that rule becomes an unconditional build blocker.

### BLD-003 — Package declarations

Direct dependencies MUST be declared directly. Code MUST NOT rely on a
transitive package as its contract. Unused dependencies are removed under build
and test protection.

### BLD-004 — Package versions

.NET package versions SHOULD converge on repository-root central management.
An independently versioned/deployed product MAY own a separate policy only with
a documented boundary. Lock-file policy MUST be consistent within that
dependency graph and validated in CI.

### BLD-005 — Generated and vendored code

Generated, migration, and vendored code MAY have narrowly scoped exclusions.
The exclusion MUST identify its source and MUST NOT weaken hand-written code.

### BLD-006 — Reproducibility

Builds and restores SHOULD be deterministic and pinned. Generated output,
dependency caches, local secrets, test results, and code indexes MUST remain
outside source control.

## 16. Documentation and comments

### DOC-001 — Explain why

Comments explain intent, constraints, compatibility, or non-obvious trade-offs.
They MUST NOT narrate obvious syntax or duplicate a meaningful name.

### DOC-002 — No source archaeology

Dated file headers, “major updates” histories, author logs, and commented-out
code are prohibited in hand-written source. Git owns history. Existing headers
are removed when their logical unit is remediated unless they carry a current
legal requirement.

### DOC-003 — Work markers

`TODO`, `FIXME`, and `HACK` require an owner or tracking reference, a concrete
condition, and enough context to act. Otherwise the work is completed,
documented in the remediation backlog, or removed.

### DOC-004 — Operational changes

New configuration, migrations, external dependencies, health behavior, or
failure modes MUST update the corresponding operator/developer documentation in
the same change.

### DOC-005 — Decisions and exceptions

Durable architecture decisions and compatibility exceptions belong in
authoritative documentation. They MUST be linked from the affected code or test
when a future reviewer could reasonably “fix” the intentional behavior.

## 17. Code analysis and review

### REV-001 — Evidence-backed findings

Every reportable finding contains:

1. rule ID and severity;
2. exact file/symbol/line evidence;
3. observed or plausible consequence;
4. affected architectural boundary;
5. concrete remediation;
6. verification method; and
7. disposition: fix now, planned, accepted exception, or false positive.

“Looks inconsistent” without a rule and example is not a finding.

### REV-002 — Severity

| Severity | Definition |
| --- | --- |
| Blocker | Unsafe release, data corruption, or broken required boundary |
| High | Likely production defect, major coupling, or absent critical verification |
| Medium | Material reliability, maintainability, or consistency debt |
| Low | Local clarity or minor consistency issue |

Security findings use the dedicated security workflow; this standard does not
replace it.

### REV-003 — Review scope

Every remediation review checks applicable rules for architecture, naming,
async/cancellation, errors, data/time, HTTP, resource ownership, observability,
configuration, compatibility, tests, documentation, and baseline delta.

### REV-004 — Dead and unreachable code

Removal requires evidence from references, composition, tests, reflection and
serialization considerations, and external compatibility where applicable.
Compiler reachability alone is insufficient. Deletion is build/test guarded.

### REV-005 — Definition of done

A remediation unit is complete only when:

- focused tests and relevant aggregate tests pass;
- formatter, compiler, analyzer, lint, and architecture gates pass or have
  scoped recorded exceptions;
- `git diff --check` passes;
- no new warnings or baseline increases exist;
- documentation and configuration contracts are updated;
- the remediation plan records evidence and remaining debt; and
- the change has been reviewed against this standard.

## 18. Remediation application checklist

Every R1–R11 remediation phase MUST begin by listing its applicable rule IDs and
end with the following evidence:

- [ ] Dependency direction and project boundary reviewed.
- [ ] Naming and logical-unit style compliant.
- [ ] Async, cancellation, concurrency, and resource ownership reviewed.
- [ ] Error and provider-failure translation reviewed.
- [ ] Database, UTC/local-date, serialization, and compatibility semantics reviewed.
- [ ] HTTP, configuration, trusted-proxy, and secret handling reviewed.
- [ ] Logs, metrics, traces, liveness, and readiness reviewed where applicable.
- [ ] Focused RED/GREEN or documented TDD inapplicability recorded.
- [ ] Relevant integration, architecture, and aggregate gates passed.
- [ ] New-warning and baseline-delta checks passed.
- [ ] Documentation, exception register, and remediation status updated.

## 19. Relationship to existing module guidance

Existing Portal date/database, transaction, configuration, and release rules
remain valid module-specific requirements. Existing monitor/shared-library
provider isolation remains valid. Where a module `.editorconfig`, build file, or
test convention differs from this standard, ratcheted implementation resolves
the difference without making untouched legacy debt an unrelated blocker.

The root configuration, machine-readable baseline and exception formats,
guard scripts, and migration order are implemented. Reviewers still apply this
document to architectural and behavioral rules that are not mechanically
detectable. The executable workflow, baseline lifecycle, exception procedure,
and Ratchet-to-Strict promotion gate are documented in the
[engineering standards enforcement guide](engineering-standards-enforcement.md).

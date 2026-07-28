# Portal Lint Modernization Design

**Date:** 2026-07-28
**Status:** Implemented, verified, and reviewed; ready for draft publication
**Decision owner:** RVT Portal product owner
**Implementation branch:** `codex/portal-lint-modernization`

## Purpose

Adopt the complete ESLint 10 core recommended profile and the complete React
Hooks 7 recommended profile in the Portal client. Resolve every newly exposed
error structurally while preserving user-visible behavior, API contracts,
request cancellation, routing, and error handling.

No new `eslint-disable` comments, rule-specific suppressions, severity
downgrades, or configuration exceptions are permitted.

## Current baseline

The Portal client uses Node 24, ESLint 10.8.0, `@eslint/js` 10.0.1,
TypeScript-ESLint 8.65.0, React Hooks 7.1.1, and React Refresh 0.5.3.

The dependency-remediation change intentionally kept these ESLint 10 core
rules disabled:

- `no-unassigned-vars`;
- `no-useless-assignment`; and
- `preserve-caught-error`.

It also retained only the React Hooks 5-era `rules-of-hooks` and
`exhaustive-deps` rules instead of the complete React Hooks 7 recommended
profile.

A disposable full-profile diagnostic run reports 38 findings across 14 files:

- 31 `react-hooks/set-state-in-effect` errors;
- 3 `react-hooks/refs` errors;
- 1 `react-hooks/static-components` error;
- 1 `preserve-caught-error` error; and
- the same 2 existing `react-refresh/only-export-components` warnings.

The other newly enabled ESLint 10 and React Hooks 7 recommended rules produce
no current diagnostics.

## Approved approach

Implement the modernization by rule family rather than by source file.

1. Enable and fix the focused non-effect rules:
   `preserve-caught-error`, `react-hooks/static-components`, and
   `react-hooks/refs`.
2. Refactor synchronous effect-driven state transitions by behavioral pattern:
   request loading, derived/reset state, and prop-to-form initialization.
3. Enable the complete recommended profiles and remove the temporary
   behavior-neutral overrides.
4. Run the complete client and repository verification gates.

Each behavioral batch must be independently testable and committed. This keeps
regressions attributable without introducing a generalized data-fetching
framework.

## Alternatives considered

### Rule-family refactoring — approved

This groups structurally similar defects, permits focused tests, and makes each
commit reviewable. It enforces the complete profile without coupling unrelated
files in one undifferentiated rewrite.

### General-purpose async-state hook — rejected

A shared fetching hook could remove many effect findings quickly, but it would
introduce a new application abstraction and change numerous call sites at
once. The current scope does not require a new fetching framework.

### File-by-file modernization — rejected

Completing all rule families in one file before moving to the next is simple to
schedule, but it mixes unrelated behavior and makes regressions harder to
isolate.

## Lint configuration

`apps/portal/RvtPortal.Client/eslint.config.js` will:

- retain `js.configs.recommended`;
- retain the TypeScript-ESLint recommended configuration;
- apply the complete current
  `reactHooks.configs.recommended.rules` profile to TypeScript and TSX files;
- remove the explicit `off` overrides for `no-unassigned-vars`,
  `no-useless-assignment`, and `preserve-caught-error`; and
- retain the existing React Refresh warning configuration and naming
  conventions.

The final configuration must not contain exceptions introduced solely to make
the modernization pass.

## Focused rule corrections

### Caught-error preservation

When `src/api/client.ts` converts a caught parsing or transport error into a
new domain-facing error, the new error will retain the original error as
`Error.cause`. The existing public message remains unchanged.

### Stable component selection

`src/components/FormControls.tsx` will stop creating or selecting a component
type during `Notice` render. Icon selection will use stable module-scope
components or stable conditional JSX. Notice tone, role, icon, and message
remain unchanged.

### Ref lifecycle

`src/components/MonitorMap.tsx` will not read or write `.current` during
render. Leaflet marker synchronization will occur after commit, and event
handlers will read the latest committed marker collection. Marker identity,
popup behavior, map fitting, and cleanup remain unchanged.

## Effect-state architecture

### Request loading

Effects may start and cancel external I/O, but they will not synchronously
publish loading state. Request-driven panels will store the identity of the
last completed request with its response or error. The rendered request
identity is derived from the current query inputs.

Loading is derived when the current request identity differs from the last
completed identity. Promise completion callbacks publish success or failure
state asynchronously. Abort handling remains silent.

This pattern applies without changing request URLs or response mapping in:

- company and user administration;
- Help administration and Help browsing;
- contracts, sites, and site assignments;
- dashboards, map/calendar routes, and data views;
- monitors;
- notifications and alert levels; and
- reports, report rules, and report recipients.

### Derived and reset state

State that can be computed from current inputs will be derived during render
instead of corrected by an effect. This includes:

- empty suggestions when search text is below the lookup threshold;
- the valid monitor tab when the requested tab is unavailable;
- route-scoped notices and shell errors;
- empty trace detail when trace mode or selection is absent; and
- notification-setting drafts that have no user override.

User edits remain local state. Derived defaults must not overwrite an active
user edit.

### Input-based form initialization

Forms currently copying loaded props into local state from an effect will use
a keyed child-state boundary or initialize editable state from the loaded
record. A record identity change creates a fresh editing session; rerenders of
the same record do not discard unsaved edits.

### Confirmation-route validation

Missing confirmation parameters are derived from the current URL and rendered
immediately. The confirmation effect runs only for valid parameters and
publishes only asynchronous completion state.

## Error and cancellation behavior

- Existing user-facing error messages and notice placement remain unchanged.
- Existing `AbortController` cancellation remains in every request effect.
- Aborted requests do not surface an error or overwrite a newer response.
- Real failures continue through the existing panel or shell error path.
- Newly wrapped errors preserve their original cause.
- No retry, caching, polling, or request-deduplication behavior is added.

## Test strategy

Every behavioral refactor follows red-green-refactor:

1. add or strengthen a focused regression test;
2. run it and confirm the expected failure;
3. implement the minimum structural correction;
4. rerun the focused test and related file suite; and
5. commit the independently passing batch.

Focused coverage will verify:

- wrapped errors retain their cause without changing the public message;
- `Notice` renders the same icon, role, and text;
- `MonitorMap` event handlers use the latest committed markers;
- request panels retain loading, success, failure, and abort behavior;
- short searches display no stale suggestions;
- route and selection changes clear only the state that belongs to the prior
  identity;
- editable forms reset for a new record but retain unsaved edits for the same
  record; and
- invalid confirmation URLs render their existing validation message without
  issuing a confirmation request.

## Completion gates

The modernization is complete only when:

- the full ESLint 10 core recommended profile is enabled;
- the full React Hooks 7 recommended profile is enabled;
- lint reports zero errors and no new warnings;
- the two existing Fast Refresh warnings remain unchanged;
- no new lint suppression or severity downgrade exists;
- all 78 existing Vitest tests and all new regression tests pass;
- the Vite production build passes under Node 24;
- `npm audit` reports zero vulnerabilities at every severity;
- the changed-range engineering-standards ratchet passes;
- both workflow contract tests pass; and
- `git diff --check` passes.

## Scope boundaries

This phase does not change:

- backend code, database schemas, migrations, or API schemas;
- npm dependency versions or Node 24 requirements;
- visible UI copy or navigation;
- Help Admin/R2 release readiness or its operator audit requirement;
- the two existing Fast Refresh warnings; or
- the unrelated local Portal launch and Windows SPA-command settings preserved
  in the primary `main` worktree.

# OpenAPI Client And Request Cancellation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve H9 and H10 by making generated OpenAPI schema types the SPA contract source and preventing stale client fetch responses from overwriting newer state.

**Architecture:** Keep the existing `dtos.ts` import surface so screen components do not churn, but replace hand-written response/item/request-body DTOs with aliases derived from `src/api/schema.d.ts`. Add `AbortSignal` support at the shared API helper boundary, then wire cleanup into high-churn list and lookup effects.

**Tech Stack:** React 19, TypeScript strict mode, Vite, Vitest, openapi-typescript generated schema.

---

### Task 1: Track Work In Plane

**Files:**
- External: Plane project `1eff77df-acf1-4f43-a8b7-ce257cc2a10a`.

- [x] **Step 1: Create H9 and H10 Plane items**

Create `[H9] OpenAPI-generated SPA contracts` and `[H10] Abort stale SPA fetch requests`.

- [x] **Step 2: Move items to Done after verification**

Add the commands and pass/fail evidence after implementation.

### Task 2: Request Cancellation Guardrails

**Files:**
- Modify: `RvtPortal.Client/src/api/client.test.ts`
- Modify: `RvtPortal.Client/src/App.test.tsx`

- [x] **Step 1: Add API signal propagation red test**

Add a test that calls a query helper with an `AbortSignal` and asserts the same signal reaches `fetch`.

- [x] **Step 2: Add stale monitor response red test**

Add an app test where the first monitor-list request is held, a newer search request resolves first, and the old response must not render stale rows.

### Task 3: API Cancellation Implementation

**Files:**
- Modify: `RvtPortal.Client/src/api/client.ts`

- [x] **Step 1: Add `ApiRequestOptions` and `isAbortError`**

Expose a tiny request options type carrying `signal?: AbortSignal` and make aborted fetches rethrow instead of becoming API-unavailable errors.

- [x] **Step 2: Thread options through high-churn query helpers**

Add optional request options to list/query/search helpers used from live search, list grids, dashboards, report recipients, notification lists, alert-level lists, and data views.

### Task 4: React Effect Cancellation

**Files:**
- Modify: `RvtPortal.Client/src/admin/AdminPanels.tsx`
- Modify: `RvtPortal.Client/src/operations/ContractSitePanels.tsx`
- Modify: `RvtPortal.Client/src/operations/DashboardPanels.tsx`
- Modify: `RvtPortal.Client/src/operations/DataViewPanels.tsx`
- Modify: `RvtPortal.Client/src/operations/MonitorPanels.tsx`
- Modify: `RvtPortal.Client/src/operations/NotificationAlertPanels.tsx`
- Modify: `RvtPortal.Client/src/operations/ReportPanels.tsx`

- [x] **Step 1: Add cleanup to list effects**

Create an `AbortController` inside each high-churn loading effect, pass its signal to the API call, ignore abort errors, avoid clearing loading state after abort, and abort on cleanup.

- [x] **Step 2: Add cleanup to live lookup suggestions**

Use the same pattern for company/user/site/contract lookup suggestions that re-run while typing.

### Task 5: OpenAPI Contract Source

**Files:**
- Modify: `RvtPortal.Client/src/dtos.ts`
- Modify: `RvtPortal.Client/src/api/openApiClient.ts`
- Modify: `RvtPortal.Client/src/api/client.test.ts`

- [x] **Step 1: Add a failing contract-source test**

Assert `dtos.ts` imports `./api/schema`, does not contain a manually declared `CompanyListItem` object body, and exports schema-derived aliases for representative DTOs.

- [x] **Step 2: Replace manual DTO bodies with schema aliases**

Keep local query request types and UI-only helper unions, but alias generated response/item/request-body contracts through `components['schemas']`.

- [x] **Step 3: Remove facade sentinels that depend on manual DTOs**

Keep `openApiClient.ts` as a schema-backed re-export facade without importing `../dtos` as a competing contract source.

### Task 6: Verification And Documentation

**Files:**
- Modify: `README.md` or `RvtPortal.Spa/AUTHORIZATION.md` only if client contract/cancellation behavior needs reader-facing notes.
- Modify: `project_state.md`.

- [x] **Step 1: Run focused red/green checks**

Run the focused Vitest tests for `client.test.ts` and the stale monitor app test.

- [x] **Step 2: Run client quality gates**

Run `npm run test:run -- src/api/client.test.ts src/App.test.tsx`, `npm run lint`, and `npm run build`.

- [x] **Step 3: Update state and Plane**

Record affected paths, verification, and known follow-ups in `project_state.md`, then move Plane H9/H10 items to Done with evidence.

**Implementation evidence - 2026-06-26**

- Added `ApiRequestOptions.signal` support at the shared client boundary and guarded stale responses after transport/body resolution.
- Wired `AbortController` cleanup through high-churn list/search/dashboard/report/data/help effects.
- Replaced the large manually maintained SPA DTO object declarations with `ApiSchema<'...'>` aliases against `src/api/schema.d.ts`, keeping local UI/query compatibility extensions where the generated schema is still narrower than runtime use.
- Updated `openApiClient.ts` so schema sentinels no longer import the DTO facade as a competing source of truth.
- Verification passed:
  - `npm run build`
  - `npm run test:run -- src/api/client.test.ts src/App.test.tsx` (`50/50`)
  - `npm run test:run` (`60/60`)
  - `npm run lint`
  - `git diff --check`

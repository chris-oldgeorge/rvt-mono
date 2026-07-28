# Portal Lint Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce the complete ESLint 10 core and React Hooks 7 recommended profiles in the Portal client with zero errors and no behavior-changing suppressions.

**Architecture:** Refactor by diagnostic family. Use a disposable full-profile ESLint overlay as the failing structural test until the production config can adopt the complete profiles; preserve behavior with focused Vitest coverage and request-identity state rather than a new generalized fetching abstraction.

**Tech Stack:** Node.js 24, React 19, TypeScript, ESLint 10.8.0, React Hooks ESLint plugin 7.1.1, Vitest 4.1.7, Vite 6.4.3.

## Global Constraints

- Enable the complete current `js.configs.recommended` and `reactHooks.configs.recommended.rules` profiles.
- Add no `eslint-disable` comments, rule suppressions, file exceptions, or severity downgrades.
- Preserve the two existing `react-refresh/only-export-components` warnings; add no warnings.
- Preserve API URLs, request cancellation, visible copy, navigation, loading/error behavior, and form-edit semantics.
- Add no runtime or development dependency and do not change the Node 24 requirement.
- Keep Help Admin/R2 release readiness conditional on its separate operator audit.
- Do not stage or modify the primary worktree's local `launchSettings.json` or `RvtPortal.Spa.csproj` edits.

## Disposable full-profile lint overlay

Task 1 creates this untracked scan-local file:

`/private/tmp/rvt-eslint-modernization.config.mjs`

```js
import baseConfig from '/Users/oldgeorge/Documents/rvt-mono/apps/portal/RvtPortal.Client/eslint.config.js';

const portalConfig = baseConfig.find((config) => config.plugins?.['react-hooks']);
const reactHooks = portalConfig.plugins['react-hooks'];

export default [
  ...baseConfig,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: {
      'react-hooks': reactHooks,
    },
    rules: {
      'no-unassigned-vars': 'error',
      'no-useless-assignment': 'error',
      'preserve-caught-error': 'error',
      ...reactHooks.configs.recommended.rules,
    },
  },
];
```

Run overlay checks from `apps/portal/RvtPortal.Client`. This file is diagnostic
only and must never be staged.

---

### Task 1: Preserve caught errors and stabilize reusable components

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/api/client.ts:176-191`
- Modify: `apps/portal/RvtPortal.Client/src/api/client.test.ts`
- Modify: `apps/portal/RvtPortal.Client/src/components/FormControls.tsx:6-24,132-141`
- Modify: `apps/portal/RvtPortal.Client/src/components/FormControls.test.tsx`
- Modify: `apps/portal/RvtPortal.Client/src/components/MonitorMap.tsx:19-31`
- Modify: `apps/portal/RvtPortal.Client/src/components/MonitorMap.test.tsx`

**Interfaces:**
- Consumes: existing `requestJson<T>`, `Notice`, and `MonitorMap` public contracts
- Produces: cause-preserving transport errors, stable notice icon rendering, and commit-phase marker-ref synchronization

- [ ] **Step 1: Create the disposable overlay and verify the focused lint test is red**

Create `/private/tmp/rvt-eslint-modernization.config.mjs` with the exact content
in the plan's overlay section, then run:

```bash
npx eslint \
  src/api/client.ts \
  src/components/FormControls.tsx \
  src/components/MonitorMap.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL with one `preserve-caught-error`, one
`react-hooks/static-components`, and three `react-hooks/refs` diagnostics.

- [ ] **Step 2: Add the transport-cause regression test**

Add to `src/api/client.test.ts`:

```ts
it('preserves the transport error when reporting an unavailable API', async () => {
  const transportError = new TypeError('connection refused');
  vi.stubGlobal('fetch', vi.fn(async () => {
    throw transportError;
  }));

  await expect(getHealth()).rejects.toMatchObject({
    message:
      'Unable to reach the RVT Portal API. Start RvtPortal.Spa on http://localhost:5178, or set VITE_RVT_PORTAL_API_URL to the API origin.',
    cause: transportError,
  });
});
```

Run:

```bash
npm run test:run -- src/api/client.test.ts
```

Expected: FAIL because the replacement `Error` has no `cause`.

- [ ] **Step 3: Preserve the caught error**

Change the non-abort branch in `requestJson<T>` to:

```ts
throw new Error(apiUnavailableMessage, { cause: error });
```

Run the focused API test again. Expected: PASS.

- [ ] **Step 4: Strengthen stable rendering coverage**

In `FormControls.test.tsx`, render success, error, and info notices and assert
their roles and messages:

```tsx
render(
  <>
    <Notice tone="success" message="Saved" />
    <Notice tone="error" message="Failed" />
    <Notice tone="info" message="Review" />
  </>,
);

expect(screen.getByText('Saved').closest('output')).not.toHaveAttribute('role');
expect(screen.getByText('Failed').closest('output')).toHaveAttribute('role', 'alert');
expect(screen.getByText('Review').closest('output')).not.toHaveAttribute('role');
```

In `MonitorMap.test.tsx`, add:

```tsx
it('rebuilds Leaflet with the latest committed marker content', async () => {
  const { rerender } = render(<MonitorMap markers={[markerFixture()]} />);
  await waitFor(() => expect(leafletMocks.mapSetView).toHaveBeenCalledTimes(1));

  rerender(
    <MonitorMap
      markers={[{ ...markerFixture(), latitude: 40.7128, longitude: -74.006 }]}
    />,
  );

  await waitFor(() =>
    expect(leafletMocks.mapSetView).toHaveBeenLastCalledWith([40.7128, -74.006], 13),
  );
});
```

Run:

```bash
npm run test:run -- \
  src/components/FormControls.test.tsx \
  src/components/MonitorMap.test.tsx
```

Expected: PASS as characterization coverage before the structural refactor.

- [ ] **Step 5: Replace render-created icon selection**

Replace `noticeIcon` and the render-local `Icon` with a stable component:

```tsx
function NoticeIcon({ tone }: Readonly<{ tone: NoticeTone }>) {
  if (tone === 'success') {
    return <CheckCircle2 size={18} aria-hidden="true" />;
  }
  if (tone === 'error') {
    return <AlertCircle size={18} aria-hidden="true" />;
  }
  return <HelpCircle size={18} aria-hidden="true" />;
}
```

Render `<NoticeIcon tone={tone} />` from `Notice` and remove the unused
`LucideIcon` import.

- [ ] **Step 6: Synchronize marker refs after commit**

Remove `leafletSignature`, keep `leafletMarkers`, remove the render-time
conditional, and add this effect before the Leaflet lifecycle effect:

```tsx
useEffect(() => {
  leafletMarkers.current = markers;
}, [markerSignature, markers]);
```

The Leaflet lifecycle effect continues to depend only on `markerSignature`,
which preserves the existing no-remount behavior for equivalent marker arrays.

- [ ] **Step 7: Verify and commit the focused rules**

Run:

```bash
npx eslint \
  src/api/client.ts \
  src/components/FormControls.tsx \
  src/components/MonitorMap.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- \
  src/api/client.test.ts \
  src/components/FormControls.test.tsx \
  src/components/MonitorMap.test.tsx
git diff --check
```

Expected: overlay lint has no findings for these files and all focused tests
pass.

```bash
git add \
  apps/portal/RvtPortal.Client/src/api/client.ts \
  apps/portal/RvtPortal.Client/src/api/client.test.ts \
  apps/portal/RvtPortal.Client/src/components/FormControls.tsx \
  apps/portal/RvtPortal.Client/src/components/FormControls.test.tsx \
  apps/portal/RvtPortal.Client/src/components/MonitorMap.tsx \
  apps/portal/RvtPortal.Client/src/components/MonitorMap.test.tsx
git commit -m "refactor: satisfy focused Portal lint rules"
```

### Task 2: Remove App effect-driven resets and prop copying

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/App.tsx:887-915,1015-1071,1387-1410`
- Modify: `apps/portal/RvtPortal.Client/src/App.test.tsx`

**Interfaces:**
- Consumes: `ConfirmEmailPage`, `PortalShell`, `ProfileForm`, existing auth/profile API functions
- Produces: URL-derived confirmation validation, route-owned shell errors, and keyed profile editing sessions

- [ ] **Step 1: Verify the App structural test is red**

```bash
npx eslint src/App.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL with three `react-hooks/set-state-in-effect` diagnostics.

- [ ] **Step 2: Add App regressions**

Add focused tests to `App.test.tsx` using the existing `stubFetch`,
`fetchedUrls`, `adminUser`, and render helpers:

```ts
it('rejects an incomplete confirmation URL without issuing a confirmation request', async () => {
  globalThis.history.replaceState(null, '', '/confirm-email?userId=user-id');
  stubFetch({ auth: { isAuthenticated: false, user: null } });

  render(<App />);

  expect(
    await screen.findByText('A user and confirmation code must be supplied.'),
  ).toBeInTheDocument();
  expect(
    fetchedUrls().some((url) => url.pathname === '/api/auth/confirm-email'),
  ).toBe(false);
});
```

Add this profile characterization test:

```ts
it('initializes profile fields and keeps local edits user-owned', async () => {
  globalThis.history.replaceState(null, '', '/profile');
  stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

  render(<App />);

  const name = await screen.findByLabelText(/^name$/i);
  expect(name).toHaveValue('Admin User');
  fireEvent.change(name, { target: { value: 'Edited locally' } });
  expect(name).toHaveValue('Edited locally');
});
```

Run the focused tests. The confirmation behavior is characterization coverage;
the overlay lint failure remains the red structural test.

- [ ] **Step 3: Derive confirmation input and initial state**

Read `userId` and `code` before hooks. Initialize:

```ts
const parameterError =
  !userId || !code
    ? 'A user and confirmation code must be supplied.'
    : null;
const confirmationCode = code;
const [message, setMessage] = useState(parameterError ? '' : 'Confirming email');
const [error, setError] = useState<string | null>(parameterError);
```

The effect returns immediately for invalid parameters without setting state.
For valid parameters it calls `confirmEmail` and only promise callbacks update
state.

- [ ] **Step 4: Scope shell errors to their route**

Replace the string state with:

```ts
const [shellError, setShellError] = useState<{
  route: ProtectedRoute;
  message: string;
} | null>(null);
const visibleShellError =
  shellError?.route === route ? shellError.message : null;
```

Store `{ route, message }` in both forbidden and ordinary shell-error paths,
render `visibleShellError`, and remove the route-clearing effect. Include
`route` in callback dependency arrays.

- [ ] **Step 5: Key and initialize profile editing sessions**

Build a stable key from the loaded profile fields at the `ProfileForm` call:

```ts
const profileFormKey = profile
  ? [profile.id, profile.email, profile.name, profile.mobilePhone, profile.companyRole].join('|')
  : 'profile-loading';
```

Render `<ProfileForm key={profileFormKey} ... />`. Initialize `ProfileForm`
state directly from `profile`:

```ts
const [email, setEmail] = useState(profile?.email ?? '');
const [name, setName] = useState(profile?.name ?? '');
const [mobilePhone, setMobilePhone] = useState(profile?.mobilePhone ?? '');
const [companyRole, setCompanyRole] = useState(profile?.companyRole ?? '');
```

Remove the prop-copying effect.

- [ ] **Step 6: Verify and commit App state ownership**

```bash
npx eslint src/App.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- src/App.test.tsx
git diff --check
```

Expected: no overlay diagnostics in `App.tsx`; App tests pass.

```bash
git add \
  apps/portal/RvtPortal.Client/src/App.tsx \
  apps/portal/RvtPortal.Client/src/App.test.tsx
git commit -m "refactor: derive Portal shell and form state"
```

### Task 3: Modernize administration and Help request effects

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/admin/AdminPanels.tsx:107-192,424-509`
- Modify: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx:80-124`
- Modify: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.tsx`
- Modify: `apps/portal/RvtPortal.Client/src/operations/HelpPanel.tsx:28-125`
- Modify: `apps/portal/RvtPortal.Client/src/App.test.tsx`

**Interfaces:**
- Consumes: company/user lookup APIs, Help admin query, public Help query/article APIs
- Produces: request-identity loading and query-owned suggestions with unchanged abort/error behavior

- [ ] **Step 1: Verify the administration/Help structural test is red**

```bash
npx eslint \
  src/admin/AdminPanels.tsx \
  src/admin/HelpAdminPanel.tsx \
  src/operations/HelpPanel.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL with seven `react-hooks/set-state-in-effect` diagnostics.

- [ ] **Step 2: Add stale-state and abort regressions**

In `App.test.tsx`, extend the existing company live-suggestion test:

```ts
fireEvent.change(screen.getByPlaceholderText(/search companies/i), {
  target: { value: 'acme' },
});
await screen.findByText('Acme Environmental');
fireEvent.change(screen.getByPlaceholderText(/search companies/i), {
  target: { value: 'a' },
});
expect(screen.queryByText('Acme Environmental')).not.toBeInTheDocument();
```

Run the existing deferred-refresh and filter/focus tests in
`HelpAdminPanel.test.tsx`; they already protect newer Help results from stale
refreshes.

- [ ] **Step 3: Derive list loading from request identity**

For company and user lists, replace `isLoading` state with a completed request
key:

```ts
const requestKey = JSON.stringify(query);
const [completedRequestKey, setCompletedRequestKey] = useState<string | null>(null);
const isLoading = completedRequestKey !== requestKey;
```

On each non-aborted success or failure, set `completedRequestKey(requestKey)`.
Remove synchronous `setIsLoading(true)` and the `finally` loading reset.

Apply the same local pattern to Help overview/article loading, using
`searchText` and `slug` as request keys.

- [ ] **Step 4: Make suggestions query-owned**

Replace raw `suggestions` with:

```ts
const [suggestionResult, setSuggestionResult] = useState<{
  query: string;
  results: string[];
}>({ query: '', results: [] });
const suggestions =
  searchText.length >= 2 && suggestionResult.query === searchText
    ? suggestionResult.results
    : [];
```

For short searches, return from the effect without setting state. Promise
callbacks publish `{ query: searchText, results }`; non-abort failures publish
an empty result for that query.

- [ ] **Step 5: Separate Help Admin initial loading from event refresh**

The mount/filter effect must call `queryAdminHelp` directly and publish results
only from promise callbacks. Keep an event-safe `loadArticles` function for
create/update/delete flows, but do not call a state-updating wrapper directly
from the effect.

Preserve focus-restoration behavior and stale-response protection already
covered by `HelpAdminPanel.test.tsx`.

- [ ] **Step 6: Verify and commit administration/Help effects**

```bash
npx eslint \
  src/admin/AdminPanels.tsx \
  src/admin/HelpAdminPanel.tsx \
  src/operations/HelpPanel.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- \
  src/App.test.tsx \
  src/admin/HelpAdminPanel.test.tsx
git diff --check
```

Expected: no overlay errors in the three source files; focused tests pass.

```bash
git add \
  apps/portal/RvtPortal.Client/src/admin/AdminPanels.tsx \
  apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx \
  apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.tsx \
  apps/portal/RvtPortal.Client/src/operations/HelpPanel.tsx \
  apps/portal/RvtPortal.Client/src/App.test.tsx
git commit -m "refactor: modernize Portal administration effects"
```

### Task 4: Modernize dashboard, calendar, and data-view effects

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/operations/DashboardPanels.tsx:60-90,170-222,271-302`
- Modify: `apps/portal/RvtPortal.Client/src/operations/DashboardRoutePanels.tsx:31-65,100-175`
- Modify: `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.tsx:100-205`
- Modify: `apps/portal/RvtPortal.Client/src/App.test.tsx`

**Interfaces:**
- Consumes: dashboard summary, site search, breach/alert, map, calendar, and data APIs
- Produces: query-owned loading plus selection-owned calendar and trace details

- [ ] **Step 1: Verify the dashboard/data structural test is red**

```bash
npx eslint \
  src/operations/DashboardPanels.tsx \
  src/operations/DashboardRoutePanels.tsx \
  src/operations/DataViewPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL with eight `react-hooks/set-state-in-effect` errors and the two
pre-existing Fast Refresh warnings.

- [ ] **Step 2: Run the existing dashboard/data characterization suite**

```bash
npm run test:run -- \
  src/App.test.tsx \
  src/operations/DataViewPanels.test.tsx
```

Expected: PASS. These tests already cover dashboard summary, live site search,
calendar rendering, data-view rendering, UTC formatting, and API timestamp
conversion. The overlay lint failure remains the red structural test.

- [ ] **Step 3: Apply request-identity loading**

Use local request keys and completed keys for:

- dashboard summary;
- dashboard site search;
- breaches/alerts by date;
- map summary and markers;
- calendar month; and
- data-view grid requests.

Each non-aborted success or failure records its request key. Remove
synchronous loading updates and `finally` loading state. Do not share a new
hook between panels.

- [ ] **Step 4: Make calendar day data selection-owned**

Store:

```ts
type CalendarDayResult = {
  selectedDate: string;
  data: CalendarDayResponse;
};
```

Derive visible day data only when the stored `selectedDate` equals the current
selection. If no month or date is selected, return from the effect without
setting state. The promise callback publishes the keyed result.

- [ ] **Step 5: Make trace detail selection-owned**

Store trace detail with the selected trace ID:

```ts
type TraceDetailResult = {
  traceId: string;
  item: TraceDetailResponse | null;
};
```

Derive visible detail only in trace mode and only when the stored ID matches
`selectedTraceId`. Remove the synchronous clear from the effect.

- [ ] **Step 6: Verify and commit dashboard/data effects**

```bash
npx eslint \
  src/operations/DashboardPanels.tsx \
  src/operations/DashboardRoutePanels.tsx \
  src/operations/DataViewPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- \
  src/App.test.tsx \
  src/operations/DataViewPanels.test.tsx
git diff --check
```

Expected: zero overlay errors, only the two established Fast Refresh warnings,
and passing focused tests.

```bash
git add \
  apps/portal/RvtPortal.Client/src/operations/DashboardPanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/DashboardRoutePanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/DataViewPanels.tsx \
  apps/portal/RvtPortal.Client/src/App.test.tsx
git commit -m "refactor: modernize Portal dashboard effects"
```

### Task 5: Modernize operational list loading

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.tsx:180-241,520-578`
- Modify: `apps/portal/RvtPortal.Client/src/operations/MonitorPanels.tsx:162-260,800-858`
- Modify: `apps/portal/RvtPortal.Client/src/operations/NotificationAlertPanels.tsx:103-199,480-537`
- Modify: `apps/portal/RvtPortal.Client/src/operations/ReportPanels.tsx:129-185,250-305`
- Modify: `apps/portal/RvtPortal.Client/src/App.test.tsx`

**Interfaces:**
- Consumes: existing list/filter/sort API request objects
- Produces: request-key loading for contracts, sites, monitors, notifications, alert levels, reports, and report rules

- [ ] **Step 1: Verify operational-list lint is red**

```bash
npx eslint \
  src/operations/ContractSitePanels.tsx \
  src/operations/MonitorPanels.tsx \
  src/operations/NotificationAlertPanels.tsx \
  src/operations/ReportPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL for synchronous list loading, invalid-tab correction, and
state-updating loader calls.

- [ ] **Step 2: Run operational-list characterization coverage**

```bash
npm run test:run -- src/App.test.tsx
```

Expected: PASS. The suite already covers contracts, sites, monitors,
notifications, reports, report rules, an older monitor request resolving after
a newer search, and a report-grid failure followed by a successful retry.

- [ ] **Step 3: Apply request-key loading to direct effects**

For contracts, sites, monitors, notifications, alert levels, reports, and
report rules:

```ts
const requestKey = JSON.stringify(query);
const [completedRequestKey, setCompletedRequestKey] = useState<string | null>(null);
const isLoading = completedRequestKey !== requestKey;
```

Record completion on non-aborted success or failure. Remove synchronous
`setIsLoading(true)` and loading `finally` blocks.

For loader functions used by button actions, accept an explicit
`showLoading: boolean` or keep event-owned busy state; the effect must call the
API promise directly rather than invoking a state-updating wrapper.

- [ ] **Step 4: Derive the effective monitor tab**

Replace the corrective effect with:

```ts
const effectiveState = tabs.some((tab) => tab.state === state)
  ? state
  : tabs[0].state;
```

Use `effectiveState` consistently in the query, URL, active tab, and rendered
state. Event handlers continue to store only valid tab values.

- [ ] **Step 5: Verify and commit operational list loading**

```bash
npx eslint \
  src/operations/ContractSitePanels.tsx \
  src/operations/MonitorPanels.tsx \
  src/operations/NotificationAlertPanels.tsx \
  src/operations/ReportPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- src/App.test.tsx
git diff --check
```

Expected: only the nested assignment/form diagnostics reserved for Task 6
remain in these files.

```bash
git add \
  apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/MonitorPanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/NotificationAlertPanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/ReportPanels.tsx
git commit -m "refactor: derive Portal list loading state"
```

### Task 6: Modernize nested assignments, drafts, and report forms

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.tsx:1154-1310`
- Modify: `apps/portal/RvtPortal.Client/src/operations/ReportPanels.tsx:110-126,393-470,630-710`
- Create: `apps/portal/RvtPortal.Client/src/operations/notificationDrafts.ts`
- Create: `apps/portal/RvtPortal.Client/src/operations/notificationDrafts.test.ts`

**Interfaces:**
- Consumes: site assignments/settings and report rule/recipient APIs
- Produces: direct promise effects, keyed report forms, and user-edit-owned notification drafts

- [ ] **Step 1: Verify nested-flow lint is red**

```bash
npx eslint \
  src/operations/ContractSitePanels.tsx \
  src/operations/ReportPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: FAIL at site-assignment loading, notification draft reset, report
form notice reset, and report-recipient loading.

- [ ] **Step 2: Write failing notification-draft tests**

Create `notificationDrafts.ts` with compile-safe, deliberately failing stubs:

```ts
import type {
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
} from '../dtos';

export type NotificationDraftOverrides = Record<
  string,
  SiteNotificationSettingMutationRequest
>;

export function notificationSettingDraft(
  _setting: SiteNotificationSettingItem,
  _overrides: NotificationDraftOverrides,
): SiteNotificationSettingMutationRequest {
  throw new Error('notificationSettingDraft is not implemented');
}

export function withoutNotificationDraft(
  _overrides: NotificationDraftOverrides,
  _siteUserId: string,
): NotificationDraftOverrides {
  throw new Error('withoutNotificationDraft is not implemented');
}
```

Create `notificationDrafts.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import type { SiteNotificationSettingItem } from '../dtos';
import {
  notificationSettingDraft,
  withoutNotificationDraft,
} from './notificationDrafts';

const setting = {
  siteUserId: 'site-user-1',
  email: true,
  sms: false,
  startTime: '08:00',
  endTime: '18:00',
} as SiteNotificationSettingItem;

describe('notification draft ownership', () => {
  it('prefers a user override without copying every server setting', () => {
    const override = {
      email: false,
      sms: true,
      startTime: '09:00',
      endTime: '17:00',
    };

    expect(
      notificationSettingDraft(setting, { [setting.siteUserId]: override }),
    ).toEqual(override);
    expect(notificationSettingDraft(setting, {})).toEqual({
      email: true,
      sms: false,
      startTime: '08:00',
      endTime: '18:00',
    });
  });

  it('removes only the successfully saved override', () => {
    const overrides = {
      'site-user-1': { email: false, sms: true, startTime: '', endTime: '' },
      'site-user-2': { email: true, sms: true, startTime: '', endTime: '' },
    };

    expect(withoutNotificationDraft(overrides, 'site-user-1')).toEqual({
      'site-user-2': overrides['site-user-2'],
    });
  });
});
```

Run:

```bash
npm run test:run -- src/operations/notificationDrafts.test.ts
```

Expected: FAIL with `notificationSettingDraft is not implemented`.

- [ ] **Step 3: Call assignment APIs directly from effects**

For site assignments and report recipients, effects call the underlying API
promises directly and update state only in promise callbacks. Keep separately
named event functions for mutation-triggered refreshes.

Do not call `loadAssignments` or `loadUsers` from an effect when those
functions also mutate state.

- [ ] **Step 4: Store only notification draft overrides**

Create `notificationDrafts.ts`:

```ts
import type {
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
} from '../dtos';

export type NotificationDraftOverrides = Record<
  string,
  SiteNotificationSettingMutationRequest
>;

export function notificationSettingDraft(
  setting: SiteNotificationSettingItem,
  overrides: NotificationDraftOverrides,
) {
  return overrides[setting.siteUserId] ?? {
    email: setting.email,
    sms: setting.sms,
    startTime: setting.startTime ?? '',
    endTime: setting.endTime ?? '',
  };
}

export function withoutNotificationDraft(
  overrides: NotificationDraftOverrides,
  siteUserId: string,
) {
  return Object.fromEntries(
    Object.entries(overrides).filter(([key]) => key !== siteUserId),
  );
}
```

Replace the prop-copying effect with a draft override map:

```ts
const [draftOverrides, setDraftOverrides] = useState<
  NotificationDraftOverrides
>({});

const draft = notificationSettingDraft(setting, draftOverrides);
```

On an edit, store only that row's override. After a successful save, remove
the saved override with `withoutNotificationDraft` so the updated server
setting becomes authoritative.

- [ ] **Step 5: Key report editing sessions**

At `ReportsPanel`, key each existing report-form branch:

```tsx
if (route.kind === 'new-rule') {
  return (
    <ReportRuleForm
      key="new-rule"
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
    />
  );
}
if (route.kind === 'edit-rule') {
  return (
    <ReportRuleForm
      key={route.ruleId}
      ruleId={route.ruleId}
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
    />
  );
}
```

Remove synchronous `setNotice(null)` from the load effect. Initial state is
already null, and a rule identity change remounts the form.

- [ ] **Step 6: Verify and commit nested state ownership**

```bash
npx eslint \
  src/operations/ContractSitePanels.tsx \
  src/operations/ReportPanels.tsx \
  --config /private/tmp/rvt-eslint-modernization.config.mjs
npm run test:run -- src/App.test.tsx
npm run test:run -- src/operations/notificationDrafts.test.ts
git diff --check
```

Expected: no overlay errors in either source file and passing regressions.

```bash
git add \
  apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/ReportPanels.tsx \
  apps/portal/RvtPortal.Client/src/operations/notificationDrafts.ts \
  apps/portal/RvtPortal.Client/src/operations/notificationDrafts.test.ts
git commit -m "refactor: modernize Portal nested state"
```

### Task 7: Enable the complete lint profiles

**Files:**
- Modify: `apps/portal/RvtPortal.Client/eslint.config.js:1-45`

**Interfaces:**
- Consumes: source changes from Tasks 1-6
- Produces: production lint enforcement of every current recommended core and React Hooks rule

- [ ] **Step 1: Verify the full overlay is green before changing production config**

```bash
npx eslint . --config /private/tmp/rvt-eslint-modernization.config.mjs
```

Expected: exit `0` with exactly the two existing
`react-refresh/only-export-components` warnings and no errors.

- [ ] **Step 2: Replace temporary policy preservation with full enforcement**

In the TypeScript/TSX rule object, replace the hand-selected Hooks rules with:

```js
...reactHooks.configs.recommended.rules,
```

Remove:

```js
'no-unassigned-vars': 'off',
'no-useless-assignment': 'off',
'preserve-caught-error': 'off',
```

Remove the obsolete comments describing the temporary behavior-neutral
dependency-remediation policy.

- [ ] **Step 3: Verify the production config and suppression contract**

```bash
npm run lint
git diff --unified=0 origin/main -- apps/portal/RvtPortal.Client | \
  rg '^\\+.*(eslint-disable|no-unassigned-vars.*off|no-useless-assignment.*off|preserve-caught-error.*off|react-hooks/.*(off|warn))'
```

Expected: lint exits `0` with exactly two Fast Refresh warnings. The `rg`
pipeline exits `1` because no prohibited additions exist.

- [ ] **Step 4: Run the complete client gate**

```bash
npm run test:run
npm run build
npm audit
```

Expected: every existing and new Vitest test passes, Vite production build
passes, and audit reports zero vulnerabilities.

- [ ] **Step 5: Commit full enforcement**

```bash
git add apps/portal/RvtPortal.Client/eslint.config.js
git commit -m "build: enforce modern Portal lint rules"
```

### Task 8: Verify repository policy and record the checkpoint

**Files:**
- Modify: `project_state.md`

**Interfaces:**
- Consumes: the fully verified lint modernization
- Produces: durable resume state and a reviewable branch

- [ ] **Step 1: Run final repository verification**

From the repository root:

```bash
scripts/verify-engineering-standards.sh --base origin/main --head HEAD
tests/verify-engineering-standards-workflow.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
git diff --check
git status --short
```

Expected: all policy checks pass. Only the harness-owned untracked `.codex/`
and `AGENTS.md` entries plus explicitly identified pre-existing unrelated
workspace changes may remain; none is staged.

- [ ] **Step 2: Record the authoritative state**

If `project_state.md` already contains an uncommitted checkpoint owned by
another task, do not stage a mixed state commit. Wait for that checkpoint to
be committed on its owning branch, merge or rebase the committed result, and
then add the lint checkpoint above it.

Add a top checkpoint to `project_state.md` containing:

- branch `codex/portal-lint-modernization`;
- design and plan paths;
- the original 36-error/2-warning diagnostic inventory;
- every enabled rule profile;
- the final exact Vitest total;
- lint, build, audit, engineering-standards, and workflow-contract results;
- confirmation that no suppression, dependency, backend, database, API, or
  Help Admin/R2 release-status change occurred; and
- next step: push a dedicated review branch and open a draft pull request.

- [ ] **Step 3: Verify and commit the checkpoint**

```bash
git diff --check
git add project_state.md
git commit -m "docs: record Portal lint modernization"
```

Expected: the commit contains only `project_state.md`.

- [ ] **Step 4: Push and open the review**

```bash
git push -u origin codex/portal-lint-modernization
gh pr create \
  --base main \
  --head codex/portal-lint-modernization \
  --draft \
  --title "Modernize Portal lint enforcement" \
  --body-file /private/tmp/rvt-portal-lint-modernization-pr.md
```

The PR body must summarize the structural rule families, test totals, exact
two-warning lint result, zero-vulnerability audit, repository policy results,
and unchanged Help Admin/R2 release condition.

Expected: GitHub returns a draft PR URL and starts the Engineering Standards
check for the branch.

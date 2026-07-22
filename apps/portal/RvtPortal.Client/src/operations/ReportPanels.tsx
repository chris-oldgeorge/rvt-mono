// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for report list, rule list, and recipient requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for report-rule edit and recipient forms.
// - 2026-06-26 pending Warned on archived report-rule sites and blocked saving until an active site is selected.
// - 2026-06-25 pending Added Daily report rule support in the Report Rule Wizard.
// - 2026-06-24 pending Added report-rule generation, setup wizard, recipient grids, and guideline hooks.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  BookOpen,
  ChevronLeft,
  Download,
  Edit3,
  ListChecks,
  PlayCircle,
  Plus,
  Save,
  Search,
  Trash2,
  UserPlus,
  UserRound,
  UsersRound
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  addReportRuleUser,
  createReportRule,
  deleteReportRule,
  getReportRule,
  getReportRuleOptions,
  getReportRuleUsers,
  isAbortError,
  queryReportRuleAssignedUsers,
  queryReportRuleAvailableUsers,
  queryReportRules,
  queryReports,
  removeReportRuleUser,
  requestReportRuleGeneration,
  updateReportRule
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import type {
  QueryCompaniesRequest,
  QueryReportRulesRequest,
  QueryReportRuleUsersResponse,
  ReportAlertRuleGuidelineItem,
  ReportGenerationRequestResponse,
  ReportListItem,
  ReportRuleListItem,
  ReportRuleMutationRequest,
  ReportRuleOptionsResponse,
  ReportUserAssignmentResponse,
  SortDirection,
  UserListItem
} from '../dtos';

const pageSize = 10;
const dailyFrequency = 1;
const weeklyFrequency = 2;
const monthlyFrequency = 3;
const weeklyAndMonthlyFrequency = 4;
const monday = 1;

type ReportsPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type ReportsRoute =
  | { kind: 'reports' }
  | { kind: 'rules' }
  | { kind: 'new-rule' }
  | { kind: 'edit-rule'; ruleId: string }
  | { kind: 'rule-users'; ruleId: string };

type ReportRuleAssignmentContext = Pick<
  ReportUserAssignmentResponse,
  'reportRuleId' | 'siteId' | 'siteName' | 'companyId' | 'companyName'
>;

type UserGridState = Readonly<{
  rows: UserListItem[];
  total: number;
  totalPages: number;
  error: string | null;
  isLoading: boolean;
}>;

// Function summary: Applies grid sort handler to the current configuration.
function useGridSortHandler(
  setSortKey: (key: string) => void,
  setSortDir: (direction: SortDirection) => void,
  setPage: (page: number) => void
) {
  return useCallback((key: string, direction: GridSortDirection) => {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }, [setPage, setSortDir, setSortKey]);
}

// Function summary: Renders the ReportsPanel React component and wires its local UI behavior.
export function ReportsPanel({ locationPath, onNavigate, onRequestError }: ReportsPanelProps) {
  const route = parseReportsRoute(locationPath);
  if (route.kind === 'rules') {
    return <ReportRulesListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (route.kind === 'new-rule') {
    return <ReportRuleForm locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (route.kind === 'edit-rule') {
    return <ReportRuleForm ruleId={route.ruleId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (route.kind === 'rule-users') {
    return <ReportRuleUsersPanel ruleId={route.ruleId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }

  return <ReportsListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the ReportsListPanel React component and wires its local UI behavior.
function ReportsListPanel({ locationPath, onNavigate, onRequestError }: ReportsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [reports, setReports] = useState<ReportListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'reportDate');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir'), 'Descending'));
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const query = useMemo<QueryCompaniesRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const returnPath = currentRoutePath(locationPath);

  const columns = useMemo<DataGridColumn<ReportListItem>[]>(() => [
    { key: 'reportName', header: 'Report', sortable: true, render: (report) => report.reportName || 'Scheduled Report' },
    { key: 'reportDate', header: 'Generated', sortable: true, render: (report) => formatDateTime(report.reportDate) },
    { key: 'period', header: 'Period', render: (report) => formatPeriod(report.reportFrom, report.reportTo) },
    { key: 'frequency', header: 'Frequency', sortable: true, render: (report) => report.frequencyLabel },
    { key: 'siteName', header: 'Site', sortable: true, render: (report) => report.siteName },
    { key: 'contracts', header: 'Contracts', sortable: true, render: (report) => report.contracts || 'None' }
  ], []);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildReportsUrl({ searchText, page, sort: sortKey, sortDir }));
    setIsLoading(true);
    queryReports(query, { signal: controller.signal })
      .then((response) => {
        setReports(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [onRequestError, page, query, searchText, sortDir, sortKey]);

  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Reports</p>
          <h2>Generated Reports</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/reports/rules', returnPath))}>
            <ListChecks size={17} aria-hidden="true" />
            <span>Report Rules</span>
          </button>
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/reports/rules/new', returnPath))}>
            <Plus size={17} aria-hidden="true" />
            <span>Add Rule</span>
          </button>
        </div>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search reports" />
      </label>
      <DataGrid
        columns={columns}
        rows={reports}
        getRowKey={(report) => report.id}
        emptyMessage="No reports match the current filters."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={pageSize}
        total={total}
        totalPages={totalPages}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={[
          {
            label: 'Open report',
            icon: <Download size={16} aria-hidden="true" />,
            onClick: openReport,
            disabled: (report) => !safeReportLink(report.reportLink)
          },
          {
            label: 'Edit report rule',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (report) => onNavigate(withReturnTo(`/reports/rules/${report.reportRuleId}`, returnPath)),
            disabled: (report) => !report.reportRuleId
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the ReportRulesListPanel React component and wires its local UI behavior.
function ReportRulesListPanel({ locationPath, onNavigate, onRequestError }: ReportsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [rules, setRules] = useState<ReportRuleListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'lastGenerated');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const query = useMemo<QueryReportRulesRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);

  const columns = useMemo<DataGridColumn<ReportRuleListItem>[]>(() => [
    { key: 'reportName', header: 'Rule', sortable: true, render: (rule) => rule.reportName || 'Scheduled Report' },
    { key: 'siteName', header: 'Site', sortable: true, render: (rule) => rule.siteName },
    { key: 'frequency', header: 'Frequency', sortable: true, render: (rule) => rule.frequencyLabel },
    { key: 'schedule', header: 'Schedule', render: formatRuleSchedule },
    { key: 'lastGenerated', header: 'Last Generated', sortable: true, render: (rule) => formatDateTime(rule.lastGenerated) || 'Never' }
  ], []);

  const loadRules = useCallback(async (signal?: AbortSignal) => {
    setIsLoading(true);
    try {
      const response = await queryReportRules(query, { signal });
      setRules(response.results);
      setTotal(response.total);
      setTotalPages(response.totalPages);
      setError(null);
    } catch (err) {
      if (isAbortError(err)) {
        return;
      }
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [onRequestError, query]);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildRulesUrl({ searchText, page, sort: sortKey, sortDir }));
    loadRules(controller.signal).catch(onRequestError);
    return () => controller.abort();
  }, [loadRules, onRequestError, page, searchText, sortDir, sortKey]);

  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }

  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const backPath = returnToOr(locationPath, '/reports');
  const returnPath = currentRoutePath(locationPath);

  async function handleDelete(rule: ReportRuleListItem) {
    if (!globalThis.confirm(`Delete ${rule.reportName || rule.frequencyLabel} report rule?`)) {
      return;
    }
    setNotice(null);
    setError(null);
    try {
      await deleteReportRule(rule.id);
      setNotice('Report rule has been deleted.');
      await loadRules();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Reports</p>
          <h2>Report Rules</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Reports</span>
          </button>
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/reports/rules/new', returnPath))}>
            <Plus size={17} aria-hidden="true" />
            <span>Add Rule</span>
          </button>
        </div>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search rules" />
      </label>
      {notice && <Notice tone="success" message={notice} />}
      <DataGrid
        columns={columns}
        rows={rules}
        getRowKey={(rule) => rule.id}
        emptyMessage="No report rules match the current filters."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={pageSize}
        total={total}
        totalPages={totalPages}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={[
          {
            label: 'Edit report rule',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (rule) => onNavigate(withReturnTo(`/reports/rules/${rule.id}`, returnPath))
          },
          {
            label: 'Manage report users',
            icon: <UsersRound size={16} aria-hidden="true" />,
            onClick: (rule) => onNavigate(withReturnTo(`/reports/rules/${rule.id}/users`, returnPath))
          },
          {
            label: 'Delete report rule',
            icon: <Trash2 size={16} aria-hidden="true" />,
            onClick: handleDelete
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the ReportRuleForm React component and wires its local UI behavior.
function ReportRuleForm({
  ruleId,
  locationPath,
  onNavigate,
  onRequestError
}: ReportsPanelProps & Readonly<{ ruleId?: string }>) {
  const [options, setOptions] = useState<ReportRuleOptionsResponse | null>(null);
  const [form, setForm] = useState<ReportRuleMutationRequest>(emptyRuleForm());
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const isEdit = Boolean(ruleId);
  const selectedSite = options?.sites.find((site) => site.value === form.siteId);
  const selectedSiteDisabled = selectedSite?.disabled === true;
  const selectedSiteError = selectedSiteDisabled
    ? 'This site is archived. Select an active site before saving changes.'
    : null;
  const backPath = returnToOr(locationPath, '/reports/rules');

  useEffect(() => {
    setNotice(null);
    if (ruleId) {
      getReportRule(ruleId)
        .then((response) => {
          const rule = response.item;
          if (!rule) {
            return;
          }
          setOptions({
            sites: rule.sites,
            frequencies: rule.frequencies,
            daysOfWeek: rule.daysOfWeek,
            alertRuleGuidelines: rule.alertRuleGuidelines
          });
          setForm({
            siteId: rule.siteId,
            frequency: rule.frequency,
            dayOfWeek: rule.dayOfWeek ?? monday,
            dayOfMonth: rule.dayOfMonth ?? 1,
            reportName: rule.reportName ?? ''
          });
          setError(null);
        })
        .catch((err: Error) => {
          setError(err.message);
          onRequestError(err);
        });
      return;
    }

    getReportRuleOptions()
      .then((nextOptions) => {
        setOptions(nextOptions);
        setForm((current) => ({
          ...current,
          siteId: current.siteId || nextOptions.sites[0]?.value || ''
        }));
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError, ruleId]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (selectedSiteDisabled) {
      setError(selectedSiteError);
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      const request = normalizeRuleForm(form);
      if (ruleId) {
        await updateReportRule(ruleId, request);
        onNavigate(backPath);
      } else {
        const response = await createReportRule(request);
        const createdId = response.item?.id;
        onNavigate(createdId ? withReturnTo(`/reports/rules/${createdId}/users`, backPath) : backPath);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleGenerateNow() {
    if (!ruleId) {
      return;
    }

    setIsGenerating(true);
    setNotice(null);
    setError(null);
    try {
      const response: ReportGenerationRequestResponse = await requestReportRuleGeneration(ruleId);
      setNotice(response.message || `Manual generation ${response.status.toLowerCase()}.`);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsGenerating(false);
    }
  }

  // Function summary: Handles the handle frequency change workflow for this module.
  function handleFrequencyChange(value: string) {
    const frequency = Number(value);
    setForm((current) => ({
      ...current,
      frequency,
      dayOfWeek: requiresDayOfWeek(frequency) ? current.dayOfWeek ?? monday : null,
      dayOfMonth: requiresDayOfMonth(frequency) ? current.dayOfMonth ?? 1 : null
    }));
  }

  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Report Rules</p>
          <h2>{isEdit ? 'Edit Rule' : 'Add Rule'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Back</span>
          </button>
          {ruleId && (
            <button className="secondary-button" type="button" onClick={handleGenerateNow} disabled={isGenerating}>
              <PlayCircle size={17} aria-hidden="true" />
              <span>{isGenerating ? 'Generating' : 'Generate Now'}</span>
            </button>
          )}
          {ruleId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/reports/rules/${ruleId}/users`, backPath))}>
              <UsersRound size={17} aria-hidden="true" />
              <span>Users</span>
            </button>
          )}
        </div>
      </div>
      <div className="detail-grid" aria-label="Report setup steps">
        <DetailItem label="Step 1" value="Site" />
        <DetailItem label="Step 2" value="Schedule" />
        <DetailItem label="Step 3" value="Recipients" />
      </div>
      <ReportGuidelinesPanel guidelines={options?.alertRuleGuidelines ?? []} />
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Site" error={selectedSiteError}>
          <select value={form.siteId} onChange={(event) => setForm({ ...form, siteId: event.target.value })}>
            {options?.sites.map((site) => (
              <option key={site.value} value={site.value} disabled={site.disabled === true}>{site.label}</option>
            ))}
          </select>
        </FormField>
        <FormField label="Report Name">
          <input value={form.reportName ?? ''} maxLength={128} onChange={(event) => setForm({ ...form, reportName: event.target.value })} />
        </FormField>
        <FormField label="Frequency">
          <select value={form.frequency} onChange={(event) => handleFrequencyChange(event.target.value)}>
            {options?.frequencies.map((frequency) => (
              <option key={frequency.value} value={frequency.value}>{frequency.label}</option>
            ))}
          </select>
        </FormField>
        {requiresDayOfWeek(form.frequency) && (
          <FormField label="Day of Week">
            <select value={form.dayOfWeek ?? monday} onChange={(event) => setForm({ ...form, dayOfWeek: Number(event.target.value) })}>
              {options?.daysOfWeek.map((day) => (
                <option key={day.value} value={day.value}>{day.label}</option>
              ))}
            </select>
          </FormField>
        )}
        {requiresDayOfMonth(form.frequency) && (
          <FormField label="Day of Month">
            <input
              value={form.dayOfMonth ?? 1}
              inputMode="numeric"
              min={1}
              max={31}
              type="number"
              onChange={(event) => setForm({ ...form, dayOfMonth: parsePositiveInt(event.target.value, 1) })}
            />
          </FormField>
        )}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          disabled={!form.siteId || selectedSiteDisabled}
          idleLabel="Save Report Rule"
        />
      </form>
    </section>
  );
}

// Function summary: Renders the ReportRuleUsersPanel React component and wires its local UI behavior.
function ReportRuleUsersPanel({
  ruleId,
  locationPath,
  onNavigate,
  onRequestError
}: ReportsPanelProps & Readonly<{ ruleId: string }>) {
  const [context, setContext] = useState<ReportRuleAssignmentContext | null>(null);
  const [available, setAvailable] = useState<UserGridState>(emptyUserGrid());
  const [assigned, setAssigned] = useState<UserGridState>(emptyUserGrid());
  const [availableSearch, setAvailableSearch] = useState('');
  const [assignedSearch, setAssignedSearch] = useState('');
  const [availablePage, setAvailablePage] = useState(1);
  const [assignedPage, setAssignedPage] = useState(1);
  const [availableSortKey, setAvailableSortKey] = useState('email');
  const [assignedSortKey, setAssignedSortKey] = useState('email');
  const [availableSortDir, setAvailableSortDir] = useState<SortDirection>('Ascending');
  const [assignedSortDir, setAssignedSortDir] = useState<SortDirection>('Ascending');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const availableQuery = useMemo<QueryCompaniesRequest>(() => ({
    searchText: availableSearch,
    page: availablePage,
    pageSize,
    sort: availableSortKey,
    sortDir: availableSortDir
  }), [availablePage, availableSearch, availableSortDir, availableSortKey]);
  const assignedQuery = useMemo<QueryCompaniesRequest>(() => ({
    searchText: assignedSearch,
    page: assignedPage,
    pageSize,
    sort: assignedSortKey,
    sortDir: assignedSortDir
  }), [assignedPage, assignedSearch, assignedSortDir, assignedSortKey]);

  const columns = useMemo<DataGridColumn<UserListItem>[]>(() => [
    {
      key: 'email',
      header: 'User',
      sortable: true,
      render: (user) => (
        <span className="cell-with-icon">
          <UserRound size={16} aria-hidden="true" />
          {user.email}
        </span>
      )
    },
    { key: 'name', header: 'Name', sortable: true, render: (user) => user.name || 'None' },
    { key: 'role', header: 'Role', sortable: true, render: (user) => user.role },
    { key: 'companyName', header: 'Company', sortable: true, render: (user) => user.companyName || 'RVT Group' }
  ], []);
  const handleAvailableSortChange = useGridSortHandler(setAvailableSortKey, setAvailableSortDir, setAvailablePage);
  const handleAssignedSortChange = useGridSortHandler(setAssignedSortKey, setAssignedSortDir, setAssignedPage);
  const backPath = returnToOr(locationPath, '/reports/rules');

  const loadUsers = useCallback(async (signal?: AbortSignal) => {
    setAvailable((current) => ({ ...current, isLoading: true, error: null }));
    setAssigned((current) => ({ ...current, isLoading: true, error: null }));
    try {
      const [availableResponse, assignedResponse] = await Promise.all([
        queryReportRuleAvailableUsers(ruleId, availableQuery, { signal }),
        queryReportRuleAssignedUsers(ruleId, assignedQuery, { signal })
      ]);
      setContext((current) => contextFromPagedUsers(assignedResponse) ?? contextFromPagedUsers(availableResponse) ?? current);
      setAvailable(userGridFromResponse(availableResponse));
      setAssigned(userGridFromResponse(assignedResponse));
      setError(null);
    } catch (err) {
      if (isAbortError(err)) {
        return;
      }
      try {
        const response = await getReportRuleUsers(ruleId);
        const item = response.item ?? null;
        setContext(item ? contextFromAssignments(item) : null);
        setAvailable(item ? userGridFromUsers(item.availableUsers, availableQuery) : emptyUserGrid());
        setAssigned(item ? userGridFromUsers(item.assignedUsers, assignedQuery) : emptyUserGrid());
        setError(null);
      } catch (fallbackErr) {
        const message = (fallbackErr as Error).message || (err as Error).message;
        setAvailable((current) => ({ ...current, error: message, isLoading: false }));
        setAssigned((current) => ({ ...current, error: message, isLoading: false }));
        setError(message);
        onRequestError(fallbackErr);
      }
    }
  }, [assignedQuery, availableQuery, onRequestError, ruleId]);

  useEffect(() => {
    const controller = new AbortController();
    loadUsers(controller.signal).catch(onRequestError);
    return () => controller.abort();
  }, [loadUsers, onRequestError]);

  async function runMutation(action: () => Promise<{ item?: ReportUserAssignmentResponse | null }>) {
    setIsBusy(true);
    setError(null);
    try {
      const response = await action();
      if (response.item) {
        setContext(contextFromAssignments(response.item));
      }
      await loadUsers();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
    }
  }

  function handleAvailableSearch(value: string) {
    setAvailableSearch(value);
    setAvailablePage(1);
  }

  function handleAssignedSearch(value: string) {
    setAssignedSearch(value);
    setAssignedPage(1);
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Report Rule</p>
          <h2>{context?.siteName || 'Report Users'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Rules</span>
          </button>
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/reports/rules/${ruleId}`, backPath))}>
            <Edit3 size={17} aria-hidden="true" />
            <span>Edit Rule</span>
          </button>
        </div>
      </div>
      {context?.companyName && (
        <div className="detail-grid">
          <DetailItem label="Site" value={context.siteName} />
          <DetailItem label="Company" value={context.companyName} />
          <DetailItem label="Assigned" value={String(assigned.total)} />
        </div>
      )}
      {error && <Notice tone="error" message={error} />}
      <section className="subsection">
        <div className="subsection-heading">
          <UserPlus size={18} aria-hidden="true" />
          <h3>Available Users</h3>
        </div>
        <label className="search-box">
          <Search size={18} aria-hidden="true" />
          <input
            value={availableSearch}
            onChange={(event) => handleAvailableSearch(event.target.value)}
            placeholder="Search available users"
          />
        </label>
        <DataGrid
          columns={columns}
          rows={available.rows}
          getRowKey={(user) => user.id}
          emptyMessage="No available users match the current filters."
          error={available.error}
          isLoading={available.isLoading}
          page={availablePage}
          pageSize={pageSize}
          total={available.total}
          totalPages={available.totalPages}
          sortKey={availableSortKey}
          sortDirection={availableSortDir}
          onPageChange={setAvailablePage}
          onSortChange={handleAvailableSortChange}
          rowActions={[
            {
              label: 'Add report user',
              icon: <UserPlus size={16} aria-hidden="true" />,
              onClick: (user) => runMutation(() => addReportRuleUser(ruleId, { userId: user.id })),
              disabled: () => isBusy
            }
          ]}
        />
      </section>
      <section className="subsection">
        <div className="subsection-heading">
          <UsersRound size={18} aria-hidden="true" />
          <h3>Assigned Users</h3>
        </div>
        <label className="search-box">
          <Search size={18} aria-hidden="true" />
          <input
            value={assignedSearch}
            onChange={(event) => handleAssignedSearch(event.target.value)}
            placeholder="Search assigned users"
          />
        </label>
        <DataGrid
          columns={columns}
          rows={assigned.rows}
          getRowKey={(user) => user.id}
          emptyMessage="No users are assigned to this report rule."
          error={assigned.error}
          isLoading={assigned.isLoading}
          page={assignedPage}
          pageSize={pageSize}
          total={assigned.total}
          totalPages={assigned.totalPages}
          sortKey={assignedSortKey}
          sortDirection={assignedSortDir}
          onPageChange={setAssignedPage}
          onSortChange={handleAssignedSortChange}
          rowActions={[
            {
              label: 'Remove report user',
              icon: <Trash2 size={16} aria-hidden="true" />,
              onClick: (user) => runMutation(() => removeReportRuleUser(ruleId, user.id)),
              disabled: () => isBusy
            }
          ]}
        />
      </section>
    </section>
  );
}

// Function summary: Renders report alert-rule guideline content when supplied by the API.
function ReportGuidelinesPanel({ guidelines }: Readonly<{ guidelines: ReportAlertRuleGuidelineItem[] }>) {
  if (guidelines.length === 0) {
    return null;
  }

  return (
    <section className="subsection" aria-label="Alert rule guidelines">
      <div className="subsection-heading">
        <BookOpen size={18} aria-hidden="true" />
        <h3>Alert Rule Guidelines</h3>
      </div>
      <div className="detail-grid">
        {guidelines.map((guideline) => (
          <DetailItem
            key={`${guideline.monitorType}-${guideline.title}`}
            label={guideline.monitorType}
            value={guideline.summary || guideline.body || guideline.title}
          />
        ))}
      </div>
    </section>
  );
}

// Function summary: Builds the default user grid state for report recipient panels.
function emptyUserGrid(): UserGridState {
  return {
    rows: [],
    total: 0,
    totalPages: 0,
    error: null,
    isLoading: false
  };
}

// Function summary: Maps paged report-recipient responses into DataGrid state.
function userGridFromResponse(response: QueryReportRuleUsersResponse): UserGridState {
  return {
    rows: response.results,
    total: response.total,
    totalPages: response.totalPages,
    error: null,
    isLoading: false
  };
}

// Function summary: Pages and filters legacy report-recipient arrays for the dual-grid assignment view.
function userGridFromUsers(users: UserListItem[], query: QueryCompaniesRequest): UserGridState {
  const searchText = query.searchText?.trim().toLowerCase() ?? '';
  const filtered = searchText ? users.filter((user) => userMatchesSearch(user, searchText)) : [...users];
  const sorted = sortUsers(filtered, query.sort ?? 'email', query.sortDir ?? 'Ascending');
  const currentPage = query.page ?? 1;
  const currentPageSize = query.pageSize ?? pageSize;
  const totalPages = sorted.length > 0 ? Math.ceil(sorted.length / currentPageSize) : 0;
  const start = (currentPage - 1) * currentPageSize;

  return {
    rows: sorted.slice(start, start + currentPageSize),
    total: sorted.length,
    totalPages,
    error: null,
    isLoading: false
  };
}

// Function summary: Evaluates whether a user matches report-recipient grid search text.
function userMatchesSearch(user: UserListItem, searchText: string) {
  return [user.email, user.name, user.role, user.companyName]
    .some((value) => value?.toLowerCase().includes(searchText));
}

// Function summary: Sorts report-recipient users for legacy assignment endpoint fallback data.
function sortUsers(users: UserListItem[], sortKey: string, sortDir: SortDirection) {
  return [...users].sort((left, right) => {
    const leftValue = userSortValue(left, sortKey);
    const rightValue = userSortValue(right, sortKey);
    const result = leftValue.localeCompare(rightValue, undefined, { sensitivity: 'base' });
    return sortDir === 'Descending' ? -result : result;
  });
}

// Function summary: Retrieves sortable text values for report-recipient user rows.
function userSortValue(user: UserListItem, sortKey: string) {
  if (sortKey === 'name') {
    return user.name ?? '';
  }
  if (sortKey === 'role') {
    return user.role;
  }
  if (sortKey === 'companyName') {
    return user.companyName ?? '';
  }
  return user.email;
}

// Function summary: Extracts report-rule assignment context from legacy assignment responses.
function contextFromAssignments(assignments: ReportUserAssignmentResponse): ReportRuleAssignmentContext {
  return {
    reportRuleId: assignments.reportRuleId,
    siteId: assignments.siteId,
    siteName: assignments.siteName,
    companyId: assignments.companyId,
    companyName: assignments.companyName
  };
}

// Function summary: Extracts report-rule assignment context from future paged recipient responses.
function contextFromPagedUsers(response: QueryReportRuleUsersResponse): ReportRuleAssignmentContext | null {
  if (!response.reportRuleId || !response.siteId || !response.siteName) {
    return null;
  }

  return {
    reportRuleId: response.reportRuleId,
    siteId: response.siteId,
    siteName: response.siteName,
    companyId: response.companyId,
    companyName: response.companyName
  };
}

// Function summary: Renders the DetailItem React component and wires its local UI behavior.
function DetailItem({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

// Function summary: Handles the open report workflow for this module.
function openReport(report: ReportListItem) {
  const link = safeReportLink(report.reportLink);
  if (!link) {
    return;
  }

  globalThis.open(link, '_blank', 'noopener,noreferrer');
}

// Function summary: Handles the safe report link workflow for this module.
function safeReportLink(value: string) {
  if (!value.trim()) {
    return null;
  }

  let url: URL;
  try {
    url = new URL(value, globalThis.location.origin);
  } catch {
    return null;
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') {
    return null;
  }

  return url.toString();
}

// Function summary: Handles the parse reports route workflow for this module.
function parseReportsRoute(locationPath: string): ReportsRoute {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (/^\/reports\/rules\/new$/i.test(path)) {
    return { kind: 'new-rule' };
  }

  const usersMatch = /^\/reports\/rules\/([^/]+)\/users$/i.exec(path);
  if (usersMatch) {
    return { kind: 'rule-users', ruleId: usersMatch[1] };
  }

  const editMatch = /^\/reports\/rules\/([^/]+)$/i.exec(path);
  if (editMatch) {
    return { kind: 'edit-rule', ruleId: editMatch[1] };
  }

  if (/^\/reports\/rules$/i.test(path)) {
    return { kind: 'rules' };
  }

  return { kind: 'reports' };
}

// Function summary: Builds reports url data for callers.
function buildReportsUrl({ searchText, page, sort, sortDir }: Readonly<{ searchText: string; page: number; sort: string; sortDir: SortDirection }>) {
  const params = new URLSearchParams({ page: String(page), sort, sortDir });
  if (searchText.trim()) {
    params.set('q', searchText.trim());
  }
  return `/reports?${params.toString()}`;
}

// Function summary: Builds rules url data for callers.
function buildRulesUrl({ searchText, page, sort, sortDir }: Readonly<{ searchText: string; page: number; sort: string; sortDir: SortDirection }>) {
  const params = new URLSearchParams({ page: String(page), sort, sortDir });
  if (searchText.trim()) {
    params.set('q', searchText.trim());
  }
  return `/reports/rules?${params.toString()}`;
}

// Function summary: Handles the normalize sort direction workflow for this module.
function normalizeSortDirection(value: string | null, fallback: SortDirection = 'Ascending'): SortDirection {
  return value === 'Descending' || value === 'desc' ? 'Descending' : fallback;
}

// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

// Function summary: Handles the empty rule form workflow for this module.
function emptyRuleForm(): ReportRuleMutationRequest {
  return {
    siteId: '',
    frequency: weeklyFrequency,
    dayOfWeek: monday,
    dayOfMonth: null,
    reportName: ''
  };
}

// Function summary: Handles the normalize rule form workflow for this module.
function normalizeRuleForm(form: ReportRuleMutationRequest): ReportRuleMutationRequest {
  return {
    siteId: form.siteId,
    frequency: form.frequency,
    dayOfWeek: requiresDayOfWeek(form.frequency) ? form.dayOfWeek ?? monday : null,
    dayOfMonth: requiresDayOfMonth(form.frequency) ? form.dayOfMonth ?? 1 : null,
    reportName: form.reportName?.trim() || null
  };
}

// Function summary: Handles the requires day of week workflow for this module.
function requiresDayOfWeek(frequency: number) {
  return frequency === weeklyFrequency || frequency === weeklyAndMonthlyFrequency;
}

// Function summary: Handles the requires day of month workflow for this module.
function requiresDayOfMonth(frequency: number) {
  return frequency === monthlyFrequency || frequency === weeklyAndMonthlyFrequency;
}

// Function summary: Handles the format rule schedule workflow for this module.
function formatRuleSchedule(rule: { frequency: number; dayOfWeek?: number | null; dayOfMonth?: number | null }) {
  if (rule.frequency === dailyFrequency) {
    return 'Daily';
  }
  if (rule.frequency === weeklyFrequency) {
    return dayOfWeekLabel(rule.dayOfWeek);
  }
  if (rule.frequency === monthlyFrequency) {
    return dayOfMonthLabel(rule.dayOfMonth);
  }
  if (rule.frequency === weeklyAndMonthlyFrequency) {
    return `${dayOfWeekLabel(rule.dayOfWeek)} and ${dayOfMonthLabel(rule.dayOfMonth)}`;
  }
  return 'Off';
}

// Function summary: Handles the day of week label workflow for this module.
function dayOfWeekLabel(value?: number | null) {
  const day = dayOptions.find((option) => option.value === value);
  return day?.label ?? 'Monday';
}

// Function summary: Handles the day of month label workflow for this module.
function dayOfMonthLabel(value?: number | null) {
  return value ? `Day ${value}` : 'Day 1';
}

// Function summary: Handles the format period workflow for this module.
function formatPeriod(from: string, to: string) {
  return `${formatDate(from)} to ${formatDate(to)}`;
}

// Function summary: Handles the format date workflow for this module.
function formatDate(value?: string | null) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value));
}

// Function summary: Handles the format date time workflow for this module.
function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(value));
}

const dayOptions: ReadonlyArray<{ value: number; label: string }> = [
  { value: 0, label: 'Sunday' },
  { value: 1, label: 'Monday' },
  { value: 2, label: 'Tuesday' },
  { value: 3, label: 'Wednesday' },
  { value: 4, label: 'Thursday' },
  { value: 5, label: 'Friday' },
  { value: 6, label: 'Saturday' }
];

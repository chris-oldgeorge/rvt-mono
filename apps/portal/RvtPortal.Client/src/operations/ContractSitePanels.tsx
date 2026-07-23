// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for contract and site list requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for contract and site forms/details.
// - 2026-06-10 pending Cleared stale contract errors after successful load and retry flows.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-06-08 pending Added legacy Create Site quick actions for company and contract creation.
// - 2026-06-09 pending Added site detail shortcuts for legacy map, data, calendar, and notification workflows.
// - 2026-06-09 pending Embedded current monitor map context on site detail.
// - 2026-06-24 pending Added site customer-logo upload/delete controls for report branding.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  Archive,
  BarChart3,
  Bell,
  CalendarDays,
  Edit3,
  Eye,
  FileText,
  Gauge,
  Image as ImageIcon,
  MapPinned,
  Plus,
  Save,
  Search,
  Settings,
  Star,
  Trash2,
  Upload,
  UserPlus,
  UserRound,
  X
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import {
  addUserToSite,
  archiveSite,
  createContract,
  createSite,
  deleteContract,
  deleteSiteCustomerLogo,
  getContract,
  getContractOptions,
  getSite,
  getSiteNotificationSettings,
  getSiteOptions,
  getSiteAssignments,
  isAbortError,
  queryContracts,
  querySites,
  removeSiteContactUser,
  removeUserFromSite,
  setSiteContactUser,
  updateContract,
  uploadSiteCustomerLogo,
  updateSite,
  updateSiteNotificationSetting
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { MonitorMap, MonitorMarkerList } from '../components/MonitorMap';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import type {
  ContractDetailResponse,
  ContractListItem,
  ContractMutationRequest,
  ContractOptionsResponse,
  QueryContractsRequest,
  QuerySitesRequest,
  SiteAssignmentResponse,
  SiteDetailResponse,
  SiteListItem,
  MapMonitorMarker,
  SiteMutationRequest,
  SiteOperatingHours,
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
  SiteNotificationSettingsResponse,
  SiteOptionsResponse,
  SiteUserAssignmentItem,
  SortDirection
} from '../dtos';
const pageSize = 10;
const siteOperatingDays: SiteOperatingHours[] = [
  { dayOfWeek: 1, dayName: 'Monday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 2, dayName: 'Tuesday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 3, dayName: 'Wednesday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 4, dayName: 'Thursday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 5, dayName: 'Friday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 6, dayName: 'Saturday', startTime: '', endTime: '', isClosed: true },
  { dayOfWeek: 7, dayName: 'Sunday', startTime: '', endTime: '', isClosed: true }
];
type OperationsPanelCallbacks = Readonly<{
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type OperationsRouteProps = OperationsPanelCallbacks & Readonly<{
  locationPath: string;
}>;

type SitesPanelProps = OperationsRouteProps & Readonly<{
  canManage?: boolean;
  currentUserId?: string | null;
}>;

type SiteListPanelProps = OperationsRouteProps & Readonly<{
  canManage?: boolean;
}>;

type SiteDetailPanelProps = OperationsPanelCallbacks & Readonly<{
  siteId: string;
  locationPath: string;
  canManage?: boolean;
  currentUserId?: string | null;
}>;

type NotificationSettingsPanelProps = Readonly<{
  settings: SiteNotificationSettingsResponse;
  canManage: boolean;
  currentUserId?: string | null;
  onUpdated: (settings: SiteNotificationSettingsResponse) => void;
  onRequestError: (error: unknown) => void;
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

// Function summary: Renders the ContractsPanel React component and wires its local UI behavior.
export function ContractsPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const mode = parseContractsMode(locationPath);
  if (mode.kind === 'create') {
    return <ContractFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return <ContractFormPanel contractId={mode.contractId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'detail') {
    return <ContractDetailPanel contractId={mode.contractId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  return <ContractListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}
// Function summary: Renders the SitesPanel React component and wires its local UI behavior.
export function SitesPanel({ locationPath, onNavigate, onRequestError, canManage = false, currentUserId }: SitesPanelProps) {
  const mode = parseSitesMode(locationPath);
  if (mode.kind === 'create' && canManage) {
    return <SiteFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit' && canManage) {
    return <SiteFormPanel siteId={mode.siteId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'detail') {
    return (
      <SiteDetailPanel
        siteId={mode.siteId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
        canManage={canManage}
        currentUserId={currentUserId}
      />
    );
  }
  return <SiteListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} canManage={canManage} />;
}
// Function summary: Renders the ContractListPanel React component and wires its local UI behavior.
function ContractListPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [contracts, setContracts] = useState<ContractListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'contractNumber');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const columns = useMemo<DataGridColumn<ContractListItem>[]>(() => [
    {
      key: 'contractNumber',
      header: 'Contract',
      sortable: true,
      render: (contract) => (
        <span className="cell-with-icon">
          <FileText size={16} aria-hidden="true" />
          {contract.contractNumber}
        </span>
      )
    },
    { key: 'siteName', header: 'Site', sortable: true, render: (contract) => contract.siteName || 'Unassigned' },
    { key: 'companyName', header: 'Company', sortable: true, render: (contract) => contract.companyName || 'None' },
    { key: 'onHireDate', header: 'On Hire', sortable: true, render: (contract) => formatDate(contract.onHireDate) },
    { key: 'offHireDate', header: 'Off Hire', sortable: true, render: (contract) => formatDate(contract.offHireDate) || 'Open' }
  ], []);
  const query = useMemo<QueryContractsRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const returnPath = currentRoutePath(locationPath);
  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildContractsUrl({ searchText, page, sort: sortKey, sortDir }));
    setIsLoading(true);
    queryContracts(query, { signal: controller.signal })
      .then((response) => {
        setContracts(response.results);
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
          <p>Operations</p>
          <h2>Contracts</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/contracts/new', returnPath))}>
          <Plus size={17} aria-hidden="true" />
          <span>Create Contract</span>
        </button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search contracts" />
      </label>
      <DataGrid
        columns={columns}
        rows={contracts}
        getRowKey={(contract) => contract.id}
        emptyMessage="No contracts match the current search."
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
            label: 'View contract',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}`, returnPath))
          },
          {
            label: 'Edit contract',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}/edit`, returnPath))
          }
        ]}
      />
    </section>
  );
}
// Function summary: Renders the ContractDetailPanel React component and wires its local UI behavior.
function ContractDetailPanel({ contractId, locationPath, onNavigate, onRequestError }: OperationsRouteProps & Readonly<{ contractId: string }>) {
  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const backPath = returnToOr(locationPath, '/contracts');
  const detailPath = currentRoutePath(locationPath);
  useEffect(() => {
    getContract(contractId)
      .then((response) => {
        setContract(response.item ?? null);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError]);
  async function handleDelete() {
    setIsDeleting(true);
    setError(null);
    try {
      await deleteContract(contractId);
      onNavigate(backPath);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setConfirmDelete(false);
      setIsDeleting(false);
    }
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Contract</p>
          <h2>{contract?.contractNumber ?? 'Loading contract'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/contracts/${contractId}/edit`, detailPath))} disabled={!contract}>
            <Edit3 size={17} aria-hidden="true" />
            <span>Edit</span>
          </button>
          <button className="danger-button" type="button" onClick={() => setConfirmDelete(true)} disabled={!contract}>
            <Trash2 size={17} aria-hidden="true" />
            <span>Delete</span>
          </button>
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {contract && (
        <div className="detail-stack">
          <ReadOnlyRow label="Contract Number" value={contract.contractNumber} />
          <ReadOnlyRow label="Company" value={contract.companyName || 'None'} />
          <ReadOnlyRow label="Site" value={contract.siteName || 'Unassigned'} />
          <ReadOnlyRow label="On Hire Date" value={formatDate(contract.onHireDate)} />
          <ReadOnlyRow label="Off Hire Date" value={formatDate(contract.offHireDate) || 'Open'} />
          {contract.siteId && (
            <button className="secondary-button inline" type="button" onClick={() => onNavigate(withReturnTo(`/sites/${contract.siteId}`, detailPath))}>
              <MapPinned size={17} aria-hidden="true" />
              <span>Open site</span>
            </button>
          )}
        </div>
      )}
      <ConfirmDialog
        open={confirmDelete}
        title="Delete contract"
        message={`Delete ${contract?.contractNumber ?? 'this contract'}?`}
        confirmLabel="Delete"
        isBusy={isDeleting}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={handleDelete}
      />
    </section>
  );
}
type ContractFormState = {
  contractNumber: string;
  companyId: string;
  siteId: string;
  onHireDate: string;
  offHireDate: string;
};
// Function summary: Renders the ContractFormPanel React component and wires its local UI behavior.
function ContractFormPanel({ contractId, locationPath, onNavigate, onRequestError }: OperationsRouteProps & Readonly<{ contractId?: string }>) {
  const isEdit = Boolean(contractId);
  const backPath = returnToOr(locationPath, contractId ? `/contracts/${contractId}` : '/contracts');
  const [form, setForm] = useState<ContractFormState>({
    contractNumber: '',
    companyId: '',
    siteId: '',
    onHireDate: toDateInput(new Date().toISOString()),
    offHireDate: ''
  });
  const [options, setOptions] = useState<ContractOptionsResponse>({ companies: [], sites: [] });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  useEffect(() => {
    getContractOptions()
      .then((nextOptions) => {
        setOptions(nextOptions);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError]);
  useEffect(() => {
    if (!contractId) {
      return;
    }
    getContract(contractId)
      .then((response) => {
        const item = response.item;
        if (item) {
          setForm({
            contractNumber: item.contractNumber,
            companyId: item.companyId,
            siteId: item.siteId ?? '',
            onHireDate: toDateInput(item.onHireDate),
            offHireDate: toDateInput(item.offHireDate)
          });
          setOptions({ companies: item.companies, sites: item.sites });
        }
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError]);
  async function handleCompanyChange(companyId: string) {
    setForm((current) => ({ ...current, companyId, siteId: '' }));
    setStatus(null);
    try {
      setOptions(await getContractOptions(companyId || undefined));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }
  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const payload: ContractMutationRequest = {
        contractNumber: form.contractNumber,
        companyId: form.companyId,
        siteId: form.siteId || null,
        onHireDate: form.onHireDate,
        offHireDate: form.offHireDate || null
      };
      const response = isEdit && contractId ? await updateContract(contractId, payload) : await createContract(payload);
      const saved = response.item;
      setStatus(isEdit ? 'Contract updated.' : 'Contract created.');
      if (saved?.id) {
        onNavigate(`/contracts/${saved.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }
  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Contract</p>
          <h2>{isEdit ? 'Edit Contract' : 'Add Contract'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Contract Number">
          <input value={form.contractNumber} onChange={(event) => setForm({ ...form, contractNumber: event.target.value })} maxLength={20} />
        </FormField>
        <FormField label="Company">
          <select value={form.companyId} onChange={(event) => handleCompanyChange(event.target.value)}>
            <option value="">Select a Company</option>
            {options.companies.map((company) => (
              <option value={company.value} key={company.value}>{company.label}</option>
            ))}
          </select>
        </FormField>
        <FormField label="Site">
          <select value={form.siteId} onChange={(event) => setForm({ ...form, siteId: event.target.value })}>
            <option value="">Unassigned</option>
            {options.sites.map((site) => (
              <option value={site.value} key={site.value}>{site.label}</option>
            ))}
          </select>
        </FormField>
        <FormField label="On Hire Date">
          <input value={form.onHireDate} onChange={(event) => setForm({ ...form, onHireDate: event.target.value })} type="date" />
        </FormField>
        <FormField label="Off Hire Date">
          <input value={form.offHireDate} onChange={(event) => setForm({ ...form, offHireDate: event.target.value })} type="date" />
        </FormField>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel={isEdit ? 'Update Contract' : 'Create Contract'} />
      </form>
    </section>
  );
}
// Function summary: Renders the SiteListPanel React component and wires its local UI behavior.
function SiteListPanel({ locationPath, onNavigate, onRequestError, canManage = false }: SiteListPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [sites, setSites] = useState<SiteListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [includeArchived, setIncludeArchived] = useState(initialParams.get('archived') === 'true');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'createDate');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir') ?? 'Descending'));
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const columns = useMemo<DataGridColumn<SiteListItem>[]>(() => [
    {
      key: 'siteName',
      header: 'Site',
      sortable: true,
      render: (site) => (
        <span className="cell-with-icon">
          <MapPinned size={16} aria-hidden="true" />
          {site.siteName}
        </span>
      )
    },
    { key: 'companyName', header: 'Company', sortable: true, render: (site) => site.companyName || 'None' },
    { key: 'contracts', header: 'Contracts', sortable: true, render: (site) => site.contracts || 'None' },
    { key: 'siteAddress', header: 'Address', sortable: true, render: (site) => site.siteAddress || 'None' },
    { key: 'monitorCount', header: 'Monitors', align: 'end', render: (site) => site.monitorCount },
    { key: 'openNotificationCount', header: 'Open Alerts', align: 'end', render: (site) => site.openNotificationCount },
    { key: 'archived', header: 'State', render: (site) => site.archived ? <span className="status-chip muted">Archived</span> : <span className="status-chip">Active</span> }
  ], []);
  const query = useMemo<QuerySitesRequest>(() => ({
    searchText,
    includeArchived,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [includeArchived, page, searchText, sortDir, sortKey]);
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const returnPath = currentRoutePath(locationPath);
  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildSitesUrl({ searchText, includeArchived, page, sort: sortKey, sortDir }));
    setIsLoading(true);
    querySites(query, { signal: controller.signal })
      .then((response) => {
        setSites(response.results);
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
  }, [includeArchived, onRequestError, page, query, searchText, sortDir, sortKey]);
  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Operations</p>
          <h2>Sites</h2>
        </div>
        {canManage && (
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/sites/new', returnPath))}>
            <Plus size={17} aria-hidden="true" />
            <span>Create Site</span>
          </button>
        )}
      </div>
      <div className="toolbar-row">
        <label className="search-box">
          <Search size={18} aria-hidden="true" />
          <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search sites" />
        </label>
        <label className="checkbox-row compact">
          <input checked={includeArchived} onChange={(event) => { setIncludeArchived(event.target.checked); setPage(1); }} type="checkbox" />
          <span>Archived</span>
        </label>
      </div>
      <DataGrid
        columns={columns}
        rows={sites}
        getRowKey={(site) => site.id}
        emptyMessage="No sites match the current search."
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
            label: 'View site',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (site) => onNavigate(withReturnTo(`/sites/${site.id}`, returnPath))
          },
          {
            label: 'Edit site',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (site) => onNavigate(withReturnTo(`/sites/${site.id}/edit`, returnPath)),
            disabled: (site) => !canManage || site.archived
          }
        ]}
      />
    </section>
  );
}
// Function summary: Renders the SiteDetailPanel React component and wires its local UI behavior.
function SiteDetailPanel({
  siteId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage = false,
  currentUserId
}: SiteDetailPanelProps) {
  const [site, setSite] = useState<SiteDetailResponse | null>(null);
  const [settings, setSettings] = useState<SiteNotificationSettingsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmArchive, setConfirmArchive] = useState(false);
  const [isArchiving, setIsArchiving] = useState(false);
  const backPath = returnToOr(locationPath, '/sites');
  const detailPath = currentRoutePath(locationPath);
  useEffect(() => {
    Promise.all([getSite(siteId), getSiteNotificationSettings(siteId)])
      .then(([siteResponse, settingsResponse]) => {
        setSite(siteResponse.item ?? null);
        setSettings(settingsResponse);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError, siteId]);
  async function handleArchive() {
    setIsArchiving(true);
    try {
      const response = await archiveSite(siteId);
      setSite(response.item ?? null);
      setConfirmArchive(false);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsArchiving(false);
    }
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Site</p>
          <h2>{site?.siteName ?? 'Loading site'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
          {site && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/maps?siteId=${site.id}`, detailPath))}>
              <MapPinned size={17} aria-hidden="true" />
              <span>Open map</span>
            </button>
          )}
          {site?.monitors[0]?.deploymentId && (
            <>
              <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/data?deploymentId=${site.monitors[0].deploymentId}`, detailPath))}>
                <BarChart3 size={17} aria-hidden="true" />
                <span>Open data</span>
              </button>
              <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/calendar?deploymentId=${site.monitors[0].deploymentId}`, detailPath))}>
                <CalendarDays size={17} aria-hidden="true" />
                <span>Open calendar</span>
              </button>
            </>
          )}
          {site && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/notifications?q=${encodeURIComponent(site.siteName)}`, detailPath))}>
              <Bell size={17} aria-hidden="true" />
              <span>Open notifications</span>
            </button>
          )}
          {canManage && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/sites/${siteId}/edit`, detailPath))} disabled={!site || site.archived}>
              <Edit3 size={17} aria-hidden="true" />
              <span>Edit</span>
            </button>
          )}
          {canManage && (
            <button className="danger-button" type="button" onClick={() => setConfirmArchive(true)} disabled={!site || site.archived}>
              <Archive size={17} aria-hidden="true" />
              <span>Archive</span>
            </button>
          )}
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {site && (
        <>
          <div className="detail-grid">
            <ReadOnlyMetric label="Monitors" value={site.monitorCount} />
            <ReadOnlyMetric label="Open Alerts" value={site.openNotificationCount} />
            <ReadOnlyMetric label="State" value={site.archived ? 'Archived' : 'Active'} />
          </div>
          <div className="split-grid">
            <div className="detail-stack">
              <ReadOnlyRow label="Company" value={site.companyName || 'None'} />
              <ReadOnlyRow label="Contracts" value={site.contracts || 'None'} />
              <ReadOnlyRow label="Address" value={site.siteAddress || 'None'} />
              <ReadOnlyRow label="Created" value={formatDate(site.createDate)} />
              {normalizeOperatingHours(site.operatingHours, site).map((hours) => (
                <ReadOnlyRow
                  label={`${hours.dayName} Hours`}
                  value={hours.isClosed ? 'Closed' : formatTimeRange(hours.startTime, hours.endTime)}
                  key={hours.dayOfWeek}
                />
              ))}
              {site.archive && <ReadOnlyRow label="Archived" value={`${formatDate(site.archive.archived)} by ${site.archive.createdBy || 'Unknown'}`} />}
            </div>
          </div>
          {siteMonitorMarkers(site).length > 0 && (
            <NestedSection title="Map" icon={<MapPinned size={18} aria-hidden="true" />}>
              <MonitorMap markers={siteMonitorMarkers(site)} label="Site detail map" />
              <MonitorMarkerList markers={siteMonitorMarkers(site)} />
            </NestedSection>
          )}
          <NestedSection title="Contracts" icon={<FileText size={18} aria-hidden="true" />}>
            <DataGrid
              columns={[
                { key: 'contractNumber', header: 'Contract', render: (contract) => contract.contractNumber },
                { key: 'companyName', header: 'Company', render: (contract) => contract.companyName || 'None' },
                { key: 'onHireDate', header: 'On Hire', render: (contract) => formatDate(contract.onHireDate) },
                { key: 'offHireDate', header: 'Off Hire', render: (contract) => formatDate(contract.offHireDate) || 'Open' }
              ]}
              rows={site.contractList}
              getRowKey={(contract) => contract.id}
              emptyMessage="No contracts are assigned to this site."
              page={1}
              pageSize={Math.max(site.contractList.length, 1)}
              total={site.contractList.length}
              totalPages={site.contractList.length > 0 ? 1 : 0}
              rowActions={canManage ? [{
                label: 'View contract',
                icon: <Eye size={16} aria-hidden="true" />,
                onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}`, detailPath))
              }] : []}
            />
          </NestedSection>
          <NestedSection title="Current Monitors" icon={<Gauge size={18} aria-hidden="true" />}>
            <DataGrid
              columns={[
                { key: 'fleetNumber', header: 'Fleet Nr', render: (monitor) => monitor.fleetNumber || 'None' },
                { key: 'serialId', header: 'Serial', render: (monitor) => monitor.serialId || 'None' },
                { key: 'typeOfMonitor', header: 'Type', render: (monitor) => monitor.typeOfMonitor },
                { key: 'contractNumber', header: 'Contract', render: (monitor) => monitor.contractNumber },
                { key: 'lastDataTime', header: 'Last Data', render: (monitor) => formatDateTime(monitor.lastDataTime) || 'None' }
              ]}
              rows={site.monitors}
              getRowKey={(monitor) => monitor.deploymentId}
              emptyMessage="No current monitors are deployed to this site."
              page={1}
              pageSize={Math.max(site.monitors.length, 1)}
              total={site.monitors.length}
              totalPages={site.monitors.length > 0 ? 1 : 0}
            />
          </NestedSection>
          <NestedSection title="Open Alerts" icon={<Bell size={18} aria-hidden="true" />}>
            <DataGrid
              columns={[
                { key: 'fleetNumber', header: 'Fleet Nr', render: (notification) => notification.fleetNumber || 'None' },
                { key: 'alertField', header: 'Field', render: (notification) => notification.alertField || 'None' },
                { key: 'level', header: 'Level', render: (notification) => notification.level ?? '' },
                { key: 'limitOn', header: 'Limit', render: (notification) => notification.limitOn ?? '' },
                { key: 'notificationTime', header: 'Time', render: (notification) => formatDateTime(notification.notificationTime) }
              ]}
              rows={site.openNotifications}
              getRowKey={(notification) => notification.id}
              emptyMessage="No open alerts are recorded for this site."
              page={1}
              pageSize={Math.max(site.openNotifications.length, 1)}
              total={site.openNotifications.length}
              totalPages={site.openNotifications.length > 0 ? 1 : 0}
            />
          </NestedSection>
          {canManage && (
            <SiteAssignmentsPanel siteId={siteId} onRequestError={onRequestError} />
          )}
          {settings && (
            <NotificationSettingsPanel
              settings={settings}
              canManage={canManage}
              currentUserId={currentUserId}
              onUpdated={setSettings}
              onRequestError={onRequestError}
            />
          )}
        </>
      )}
      <ConfirmDialog
        open={confirmArchive}
        title="Archive site"
        message={`Archive ${site?.siteName ?? 'this site'}? This will mark the site as archived in the SPA API.`}
        confirmLabel="Archive"
        isBusy={isArchiving}
        onCancel={() => setConfirmArchive(false)}
        onConfirm={handleArchive}
      />
    </section>
  );
}
type SiteFormState = {
  siteName: string;
  companyId: string;
  contractId: string;
  addressLine1: string;
  addressLine2: string;
  postcode: string;
  city: string;
  county: string;
  operatingHours: SiteOperatingHours[];
};
// Function summary: Renders the SiteFormPanel React component and wires its local UI behavior.
function SiteFormPanel({ siteId, locationPath, onNavigate, onRequestError }: OperationsRouteProps & Readonly<{ siteId?: string }>) {
  const isEdit = Boolean(siteId);
  const backPath = returnToOr(locationPath, siteId ? `/sites/${siteId}` : '/sites');
  const formPath = currentRoutePath(locationPath);
  const [form, setForm] = useState<SiteFormState>({
    siteName: '',
    companyId: '',
    contractId: '',
    addressLine1: '',
    addressLine2: '',
    postcode: '',
    city: '',
    county: '',
    operatingHours: siteOperatingDays
  });
  const [options, setOptions] = useState<SiteOptionsResponse>({ companies: [], contracts: [] });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [customerLogoUrl, setCustomerLogoUrl] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoStatus, setLogoStatus] = useState<string | null>(null);
  const [logoError, setLogoError] = useState<string | null>(null);
  const [isLogoSubmitting, setIsLogoSubmitting] = useState(false);
  const [logoInputKey, setLogoInputKey] = useState(0);
  const canSelectContract = !isEdit && Boolean(form.companyId);
  useEffect(() => {
    getSiteOptions()
      .then(setOptions)
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError]);
  useEffect(() => {
    if (!siteId) {
      return;
    }
    getSite(siteId)
      .then((response) => {
        const item = response.item;
        if (item) {
          setForm({
            siteName: item.siteName,
            companyId: item.companyId ?? '',
            contractId: '',
            addressLine1: item.addressLine1 ?? '',
            addressLine2: item.addressLine2 ?? '',
            postcode: item.postcode ?? '',
            city: item.city ?? '',
            county: item.county ?? '',
            operatingHours: normalizeOperatingHours(item.operatingHours, item)
          });
          setCustomerLogoUrl(item.customerLogoUrl ?? null);
          setOptions({ companies: item.companies, contracts: item.availableContracts });
        }
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError, siteId]);
  async function handleCompanyChange(companyId: string) {
    setForm((current) => ({ ...current, companyId, contractId: '' }));
    try {
      setOptions(await getSiteOptions(companyId || undefined));
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }
  function updateOperatingHours(dayOfWeek: number, patch: Partial<SiteOperatingHours>) {
    setForm((current) => ({
      ...current,
      operatingHours: current.operatingHours.map((hours) => (
        hours.dayOfWeek === dayOfWeek
          ? { ...hours, ...patch }
          : hours
      ))
    }));
  }
  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const payload: SiteMutationRequest = {
        siteName: form.siteName,
        companyId: form.companyId,
        contractId: isEdit ? null : form.contractId || null,
        addressLine1: form.addressLine1 || null,
        addressLine2: form.addressLine2 || null,
        postcode: form.postcode || null,
        city: form.city || null,
        county: form.county || null,
        startTime: form.operatingHours.find((hours) => hours.dayOfWeek === 1)?.startTime || null,
        endTime: form.operatingHours.find((hours) => hours.dayOfWeek === 1)?.endTime || null,
        satStartTime: form.operatingHours.find((hours) => hours.dayOfWeek === 6)?.startTime || null,
        satEndTime: form.operatingHours.find((hours) => hours.dayOfWeek === 6)?.endTime || null,
        sunStartTime: form.operatingHours.find((hours) => hours.dayOfWeek === 7)?.startTime || null,
        sunEndTime: form.operatingHours.find((hours) => hours.dayOfWeek === 7)?.endTime || null,
        operatingHours: form.operatingHours
      };
      const response = isEdit && siteId ? await updateSite(siteId, payload) : await createSite(payload);
      const saved = response.item;
      setStatus(isEdit ? 'Site updated.' : 'Site created.');
      if (saved?.id) {
        onNavigate(`/sites/${saved.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }
  function handleAddContract() {
    const query = form.companyId ? `?companyId=${encodeURIComponent(form.companyId)}` : '';
    onNavigate(withReturnTo(`/contracts/new${query}`, formPath));
  }
  async function handleUploadLogo() {
    if (!siteId || !logoFile) {
      setLogoError('Choose a customer logo image first.');
      return;
    }

    setIsLogoSubmitting(true);
    setLogoError(null);
    setLogoStatus(null);
    try {
      const response = await uploadSiteCustomerLogo(siteId, logoFile);
      setCustomerLogoUrl(response.item?.customerLogoUrl ?? null);
      setLogoFile(null);
      setLogoInputKey((current) => current + 1);
      setLogoStatus('Customer logo updated.');
    } catch (err) {
      setLogoError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsLogoSubmitting(false);
    }
  }
  async function handleDeleteLogo() {
    if (!siteId) {
      return;
    }

    setIsLogoSubmitting(true);
    setLogoError(null);
    setLogoStatus(null);
    try {
      const response = await deleteSiteCustomerLogo(siteId);
      setCustomerLogoUrl(response.item?.customerLogoUrl ?? null);
      setLogoFile(null);
      setLogoInputKey((current) => current + 1);
      setLogoStatus('Customer logo removed.');
    } catch (err) {
      setLogoError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsLogoSubmitting(false);
    }
  }
  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Site</p>
          <h2>{isEdit ? 'Edit Site' : 'Add Site'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Site Name">
          <input value={form.siteName} onChange={(event) => setForm({ ...form, siteName: event.target.value })} maxLength={100} />
        </FormField>
        <FormField label="Company">
          <select value={form.companyId} onChange={(event) => handleCompanyChange(event.target.value)} disabled={isEdit}>
            <option value="">Select a Company</option>
            {options.companies.map((company) => (
              <option value={company.value} key={company.value}>{company.label}</option>
            ))}
          </select>
        </FormField>
        {!isEdit && (
          <div className="form-action-row">
            <button className="secondary-button inline" type="button" onClick={() => onNavigate(withReturnTo('/companies/new', formPath))}>
              <Plus size={16} aria-hidden="true" />
              <span>Add Company</span>
            </button>
          </div>
        )}
        {!isEdit && (
          <FormField label="Contract">
            <select value={form.contractId} onChange={(event) => setForm({ ...form, contractId: event.target.value })} disabled={!canSelectContract}>
              <option value="">Select a Contract</option>
              {options.contracts.map((contract) => (
                <option value={contract.value} key={contract.value}>{contract.label}</option>
              ))}
            </select>
          </FormField>
        )}
        {!isEdit && (
          <div className="form-action-row">
            <button className="secondary-button inline" type="button" onClick={handleAddContract} disabled={!canSelectContract}>
              <Plus size={16} aria-hidden="true" />
              <span>Add Contract</span>
            </button>
          </div>
        )}
        {isEdit && (
          <section className="customer-logo-section" aria-label="Customer logo">
            <div className="section-heading">
              <ImageIcon size={18} aria-hidden="true" />
              <h3>Customer Logo</h3>
            </div>
            {customerLogoUrl ? (
              <img className="customer-logo-preview" src={customerLogoUrl} alt="Customer logo" />
            ) : (
              <p className="muted-text">No customer logo set.</p>
            )}
            <FormField label="Customer logo image">
              <input
                key={logoInputKey}
                accept="image/png,image/jpeg,image/webp"
                type="file"
                onChange={(event) => setLogoFile(event.target.files?.[0] ?? null)}
              />
            </FormField>
            <div className="customer-logo-actions">
              <button className="secondary-button" type="button" onClick={handleUploadLogo} disabled={isLogoSubmitting || !logoFile}>
                <Upload size={16} aria-hidden="true" />
                <span>Upload Logo</span>
              </button>
              <button className="secondary-button danger" type="button" onClick={handleDeleteLogo} disabled={isLogoSubmitting || !customerLogoUrl}>
                <Trash2 size={16} aria-hidden="true" />
                <span>Delete Logo</span>
              </button>
            </div>
            {logoStatus && <Notice tone="success" message={logoStatus} />}
            {logoError && <Notice tone="error" message={logoError} />}
          </section>
        )}
        <FormField label="Address Line 1">
          <input value={form.addressLine1} onChange={(event) => setForm({ ...form, addressLine1: event.target.value })} maxLength={100} />
        </FormField>
        <FormField label="Address Line 2">
          <input value={form.addressLine2} onChange={(event) => setForm({ ...form, addressLine2: event.target.value })} maxLength={100} />
        </FormField>
        <FormField label="Postcode">
          <input value={form.postcode} onChange={(event) => setForm({ ...form, postcode: event.target.value })} maxLength={20} />
        </FormField>
        <FormField label="City">
          <input value={form.city} onChange={(event) => setForm({ ...form, city: event.target.value })} maxLength={100} />
        </FormField>
        <FormField label="County">
          <input value={form.county} onChange={(event) => setForm({ ...form, county: event.target.value })} maxLength={100} />
        </FormField>
        <div className="time-grid daily-hours-grid">
          {form.operatingHours.map((hours) => (
            <div className="daily-hours-row" key={hours.dayOfWeek}>
              <span className="daily-hours-label">{hours.dayName}</span>
              <label className="checkbox-row">
                <input
                  checked={hours.isClosed}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { isClosed: event.target.checked })}
                  type="checkbox"
                />
                <span>{hours.dayName} Closed</span>
              </label>
              <FormField label={`${hours.dayName} Start`}>
                <input
                  value={hours.startTime ?? ''}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { startTime: event.target.value })}
                  type="time"
                  disabled={hours.isClosed}
                />
              </FormField>
              <FormField label={`${hours.dayName} End`}>
                <input
                  value={hours.endTime ?? ''}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { endTime: event.target.value })}
                  type="time"
                  disabled={hours.isClosed}
                />
              </FormField>
            </div>
          ))}
        </div>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel={isEdit ? 'Update Site' : 'Create Site'} />
      </form>
    </section>
  );
}
// Function summary: Renders the SiteAssignmentsPanel React component and wires its local UI behavior.
function SiteAssignmentsPanel({
  siteId,
  onRequestError
}: Readonly<{ siteId: string; onRequestError: (error: unknown) => void }>) {
  const [assignments, setAssignments] = useState<SiteAssignmentResponse | null>(null);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const loadAssignments = useCallback(async () => {
    try {
      const response = await getSiteAssignments(siteId);
      setAssignments(response.item ?? null);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }, [onRequestError, siteId]);

  useEffect(() => {
    loadAssignments().catch(onRequestError);
  }, [loadAssignments, onRequestError]);
  async function runMutation(action: () => Promise<{ item?: SiteAssignmentResponse | null }>) {
    setIsBusy(true);
    setError(null);
    try {
      const response = await action();
      setAssignments(response.item ?? null);
      setSelectedUserId('');
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
    }
  }
  const assignedColumns = useMemo<DataGridColumn<SiteUserAssignmentItem>[]>(() => [
    {
      key: 'email',
      header: 'User',
      render: (user) => (
        <span className="cell-with-icon">
          <UserRound size={16} aria-hidden="true" />
          {user.email}
        </span>
      )
    },
    { key: 'name', header: 'Name', render: (user) => user.name || 'None' },
    { key: 'companyRole', header: 'Role', render: (user) => user.companyRole || 'None' },
    { key: 'siteContact', header: 'Contact', render: (user) => user.siteContact ? <span className="status-chip">Contact</span> : 'No' }
  ], []);
  return (
    <NestedSection title="Site Users" icon={<UserRound size={18} aria-hidden="true" />}>
      {error && <Notice tone="error" message={error} />}
      {assignments && (
        <>
          <div className="assignment-toolbar">
            <select value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)} disabled={isBusy}>
              <option value="">Select a user</option>
              {assignments.availableUsers.map((user) => (
                <option value={user.id} key={user.id}>
                  {user.email}
                </option>
              ))}
            </select>
            <button
              className="secondary-button"
              type="button"
              disabled={isBusy || !selectedUserId}
              onClick={() => runMutation(() => addUserToSite({ siteId, userId: selectedUserId }))}
            >
              <UserPlus size={17} aria-hidden="true" />
              <span>Add user</span>
            </button>
          </div>
          <DataGrid
            columns={assignedColumns}
            rows={assignments.assignedUsers}
            getRowKey={(user) => user.id}
            emptyMessage="No users are assigned to this site."
            page={1}
            pageSize={Math.max(assignments.assignedUsers.length, 1)}
            total={assignments.assignedUsers.length}
            totalPages={assignments.assignedUsers.length > 0 ? 1 : 0}
            rowActions={[
              {
                label: 'Set site contact',
                icon: <Star size={16} aria-hidden="true" />,
                onClick: (user) => runMutation(() => setSiteContactUser({ siteId, userId: user.id })),
                disabled: (user) => isBusy || user.siteContact
              },
              {
                label: 'Unset site contact',
                icon: <X size={16} aria-hidden="true" />,
                onClick: (user) => runMutation(() => removeSiteContactUser({ siteId, userId: user.id })),
                disabled: (user) => isBusy || !user.siteContact
              },
              {
                label: 'Remove user from site',
                icon: <Trash2 size={16} aria-hidden="true" />,
                onClick: (user) => runMutation(() => removeUserFromSite({ siteId, userId: user.id })),
                disabled: () => isBusy
              }
            ]}
          />
        </>
      )}
    </NestedSection>
  );
}
// Function summary: Renders the NotificationSettingsPanel React component and wires its local UI behavior.
function NotificationSettingsPanel({
  settings,
  canManage,
  currentUserId,
  onUpdated,
  onRequestError
}: NotificationSettingsPanelProps) {
  const visibleSettings = canManage
    ? settings.settings
    : settings.settings.filter((setting) => setting.userId.toLowerCase() === (currentUserId ?? '').toLowerCase());
  const [drafts, setDrafts] = useState<Record<string, SiteNotificationSettingMutationRequest>>({});
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    const nextDrafts: Record<string, SiteNotificationSettingMutationRequest> = {};
    settings.settings.forEach((setting) => {
      nextDrafts[setting.siteUserId] = {
        email: setting.email,
        sms: setting.sms,
        startTime: setting.startTime ?? '',
        endTime: setting.endTime ?? ''
      };
    });
    setDrafts(nextDrafts);
  }, [settings]);
  async function handleSave(setting: SiteNotificationSettingItem) {
    setSavingId(setting.siteUserId);
    setError(null);
    try {
      const draft = drafts[setting.siteUserId] ?? { email: setting.email, sms: setting.sms, startTime: '', endTime: '' };
      const response = await updateSiteNotificationSetting(settings.siteId, setting.siteUserId, {
        email: draft.email,
        sms: draft.sms,
        startTime: draft.startTime || null,
        endTime: draft.endTime || null
      });
      const updatedItem = response.item;
      if (updatedItem) {
        onUpdated({
          ...settings,
          settings: settings.settings.map((item) => item.siteUserId === updatedItem.siteUserId ? updatedItem : item)
        });
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setSavingId(null);
    }
  }
  // Function summary: Updates draft data for the current workflow.
  function updateDraft(siteUserId: string, patch: Partial<SiteNotificationSettingMutationRequest>) {
    setDrafts((current) => ({
      ...current,
      [siteUserId]: {
        ...(current[siteUserId] ?? { email: false, sms: false, startTime: '', endTime: '' }),
        ...patch
      }
    }));
  }
  return (
    <NestedSection title="Notification Settings" icon={<Settings size={18} aria-hidden="true" />}>
      {error && <Notice tone="error" message={error} />}
      {visibleSettings.length === 0 && <Notice tone="info" message="No notification settings are available for this site." />}
      {visibleSettings.length > 0 && (
        <div className="settings-list">
          {visibleSettings.map((setting) => {
            const draft = drafts[setting.siteUserId] ?? {
              email: setting.email,
              sms: setting.sms,
              startTime: setting.startTime ?? '',
              endTime: setting.endTime ?? ''
            };
            return (
              <div className="setting-row" key={setting.siteUserId}>
                <div>
                  <strong>{setting.userName || setting.userEmail}</strong>
                  <span>{setting.siteContact ? 'Site contact' : setting.userEmail}</span>
                </div>
                <label className="checkbox-row">
                  <input checked={draft.email} onChange={(event) => updateDraft(setting.siteUserId, { email: event.target.checked })} type="checkbox" />
                  <span>Email</span>
                </label>
                <label className="checkbox-row">
                  <input checked={draft.sms} onChange={(event) => updateDraft(setting.siteUserId, { sms: event.target.checked })} type="checkbox" />
                  <span>SMS</span>
                </label>
                <input
                  aria-label={`${setting.userEmail} notification start time`}
                  value={draft.startTime ?? ''}
                  onChange={(event) => updateDraft(setting.siteUserId, { startTime: event.target.value })}
                  type="time"
                />
                <input
                  aria-label={`${setting.userEmail} notification end time`}
                  value={draft.endTime ?? ''}
                  onChange={(event) => updateDraft(setting.siteUserId, { endTime: event.target.value })}
                  type="time"
                />
                <button className="secondary-button" type="button" onClick={() => handleSave(setting)} disabled={savingId === setting.siteUserId}>
                  <Save size={17} aria-hidden="true" />
                  <span>{savingId === setting.siteUserId ? 'Saving' : 'Save'}</span>
                </button>
              </div>
            );
          })}
        </div>
      )}
    </NestedSection>
  );
}
// Function summary: Renders the NestedSection React component and wires its local UI behavior.
function NestedSection({ title, icon, children }: Readonly<{ title: string; icon: ReactNode; children: ReactNode }>) {
  return (
    <section className="nested-section">
      <div className="section-heading">
        {icon}
        <h3>{title}</h3>
      </div>
      {children}
    </section>
  );
}
// Function summary: Renders the ReadOnlyMetric React component and wires its local UI behavior.
function ReadOnlyMetric({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="metric compact-metric">
      <CalendarDays size={18} aria-hidden="true" />
      <div>
        <strong>{value}</strong>
        <span>{label}</span>
      </div>
    </div>
  );
}
// Function summary: Renders the ReadOnlyRow React component and wires its local UI behavior.
function ReadOnlyRow({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="readonly-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
// Function summary: Handles the parse contracts mode workflow for this module.
function parseContractsMode(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (path === '/contracts/new') {
    return { kind: 'create' as const };
  }
  const edit = /^\/contracts\/([^/]+)\/edit$/i.exec(path);
  if (edit) {
    return { kind: 'edit' as const, contractId: edit[1] };
  }
  const detail = /^\/contracts\/([^/]+)$/i.exec(path);
  if (detail) {
    return { kind: 'detail' as const, contractId: detail[1] };
  }
  return { kind: 'list' as const };
}
// Function summary: Handles the parse sites mode workflow for this module.
function parseSitesMode(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (path === '/sites/new') {
    return { kind: 'create' as const };
  }
  const edit = /^\/sites\/([^/]+)\/edit$/i.exec(path);
  if (edit) {
    return { kind: 'edit' as const, siteId: edit[1] };
  }
  const detail = /^\/sites\/([^/]+)$/i.exec(path);
  if (detail) {
    return { kind: 'detail' as const, siteId: detail[1] };
  }
  return { kind: 'list' as const };
}
// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
// Function summary: Handles the normalize sort direction workflow for this module.
function normalizeSortDirection(value: string | null): SortDirection {
  return value?.toLowerCase() === 'descending' || value?.toLowerCase() === 'desc' ? 'Descending' : 'Ascending';
}
// Function summary: Builds contracts url data for callers.
function buildContractsUrl(options: { searchText: string; page: number; sort: string; sortDir: SortDirection }) {
  const params = new URLSearchParams();
  if (options.searchText) {
    params.set('q', options.searchText);
  }
  if (options.page > 1) {
    params.set('page', String(options.page));
  }
  if (options.sort !== 'contractNumber') {
    params.set('sort', options.sort);
  }
  if (options.sortDir !== 'Ascending') {
    params.set('sortDir', options.sortDir);
  }
  const query = params.toString();
  return query ? `/contracts?${query}` : '/contracts';
}
// Function summary: Builds sites url data for callers.
function buildSitesUrl(options: { searchText: string; includeArchived: boolean; page: number; sort: string; sortDir: SortDirection }) {
  const params = new URLSearchParams();
  if (options.searchText) {
    params.set('q', options.searchText);
  }
  if (options.includeArchived) {
    params.set('archived', 'true');
  }
  if (options.page > 1) {
    params.set('page', String(options.page));
  }
  if (options.sort !== 'createDate') {
    params.set('sort', options.sort);
  }
  if (options.sortDir !== 'Descending') {
    params.set('sortDir', options.sortDir);
  }
  const query = params.toString();
  return query ? `/sites?${query}` : '/sites';
}

// Function summary: Converts current site monitors into reusable map markers.
function siteMonitorMarkers(site: SiteDetailResponse): MapMonitorMarker[] {
  return site.monitors
    .filter((monitor) => typeof monitor.lat === 'number' && typeof monitor.lng === 'number')
    .map((monitor) => ({
      monitorId: monitor.id,
      deploymentId: monitor.deploymentId,
      latitude: monitor.lat as number,
      longitude: monitor.lng as number,
      typeOfMonitor: monitor.typeOfMonitor,
      offline: monitor.offLine,
      alert: site.openNotifications.some((notification) => notification.monitorId === monitor.id && notification.alertType === 'Alert'),
      caution: site.openNotifications.some((notification) => notification.monitorId === monitor.id && notification.alertType === 'Caution'),
      siteName: site.siteName,
      fleetNumber: monitor.fleetNumber,
      serialId: monitor.serialId ?? '',
      lastDataTime: monitor.lastDataTime,
      what3words: monitor.what3words ?? ''
    }));
}

// Function summary: Handles the format date workflow for this module.
function formatDate(value?: string | null) {
  if (!value) {
    return '';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat('en-GB').format(date);
}
// Function summary: Handles the format date time workflow for this module.
function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'short', timeStyle: 'short' }).format(date);
}
// Function summary: Maps date input into the shape required by callers.
function toDateInput(value?: string | null) {
  if (!value) {
    return '';
  }
  return value.slice(0, 10);
}
// Function summary: Handles the format time range workflow for this module.
function formatTimeRange(start?: string | null, end?: string | null) {
  return start && end ? `${start} - ${end}` : 'Not set';
}

// Function summary: Normalizes API and legacy site-hour values into the seven-day editor/detail model.
function normalizeOperatingHours(
  operatingHours?: SiteOperatingHours[] | null,
  legacy?: {
    startTime?: string | null;
    endTime?: string | null;
    satStartTime?: string | null;
    satEndTime?: string | null;
    sunStartTime?: string | null;
    sunEndTime?: string | null;
  } | null
) {
  const byDay = new Map((operatingHours ?? []).map((hours) => [hours.dayOfWeek, hours]));
  return siteOperatingDays.map((day) => {
    const existing = byDay.get(day.dayOfWeek);
    if (existing) {
      return {
        ...day,
        ...existing,
        startTime: existing.startTime ?? '',
        endTime: existing.endTime ?? ''
      };
    }
    return legacyOperatingHours(day, legacy);
  });
}

// Function summary: Converts the older weekday/Saturday/Sunday fields into one per-day operating-hours row.
function legacyOperatingHours(day: SiteOperatingHours, legacy?: {
  startTime?: string | null;
  endTime?: string | null;
  satStartTime?: string | null;
  satEndTime?: string | null;
  sunStartTime?: string | null;
  sunEndTime?: string | null;
} | null) {
  if (!legacy) {
    return { ...day };
  }
  if (day.dayOfWeek === 6) {
    return { ...day, startTime: legacy.satStartTime ?? '', endTime: legacy.satEndTime ?? '' };
  }
  if (day.dayOfWeek === 7) {
    return { ...day, startTime: legacy.sunStartTime ?? '', endTime: legacy.sunEndTime ?? '' };
  }
  return { ...day, startTime: legacy.startTime ?? '', endTime: legacy.endTime ?? '' };
}

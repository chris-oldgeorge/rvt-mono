// File summary: Renders the sites route panel, dispatching between the site list, detail, and form views.
// Major updates:
// - 2026-07-30 pending Split from ContractSitePanels.tsx so contract and site panels live in separate modules.

import { Edit3, Eye, MapPinned, Plus, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { isAbortError, querySites } from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { currentRoutePath, withReturnTo } from '../navigation';
import { normalizeSortDirection, parsePositiveInt, useGridSortHandler } from '../gridQuery';
import { pageSize } from './panelShared';
import type { ListExecution, OperationsRouteProps } from './panelShared';
import { SiteDetailPanel } from './SiteDetailPanel';
import { SiteFormPanel } from './SiteFormPanel';
import type { QuerySitesRequest, SiteListItem, SortDirection } from '../dtos';

type SitesPanelProps = OperationsRouteProps &
  Readonly<{
    canManage?: boolean;
    currentUserId?: string | null;
  }>;

type SiteListPanelProps = OperationsRouteProps &
  Readonly<{
    canManage?: boolean;
  }>;

// Function summary: Renders the SitesPanel React component and wires its local UI behavior.
export function SitesPanel({
  locationPath,
  onNavigate,
  onRequestError,
  canManage = false,
  currentUserId,
}: SitesPanelProps) {
  const mode = parseSitesMode(locationPath);
  if (mode.kind === 'create' && canManage) {
    return <SiteFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit' && canManage) {
    return (
      <SiteFormPanel
        siteId={mode.siteId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
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
  return (
    <SiteListPanel
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
      canManage={canManage}
    />
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
  const [sortDir, setSortDir] = useState<SortDirection>(
    normalizeSortDirection(initialParams.get('sortDir'), 'Descending'),
  );
  const [error, setError] = useState<string | null>(null);
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QuerySitesRequest> | null>(null);
  const columns = useMemo<DataGridColumn<SiteListItem>[]>(
    () => [
      {
        key: 'siteName',
        header: 'Site',
        sortable: true,
        render: (site) => (
          <span className="cell-with-icon">
            <MapPinned size={16} aria-hidden="true" />
            {site.siteName}
          </span>
        ),
      },
      { key: 'companyName', header: 'Company', sortable: true, render: (site) => site.companyName || 'None' },
      { key: 'contracts', header: 'Contracts', sortable: true, render: (site) => site.contracts || 'None' },
      { key: 'siteAddress', header: 'Address', sortable: true, render: (site) => site.siteAddress || 'None' },
      { key: 'monitorCount', header: 'Monitors', align: 'end', render: (site) => site.monitorCount },
      {
        key: 'openNotificationCount',
        header: 'Open Alerts',
        align: 'end',
        render: (site) => site.openNotificationCount,
      },
      {
        key: 'archived',
        header: 'State',
        render: (site) =>
          site.archived ? (
            <span className="status-chip muted">Archived</span>
          ) : (
            <span className="status-chip">Active</span>
          ),
      },
    ],
    [],
  );
  const query = useMemo<QuerySitesRequest>(
    () => ({
      searchText,
      includeArchived,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
    }),
    [includeArchived, page, searchText, sortDir, sortKey],
  );
  const execution = useMemo<ListExecution<QuerySitesRequest>>(() => ({ query }), [query]);
  const isLoading = completedExecution !== execution;
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const returnPath = currentRoutePath(locationPath);
  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(
      null,
      '',
      buildSitesUrl({ searchText, includeArchived, page, sort: sortKey, sortDir }),
    );
    querySites(execution.query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setSites(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
        setCompletedExecution(execution);
      })
      .catch((err: Error) => {
        if (controller.signal.aborted || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedExecution(execution);
      });
    return () => controller.abort();
  }, [execution, includeArchived, onRequestError, page, searchText, sortDir, sortKey]);
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
          <button
            className="secondary-button"
            type="button"
            onClick={() => onNavigate(withReturnTo('/sites/new', returnPath))}
          >
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
          <input
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.target.checked);
              setPage(1);
            }}
            type="checkbox"
          />
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
            onClick: (site) => onNavigate(withReturnTo(`/sites/${site.id}`, returnPath)),
          },
          {
            label: 'Edit site',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (site) => onNavigate(withReturnTo(`/sites/${site.id}/edit`, returnPath)),
            disabled: (site) => !canManage || site.archived,
          },
        ]}
      />
    </section>
  );
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

// Function summary: Builds sites url data for callers.
function buildSitesUrl(options: {
  searchText: string;
  includeArchived: boolean;
  page: number;
  sort: string;
  sortDir: SortDirection;
}) {
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

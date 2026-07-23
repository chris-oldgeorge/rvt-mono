// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-07-08 pending Moved map and calendar route panels into a lazy-loaded module so dashboard stays in the initial bundle.
// - 2026-06-26 pending Added cancellation for dashboard summary, search, map, and calendar requests.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-06-08 pending Added legacy Site Search widget to the SPA dashboard.
// - 2026-06-08 pending Made dashboard Site Search live and added site row actions.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-09 pending Reused shared monitor map component for embedded detail parity.

import {
  Activity,
  Bell,
  CalendarDays,
  Edit3,
  Eye,
  Gauge,
  MapPin,
  RefreshCcw,
  Search,
  ShieldAlert
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  getDashboardSummary,
  isAbortError,
  queryBreachesAlerts,
  querySites
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { Notice } from '../components/FormControls';
import { localDateInputValue } from '../localDate';
import type {
  AuthStateResponse,
  BreachesAlertsItem,
  BreachesAlertsResponse,
  DashboardNotificationItem,
  DashboardSummaryResponse,
  QuerySitesRequest,
  SiteListItem,
  SortDirection
} from '../dtos';

const roleNames = {
  masterAdmin: 'RVTMasterAdmin',
  installer: 'RVTInstaller',
  companyUser: 'CompanyUser'
} as const;

type DashboardPanelProps = Readonly<{
  auth: AuthStateResponse;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

const siteSearchPageSize = 50;

// Function summary: Renders the DashboardPanel React component and wires its local UI behavior.
export function DashboardPanel({ auth, onNavigate, onRequestError }: DashboardPanelProps) {
  const [summary, setSummary] = useState<DashboardSummaryResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const userRoles = auth.user?.roles ?? [];
  const isMasterAdmin = userRoles.includes(roleNames.masterAdmin);
  const isInstaller = userRoles.includes(roleNames.installer);

  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    getDashboardSummary({ signal: controller.signal })
      .then((response) => {
        setSummary(response);
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
  }, [onRequestError]);

  if (isLoading && !summary) {
    return <LoadingPanel label="Loading dashboard" />;
  }

  if (error && !summary) {
    return <Notice tone="error" message={error} />;
  }

  if (!summary) {
    return null;
  }

  return (
    <section className="dashboard-overview" aria-label="Dashboard">
      <div className="dashboard-grid">
        <DashboardMetric label="Assigned" value={summary.monitorCounts.assigned} tone="neutral" />
        <DashboardMetric label="Online" value={summary.monitorCounts.online} tone="success" />
        <DashboardMetric label="Offline" value={summary.monitorCounts.offline} tone="danger" />
        <DashboardMetric label="New" value={summary.monitorCounts.new} tone="neutral" />
        <DashboardMetric label="Not In Use" value={summary.monitorCounts.notUsed} tone="muted" />
        <DashboardMetric label="Open Alerts" value={summary.openAlerts} tone="danger" />
        <DashboardMetric label="Open Cautions" value={summary.openCautions} tone="warning" />
      </div>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>{dashboardAudience(summary.role)}</p>
            <h2>Live Monitor Overview</h2>
          </div>
          <Gauge size={22} aria-hidden="true" />
        </div>
        <div className="button-row align-left">
          {!isInstaller && (
            <>
              <button className="secondary-button" type="button" onClick={() => onNavigate('/maps')}>
                <MapPin size={17} aria-hidden="true" />
                <span>Open Maps</span>
              </button>
              <button className="secondary-button" type="button" onClick={() => onNavigate('/calendar')}>
                <CalendarDays size={17} aria-hidden="true" />
                <span>Open Calendar</span>
              </button>
            </>
          )}
          <button className="secondary-button" type="button" onClick={() => onNavigate('/monitors')}>
            <Activity size={17} aria-hidden="true" />
            <span>Open Monitors</span>
          </button>
          {!isInstaller && (
            <button className="secondary-button" type="button" onClick={() => onNavigate('/notifications')}>
              <Bell size={17} aria-hidden="true" />
              <span>Open Notifications</span>
            </button>
          )}
        </div>
      </section>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>Open notifications</p>
            <h2>Recent Activity</h2>
          </div>
          <Bell size={22} aria-hidden="true" />
        </div>
        <NotificationList notifications={summary.recentNotifications} />
      </section>
      {!isInstaller && <DashboardSiteSearch onNavigate={onNavigate} onRequestError={onRequestError} />}
      {isMasterAdmin && <BreachesAlertsWidget onRequestError={onRequestError} />}
    </section>
  );
}

// Function summary: Renders the legacy dashboard Site Search widget for admin and company users.
function DashboardSiteSearch({
  onNavigate,
  onRequestError
}: Readonly<{ onNavigate: (path: string) => void; onRequestError: (error: unknown) => void }>) {
  const [sites, setSites] = useState<SiteListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState('');
  const [page, setPage] = useState(1);
  const [sortKey, setSortKey] = useState('siteName');
  const [sortDir, setSortDir] = useState<SortDirection>('Ascending');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const columns = useMemo<DataGridColumn<SiteListItem>[]>(() => [
    { key: 'contracts', header: 'Contracts', sortable: true, render: (site) => site.contracts || 'None' },
    { key: 'siteName', header: 'Site Name', sortable: true, render: (site) => site.siteName || 'None' },
    { key: 'siteAddress', header: 'Address', sortable: true, render: (site) => site.siteAddress || 'None' },
    { key: 'companyName', header: 'Company Name', sortable: true, render: (site) => site.companyName || 'None' }
  ], []);
  const query = useMemo<QuerySitesRequest>(() => ({
    searchText,
    includeArchived: false,
    page,
    pageSize: siteSearchPageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);
  const handleSortChange = useCallback((key: string, direction: GridSortDirection) => {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
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
  }, [onRequestError, query]);

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Legacy dashboard</p>
          <h2>Site Search</h2>
        </div>
        <Search size={22} aria-hidden="true" />
      </div>
      <div className="toolbar-row site-search-form">
        <label className="search-box">
          <Search size={18} aria-hidden="true" />
          <input value={searchText} onChange={(event) => { setSearchText(event.target.value); setPage(1); }} placeholder="Search sites" />
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
        pageSize={siteSearchPageSize}
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
            onClick: (site) => onNavigate(`/sites/${site.id}`)
          },
          {
            label: 'Edit site',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (site) => onNavigate(`/sites/${site.id}/edit`),
            disabled: (site) => site.archived
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the BreachesAlertsWidget React component and wires its local UI behavior.
function BreachesAlertsWidget({ onRequestError }: Readonly<{ onRequestError: (error: unknown) => void }>) {
  const [date, setDate] = useState(todayInputValue());
  const [response, setResponse] = useState<BreachesAlertsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    queryBreachesAlerts({ date, page: 1, pageSize: 8, sort: 'notificationTime', sortDir: 'Descending' }, { signal: controller.signal })
      .then((nextResponse) => {
        if (controller.signal.aborted) {
          return;
        }
        setResponse(nextResponse);
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
  }, [date, onRequestError]);

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Master admin</p>
          <h2>Breaches And Alerts</h2>
        </div>
        <ShieldAlert size={22} aria-hidden="true" />
      </div>
      <label className="form-field compact-date">
        <span>Date</span>
        <input value={date} type="date" onChange={(event) => setDate(event.target.value)} />
      </label>
      {error && <Notice tone="error" message={error} />}
      {isLoading && <LoadingInline label="Loading breaches" />}
      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Monitor</th>
              <th>Time</th>
              <th className="align-end">X</th>
              <th className="align-end">Y</th>
              <th className="align-end">Z</th>
            </tr>
          </thead>
          <tbody>
            {(response?.results ?? []).map((item) => (
              <BreachesAlertsRow item={item} key={item.notificationId ?? `${item.monitorId}-${item.notificationTime}`} />
            ))}
            {response?.results.length === 0 && (
              <tr>
                <td colSpan={5}>No vibration breaches found for this date.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

// Function summary: Renders the NotificationList React component and wires its local UI behavior.
function NotificationList({ notifications }: Readonly<{ notifications: ReadonlyArray<DashboardNotificationItem> }>) {
  if (notifications.length === 0) {
    return <p className="muted-text">No open notifications in this view.</p>;
  }

  return (
    <div className="notification-stack">
      {notifications.map((notification) => (
        <div className="notification-card" key={notification.id}>
          <span className={`status-chip ${notificationTone(notification)}`}>{notification.alertType}</span>
          <strong>{notification.fleetNumber || notification.serialId}</strong>
          <span>{notification.alertField} / {formatNumber(notification.level)}</span>
          <time>{formatDateTime(notification.notificationTime)}</time>
        </div>
      ))}
    </div>
  );
}

// Function summary: Renders the DashboardMetric React component and wires its local UI behavior.
function DashboardMetric({ label, value, tone }: Readonly<{ label: string; value: number; tone: string }>) {
  return (
    <article className={`metric dashboard-metric ${tone}`}>
      <strong>{value}</strong>
      <span>{label}</span>
    </article>
  );
}

// Function summary: Renders the BreachesAlertsRow React component and wires its local UI behavior.
function BreachesAlertsRow({ item }: Readonly<{ item: BreachesAlertsItem }>) {
  return (
    <tr>
      <td>{item.fleetNumber || item.serialId || 'Monitor'}</td>
      <td>{formatDateTime(item.notificationTime)}</td>
      <td className="align-end">{formatOptionalNumber(item.xvtop)}</td>
      <td className="align-end">{formatOptionalNumber(item.yvtop)}</td>
      <td className="align-end">{formatOptionalNumber(item.zvtop)}</td>
    </tr>
  );
}

// Function summary: Renders the LoadingPanel React component and wires its local UI behavior.
function LoadingPanel({ label }: Readonly<{ label: string }>) {
  return (
    <section className="panel placeholder-panel">
      <RefreshCcw size={22} aria-hidden="true" />
      <p>{label}</p>
    </section>
  );
}

// Function summary: Renders the LoadingInline React component and wires its local UI behavior.
function LoadingInline({ label }: Readonly<{ label: string }>) {
  return (
    <div className="loading-inline">
      <RefreshCcw size={16} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}

// Function summary: Handles the notification tone workflow for this module.
function notificationTone(notification: DashboardNotificationItem) {
  if (notification.alertType === 'Alert') {
    return 'danger';
  }

  return 'neutral';
}

// Function summary: Handles the dashboard audience workflow for this module.
function dashboardAudience(role: string) {
  if (role === roleNames.masterAdmin) {
    return 'Master admin dashboard';
  }
  if (role === roleNames.installer) {
    return 'Installer dashboard';
  }
  if (role === roleNames.companyUser) {
    return 'Company dashboard';
  }

  return 'RVT admin dashboard';
}

// Function summary: Maps day input value into the shape required by callers.
function todayInputValue() {
  return localDateInputValue();
}

// Function summary: Handles the format date time workflow for this module.
function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }

  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

// Function summary: Handles the format number workflow for this module.
function formatNumber(value: number) {
  return new Intl.NumberFormat('en-GB', { maximumFractionDigits: 2 }).format(value);
}

// Function summary: Handles the format optional number workflow for this module.
function formatOptionalNumber(value?: number | null) {
  if (typeof value !== 'number') {
    return 'n/a';
  }

  return formatNumber(value);
}

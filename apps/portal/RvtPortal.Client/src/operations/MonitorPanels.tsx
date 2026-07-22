// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-29 pending Shared monitor search reset helper and optional-chain cleanup for Sonar maintainability.
// - 2026-06-26 pending Added cancellation for monitor list and unattached monitor requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for monitor edit/deployment forms.
// - 2026-06-08 pending Added admin unattached monitor removal panel.
// - 2026-06-09 pending Added legacy detail summaries, picture upload, and notification drill-through.
// - 2026-06-09 pending Preserved blank deployment coordinates as null instead of zero.
// - 2026-06-09 pending Embedded protected monitor map context and metric source details.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  Bell,
  BarChart3,
  CheckCircle2,
  Edit3,
  Eye,
  Gauge,
  Image,
  MapPinned,
  Plus,
  RefreshCcw,
  Save,
  Search,
  SlidersHorizontal,
  Trash2,
  Upload,
  Wrench
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  addDefaultMonitorAlertLevels,
  addMonitorToContract,
  convertWhat3Words,
  getInstallerMonitor,
  getInstallerMonitorStatus,
  getMonitor,
  getMonitorAssignment,
  isAbortError,
  queryInstallerMonitors,
  queryMonitors,
  queryUnattachedMonitors,
  removeMonitorFromContract,
  removeUnattachedMonitor,
  updateInstallerDeployment,
  updateMonitor,
  uploadMonitorPicture
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { MonitorMap, MonitorMarkerList } from '../components/MonitorMap';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { safeHref } from '../safeUrl';
import { AlertLevelsPanel } from './NotificationAlertPanels';
import type {
  DefaultMonitorsResponse,
  InstallerMonitorStatusResponse,
  MonitorAlertLevelItem,
  MonitorAssignmentContextResponse,
  MonitorDetailResponse,
  MonitorListItem,
  MapMonitorMarker,
  MonitorListState,
  MonitorMetricSummary,
  MonitorMutationRequest,
  MonitorNotificationItem,
  QueryMonitorsRequest,
  SortDirection,
  UnattachedMonitorListItem
} from '../dtos';

const pageSize = 10;

type MonitorsPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
  canManage?: boolean;
  canUseInstallerTools?: boolean;
  installerOnly?: boolean;
}>;

type MonitorRoute =
  | { kind: 'list' }
  | { kind: 'detail'; monitorId: string }
  | { kind: 'edit'; monitorId: string }
  | { kind: 'installer'; monitorId: string }
  | { kind: 'alert-levels'; monitorId: string }
  | { kind: 'unattached' }
  | { kind: 'assignment'; siteId: string; contractId?: string | null };

// Function summary: Renders the MonitorsPanel React component and wires its local UI behavior.
export function MonitorsPanel({
  locationPath,
  onNavigate,
  onRequestError,
  canManage = false,
  canUseInstallerTools = false,
  installerOnly = false
}: MonitorsPanelProps) {
  const mode = parseMonitorRoute(locationPath);
  if (mode.kind === 'unattached' && canManage) {
    return <UnattachedMonitorRemovalPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'detail') {
    return (
      <MonitorDetailPanel
        monitorId={mode.monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
        canManage={canManage}
        canUseInstallerTools={canUseInstallerTools}
        installerOnly={installerOnly}
      />
    );
  }
  if (mode.kind === 'edit' && canManage) {
    return <MonitorEditPanel monitorId={mode.monitorId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'installer' && canUseInstallerTools) {
    return <InstallerDeploymentPanel monitorId={mode.monitorId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'alert-levels' && !installerOnly) {
    return (
      <AlertLevelsPanel
        monitorId={mode.monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
        canManage={canManage}
      />
    );
  }
  if (mode.kind === 'assignment' && canManage) {
    return (
      <MonitorAssignmentPanel
        siteId={mode.siteId}
        contractId={mode.contractId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  return (
    <MonitorListPanel
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
      canManage={canManage}
      canUseInstallerTools={canUseInstallerTools}
      installerOnly={installerOnly}
    />
  );
}

// Function summary: Renders the MonitorListPanel React component and wires its local UI behavior.
function MonitorListPanel({
  locationPath,
  onNavigate,
  onRequestError,
  canManage,
  canUseInstallerTools,
  installerOnly
}: MonitorsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const tabs = useMemo(() => monitorTabs(Boolean(canManage), Boolean(installerOnly)), [canManage, installerOnly]);
  const [state, setState] = useState<MonitorListState>(() => normalizeState(initialParams.get('state'), tabs[0].state));
  const [monitors, setMonitors] = useState<MonitorListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'fleetNumber');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isAddingDefaults, setIsAddingDefaults] = useState(false);
  const columns = useMemo<DataGridColumn<MonitorListItem>[]>(() => [
    {
      key: 'fleetNumber',
      header: 'Fleet',
      sortable: true,
      render: (monitor) => (
        <span className="cell-with-icon">
          <Gauge size={16} aria-hidden="true" />
          {monitor.fleetNumber || 'Unassigned'}
        </span>
      )
    },
    { key: 'serialId', header: 'Serial', sortable: true, render: (monitor) => monitor.serialId },
    { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (monitor) => monitor.typeOfMonitor },
    { key: 'siteName', header: 'Site', sortable: true, render: (monitor) => monitor.siteName || 'Not deployed' },
    { key: 'contractNumber', header: 'Contract', sortable: true, render: (monitor) => monitor.contractNumber || 'None' },
    { key: 'online', header: 'Online', render: (monitor) => monitor.isOffline ? 'No' : 'Yes' },
    { key: 'alerts', header: 'Alerts', render: (monitor) => monitor.hasAlerts ? 'Yes' : 'No' },
    { key: 'cautions', header: 'Cautions', render: (monitor) => monitor.hasCautions ? 'Yes' : 'No' },
    {
      key: 'lastDataTime',
      header: 'Status',
      sortable: true,
      render: (monitor) => <MonitorStatusBadge monitor={monitor} />
    }
  ], []);
  const query = useMemo<QueryMonitorsRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir,
    state
  }), [page, searchText, sortDir, sortKey, state]);
  const returnPath = currentRoutePath(locationPath);

  useEffect(() => {
    if (!tabs.some((tab) => tab.state === state)) {
      setState(tabs[0].state);
    }
  }, [state, tabs]);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildMonitorsUrl({ searchText, page, sort: sortKey, sortDir, state }));
    setIsLoading(true);
    const load = installerOnly ? queryInstallerMonitors : queryMonitors;
    load(query, { signal: controller.signal })
      .then((response) => {
        setMonitors(response.results);
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
  }, [installerOnly, onRequestError, page, query, searchText, sortDir, sortKey, state]);

  // Function summary: Handles the handle state workflow for this module.
  function handleState(nextState: MonitorListState) {
    setState(nextState);
    setPage(1);
  }

  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    resetSearchPage(value, setSearchText, setPage);
  }

  // Function summary: Handles the handle sort change workflow for this module.
  function handleSortChange(key: string, direction: GridSortDirection) {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }

  async function handleDefaultLevels() {
    setIsAddingDefaults(true);
    setNotice(null);
    try {
      const response: DefaultMonitorsResponse = await addDefaultMonitorAlertLevels();
      setNotice(`Processed ${response.processed} monitors and created ${response.createdAlertLevels} alert levels.`);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsAddingDefaults(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Operations</p>
          <h2>Monitors</h2>
        </div>
        {canManage && (
          <div className="button-row">
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/monitors/unattached', returnPath))}>
              <Trash2 size={17} aria-hidden="true" />
              <span>Unattached</span>
            </button>
            <button className="secondary-button" type="button" onClick={handleDefaultLevels} disabled={isAddingDefaults}>
              <RefreshCcw size={17} aria-hidden="true" />
              <span>{isAddingDefaults ? 'Adding defaults' : 'Default Alerts'}</span>
            </button>
          </div>
        )}
      </div>
      <div className="segmented-control" role="tablist" aria-label="Monitor list states">
        {tabs.map((tab) => (
          <button
            className={state === tab.state ? 'active' : ''}
            type="button"
            role="tab"
            aria-selected={state === tab.state}
            key={tab.state}
            onClick={() => handleState(tab.state)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search monitors" />
      </label>
      {notice && <Notice tone="success" message={notice} />}
      <DataGrid
        columns={columns}
        rows={monitors}
        getRowKey={(monitor) => monitor.id}
        emptyMessage="No monitors match the current filters."
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
            label: 'View monitor',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}`, returnPath))
          },
          {
            label: 'Edit monitor',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}/edit`, returnPath)),
            disabled: (monitor) => !canManage || !monitor.canEdit
          },
          {
            label: 'Installer edit',
            icon: <Wrench size={16} aria-hidden="true" />,
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}/installer`, returnPath)),
            disabled: (monitor) => !canUseInstallerTools || !monitor.canInstallerEdit
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the MonitorDetailPanel React component and wires its local UI behavior.
function MonitorDetailPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage,
  canUseInstallerTools,
  installerOnly
}: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
  const [monitor, setMonitor] = useState<MonitorDetailResponse | null>(null);
  const [status, setStatus] = useState<InstallerMonitorStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const backPath = returnToOr(locationPath, '/monitors');
  const detailPath = currentRoutePath(locationPath);
  useEffect(() => {
    const load = installerOnly ? getInstallerMonitor : getMonitor;
    load(monitorId)
      .then((response) => setMonitor(response.item ?? null))
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [installerOnly, monitorId, onRequestError]);
  useEffect(() => {
    if (!canUseInstallerTools) {
      return;
    }
    getInstallerMonitorStatus(monitorId)
      .then(setStatus)
      .catch(() => setStatus(null));
  }, [canUseInstallerTools, monitorId]);

  async function handleRemoveAssignment() {
    setIsRemoving(true);
    try {
      await removeMonitorFromContract(monitorId);
      onNavigate('/monitors?state=not-in-use');
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsRemoving(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Monitor</p>
          <h2>{monitor?.fleetNumber || monitor?.serialId || 'Loading monitor'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
          {canManage && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/edit`, detailPath))} disabled={!monitor}>
              <Edit3 size={17} aria-hidden="true" />
              <span>Edit</span>
            </button>
          )}
          {canUseInstallerTools && monitor?.canInstallerEdit && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/installer`, detailPath))}>
              <Wrench size={17} aria-hidden="true" />
              <span>Deployment</span>
            </button>
          )}
          {canManage && monitor?.siteId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/assign?siteId=${monitor.siteId}`, detailPath))}>
              <SlidersHorizontal size={17} aria-hidden="true" />
              <span>Assignments</span>
            </button>
          )}
          {canManage && monitor?.isAssigned && (
            <button className="danger-button" type="button" onClick={handleRemoveAssignment} disabled={isRemoving}>
              <Trash2 size={17} aria-hidden="true" />
              <span>{isRemoving ? 'Removing' : 'Remove'}</span>
            </button>
          )}
          {!installerOnly && monitor && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels`, detailPath))}>
              <Bell size={17} aria-hidden="true" />
              <span>Alert Levels</span>
            </button>
          )}
          {!installerOnly && monitor?.deploymentId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/data?deploymentId=${monitor.deploymentId}`, detailPath))}>
              <BarChart3 size={17} aria-hidden="true" />
              <span>Data</span>
            </button>
          )}
          {!installerOnly && monitor?.siteId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/maps?siteId=${monitor.siteId}`, detailPath))}>
              <MapPinned size={17} aria-hidden="true" />
              <span>Map</span>
            </button>
          )}
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {monitor && (
        <>
          <div className="detail-grid legacy-monitor-summary">
            <MonitorMetricCard title="Latest Reading" metric={monitor.latestReading} fallback="No recent reading" />
            <MonitorMetricCard title="Latest Average" metric={monitor.latestAverage} fallback="No average recorded" />
            <MonitorMetricCard title="Latest Battery Level" metric={monitor.latestBattery} fallback="Not recorded" />
          </div>
          <div className="detail-grid monitor-detail-grid">
            <DetailItem label="Serial" value={monitor.serialId} />
            <DetailItem label="Type" value={monitor.typeOfMonitor} />
            <DetailItem label="Manufacturer" value={monitor.manufacturer} />
            <DetailItem label="Model" value={monitor.model} />
            <DetailItem label="Firmware" value={monitor.firmwareVersion} />
            <DetailItem label="Calibration" value={formatDate(monitor.calibrationDate) || 'Not recorded'} />
            <DetailItem label="Calibration Due" value={formatDate(monitor.calibrationDue) || 'Not recorded'} />
            <DetailItem label="Site" value={monitor.siteName || 'Not deployed'} />
            <DetailItem label="Contract" value={monitor.contractNumber || 'None'} />
            <DetailItem label="Last Data" value={formatDateTime(monitor.lastDataTime) || 'No data'} />
            <DetailItem label="Status" value={status?.status || monitor.statusLabel || (monitor.isOffline ? 'Offline' : 'Online')} />
            <DetailItem label="Location" value={monitor.location || 'Not recorded'} />
            <DetailItem label="What3words" value={monitor.what3words || 'Not recorded'} />
            <DetailItem label="Coordinates" value={formatCoordinates(monitor.lat, monitor.lng)} />
          </div>
          {monitor.pictureLink && (
            <section className="subsection">
              <div className="subsection-heading">
                <Image size={18} aria-hidden="true" />
                <h3>Location Picture</h3>
              </div>
              <img className="monitor-location-image" src={monitor.pictureLink} alt="Monitor location" />
            </section>
          )}
          {monitorDetailMarkers(monitor).length > 0 && (
            <section className="subsection">
              <div className="subsection-heading">
                <MapPinned size={18} aria-hidden="true" />
                <h3>Location Map</h3>
              </div>
              <MonitorMap markers={monitorDetailMarkers(monitor)} label="Monitor detail map" />
              <MonitorMarkerList markers={monitorDetailMarkers(monitor)} />
            </section>
          )}
          {monitor.deploymentSummary && (
            <section className="subsection">
              <div className="subsection-heading">
                <MapPinned size={18} aria-hidden="true" />
                <h3>Deployment Details</h3>
              </div>
              <div className="detail-grid">
                <DetailItem label="Contract" value={monitor.deploymentSummary.contractNumber || 'None'} />
                <DetailItem label="On Hire Date" value={formatDate(monitor.deploymentSummary.onHireDate)} />
                <DetailItem label="Off Hire Date" value={formatDate(monitor.deploymentSummary.offHireDate) || 'Open'} />
                <DetailItem label="Site" value={monitor.deploymentSummary.siteName || 'None'} />
                <DetailItem label="Company" value={monitor.deploymentSummary.companyName || 'None'} />
                <DetailItem label="Added" value={formatDate(monitor.deploymentSummary.addedDate)} />
              </div>
            </section>
          )}
          <section className="subsection">
            <div className="subsection-heading">
              <Edit3 size={18} aria-hidden="true" />
              <h3>Monitor Notes</h3>
            </div>
            <p className="muted-text">{monitor.monitorNotes || 'No notes for this monitor'}</p>
          </section>
          <section className="subsection">
            <div className="subsection-heading">
              <Bell size={18} aria-hidden="true" />
              <h3>Alert Levels</h3>
            </div>
            <DataGrid
              columns={alertLevelColumns}
              rows={monitor.alertLevels}
              getRowKey={(level) => level.id}
              emptyMessage="No alert levels are configured for this monitor."
              page={1}
              pageSize={Math.max(monitor.alertLevels.length, 1)}
              total={monitor.alertLevels.length}
              totalPages={monitor.alertLevels.length > 0 ? 1 : 0}
            />
          </section>
          <section className="subsection">
            <div className="subsection-heading">
              <Bell size={18} aria-hidden="true" />
              <h3>Recent Notifications</h3>
            </div>
            <DataGrid
              columns={notificationColumns}
              rows={monitor.recentNotifications}
              getRowKey={(notification) => notification.id}
              emptyMessage="No recent notifications are recorded for this monitor."
              page={1}
              pageSize={Math.max(monitor.recentNotifications.length, 1)}
              total={monitor.recentNotifications.length}
              totalPages={monitor.recentNotifications.length > 0 ? 1 : 0}
              rowActions={[{
                label: 'View notification',
                icon: <Eye size={16} aria-hidden="true" />,
                onClick: (notification) => onNavigate(withReturnTo(`/notifications/${notification.id}`, detailPath))
              }]}
            />
          </section>
        </>
      )}
    </section>
  );
}

// Function summary: Renders the MonitorEditPanel React component and wires its local UI behavior.
function MonitorEditPanel({ monitorId, locationPath, onNavigate, onRequestError }: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
  const [monitor, setMonitor] = useState<MonitorDetailResponse | null>(null);
  const [fleetNumber, setFleetNumber] = useState('');
  const [calibrationDate, setCalibrationDate] = useState('');
  const [calibrationDue, setCalibrationDue] = useState('');
  const [location, setLocation] = useState('');
  const [what3words, setWhat3words] = useState('');
  const [lat, setLat] = useState('');
  const [lng, setLng] = useState('');
  const [pictureFile, setPictureFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}`);

  useEffect(() => {
    getMonitor(monitorId)
      .then((response) => {
        const item = response.item ?? null;
        setMonitor(item);
        setFleetNumber(item?.fleetNumber ?? '');
        setCalibrationDate(toDateInput(item?.calibrationDate));
        setCalibrationDue(toDateInput(item?.calibrationDue));
        setLocation(item?.location ?? '');
        setWhat3words(item?.what3words ?? '');
        setLat(item?.lat?.toString() ?? '');
        setLng(item?.lng?.toString() ?? '');
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [monitorId, onRequestError]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    const request: MonitorMutationRequest = {
      fleetNumber,
      calibrationDate: dateOrNull(calibrationDate),
      calibrationDue: dateOrNull(calibrationDue),
      deploymentId: monitor?.deploymentId ?? null,
      location,
      what3words,
      lat: numberOrNull(lat),
      lng: numberOrNull(lng)
    };
    try {
      let response = await updateMonitor(monitorId, request);
      if (pictureFile) {
        response = await uploadMonitorPicture(monitorId, pictureFile);
      }
      onNavigate(withReturnTo(`/monitors/${response.item?.id ?? monitorId}`, backPath));
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
          <p>Monitor</p>
          <h2>Edit Monitor</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
      </div>
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Fleet Number">
          <input value={fleetNumber} maxLength={32} onChange={(event) => setFleetNumber(event.target.value)} />
        </FormField>
        <FormField label="Calibration Date">
          <input value={calibrationDate} type="date" onChange={(event) => setCalibrationDate(event.target.value)} />
        </FormField>
        <FormField label="Calibration Due">
          <input value={calibrationDue} type="date" onChange={(event) => setCalibrationDue(event.target.value)} />
        </FormField>
        {monitor?.deploymentId && (
          <>
            <FormField label="Location">
              <input value={location} maxLength={256} onChange={(event) => setLocation(event.target.value)} />
            </FormField>
            <FormField label="What3words">
              <input value={what3words} maxLength={256} onChange={(event) => setWhat3words(event.target.value)} />
            </FormField>
            <FormField label="Latitude">
              <input value={lat} inputMode="decimal" onChange={(event) => setLat(event.target.value)} />
            </FormField>
            <FormField label="Longitude">
              <input value={lng} inputMode="decimal" onChange={(event) => setLng(event.target.value)} />
            </FormField>
            <FormField label="Upload picture for monitor">
              <input
                accept="image/png,image/jpeg,image/webp"
                type="file"
                onChange={(event) => setPictureFile(event.target.files?.[0] ?? null)}
              />
            </FormField>
            {safeHref(monitor.pictureLink) && (
              <a className="secondary-link" href={safeHref(monitor.pictureLink) ?? undefined} target="_blank" rel="noreferrer">
                <Upload size={16} aria-hidden="true" />
                <span>Current picture</span>
              </a>
            )}
          </>
        )}
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel="Save Monitor" />
      </form>
    </section>
  );
}

// Function summary: Renders the InstallerDeploymentPanel React component and wires its local UI behavior.
function InstallerDeploymentPanel({ monitorId, locationPath, onNavigate, onRequestError }: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
  const [monitor, setMonitor] = useState<MonitorDetailResponse | null>(null);
  const [location, setLocation] = useState('');
  const [what3words, setWhat3words] = useState('');
  const [lat, setLat] = useState('');
  const [lng, setLng] = useState('');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isConverting, setIsConverting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}`);

  useEffect(() => {
    getInstallerMonitor(monitorId)
      .then((response) => {
        const item = response.item ?? null;
        setMonitor(item);
        setLocation(item?.location ?? '');
        setWhat3words(item?.what3words ?? '');
        setLat(item?.lat?.toString() ?? '');
        setLng(item?.lng?.toString() ?? '');
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [monitorId, onRequestError]);

  async function handleConvert() {
    if (!what3words.trim()) {
      setError('Enter what3words before converting.');
      return;
    }
    setIsConverting(true);
    setError(null);
    try {
      const result = await convertWhat3Words(what3words);
      if (typeof result.lat === 'number' && typeof result.lng === 'number') {
        setLat(String(result.lat));
        setLng(String(result.lng));
        setNotice(result.nearestPlace ? `Converted near ${result.nearestPlace}.` : result.message);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsConverting(false);
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!monitor?.deploymentId) {
      setError('This monitor does not have a current deployment.');
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const response = await updateInstallerDeployment(monitor.deploymentId, {
        location,
        what3words,
        lat: Number(lat || 0),
        lng: Number(lng || 0)
      });
      onNavigate(withReturnTo(`/monitors/${response.item?.id ?? monitorId}`, backPath));
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
          <p>Installer</p>
          <h2>{monitor?.fleetNumber || 'Deployment'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Location">
          <input value={location} maxLength={256} onChange={(event) => setLocation(event.target.value)} />
        </FormField>
        <FormField label="What3words">
          <div className="input-with-action">
            <input value={what3words} maxLength={256} onChange={(event) => setWhat3words(event.target.value)} />
            <button className="icon-button" type="button" onClick={handleConvert} disabled={isConverting} aria-label="Convert what3words">
              <MapPinned size={16} aria-hidden="true" />
            </button>
          </div>
        </FormField>
        <FormField label="Latitude">
          <input value={lat} inputMode="decimal" onChange={(event) => setLat(event.target.value)} />
        </FormField>
        <FormField label="Longitude">
          <input value={lng} inputMode="decimal" onChange={(event) => setLng(event.target.value)} />
        </FormField>
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel="Save Deployment" />
      </form>
    </section>
  );
}

// Function summary: Renders the UnattachedMonitorRemovalPanel React component and wires its local UI behavior.
function UnattachedMonitorRemovalPanel({ locationPath, onNavigate, onRequestError }: MonitorsPanelProps) {
  const [monitors, setMonitors] = useState<UnattachedMonitorListItem[]>([]);
  const [selectedMonitor, setSelectedMonitor] = useState<UnattachedMonitorListItem | null>(null);
  const [reason, setReason] = useState('');
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState('');
  const [page, setPage] = useState(1);
  const [sortKey, setSortKey] = useState('fleetNumber');
  const [sortDir, setSortDir] = useState<SortDirection>('Ascending');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isRemoving, setIsRemoving] = useState(false);
  const backPath = returnToOr(locationPath, '/monitors');
  const columns = useMemo<DataGridColumn<UnattachedMonitorListItem>[]>(() => [
    { key: 'fleetNumber', header: 'Fleet', sortable: true, render: (monitor) => monitor.fleetNumber || 'Unassigned' },
    { key: 'serialId', header: 'Serial', sortable: true, render: (monitor) => monitor.serialId },
    { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (monitor) => monitor.typeOfMonitor },
    { key: 'model', header: 'Model', render: (monitor) => monitor.model || 'Unknown' },
    { key: 'impact', header: 'Related data', render: (monitor) => removalImpactLabel(monitor) },
    { key: 'removalMode', header: 'Removal', render: (monitor) => monitor.willArchiveOnRemoval ? 'Archive' : 'Delete' }
  ], []);
  const query = useMemo<QueryMonitorsRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);

  // Function summary: Loads unattached monitor removal candidates from the API.
  const loadMonitors = useCallback(async (signal?: AbortSignal) => {
    setIsLoading(true);
    try {
      const response = await queryUnattachedMonitors(query, { signal });
      setMonitors(response.results);
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
    void loadMonitors(controller.signal);
    return () => controller.abort();
  }, [loadMonitors]);

  // Function summary: Handles search text changes for unattached monitor removal candidates.
  function handleSearch(value: string) {
    resetSearchPage(value, setSearchText, setPage);
  }

  // Function summary: Handles sort changes for unattached monitor removal candidates.
  function handleSortChange(key: string, direction: GridSortDirection) {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }

  // Function summary: Removes or archives the selected unattached monitor.
  async function handleRemove() {
    if (!selectedMonitor) {
      return;
    }

    setIsRemoving(true);
    setError(null);
    try {
      const response = await removeUnattachedMonitor(selectedMonitor.id, { reason });
      setNotice(response.message);
      setSelectedMonitor(null);
      setReason('');
      await loadMonitors();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsRemoving(false);
    }
  }

  const selectedMonitorName = selectedMonitor?.fleetNumber || selectedMonitor?.serialId || 'this monitor';
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Admin</p>
          <h2>Unattached Monitors</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search unattached monitors" />
      </label>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      {selectedMonitor && (
        <FormField label="Removal reason">
          <input
            value={reason}
            maxLength={512}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Reason recorded for audit history"
          />
        </FormField>
      )}
      <DataGrid
        columns={columns}
        rows={monitors}
        getRowKey={(monitor) => monitor.id}
        emptyMessage="No unattached monitors match the current filters."
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
        rowActions={[{
          label: 'Remove monitor',
          icon: <Trash2 size={16} aria-hidden="true" />,
          onClick: (monitor) => setSelectedMonitor(monitor)
        }]}
      />
      <ConfirmDialog
        open={Boolean(selectedMonitor)}
        title={selectedMonitor?.willArchiveOnRemoval ? 'Archive monitor' : 'Delete monitor'}
        message={
          selectedMonitor?.willArchiveOnRemoval
            ? `Archive ${selectedMonitorName}? Related data will be retained.`
            : `Delete ${selectedMonitorName}? This monitor has no related data.`
        }
        confirmLabel={selectedMonitor?.willArchiveOnRemoval ? 'Archive' : 'Delete'}
        isBusy={isRemoving}
        onCancel={() => setSelectedMonitor(null)}
        onConfirm={handleRemove}
      />
    </section>
  );
}

// Function summary: Renders the MonitorAssignmentPanel React component and wires its local UI behavior.
function MonitorAssignmentPanel({
  siteId,
  contractId,
  locationPath,
  onNavigate,
  onRequestError
}: MonitorsPanelProps & Readonly<{ siteId: string; contractId?: string | null }>) {
  const [context, setContext] = useState<MonitorAssignmentContextResponse | null>(null);
  const [selectedContractId, setSelectedContractId] = useState(contractId ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const backPath = returnToOr(locationPath, '/monitors');
  useEffect(() => {
    getMonitorAssignment(siteId, contractId)
      .then((response) => {
        setContext(response);
        setSelectedContractId(response.contractId ?? contractId ?? '');
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError, siteId]);

  // Function summary: Handles the handle contract change workflow for this module.
  function handleContractChange(value: string) {
    setSelectedContractId(value);
    const assignmentPath = value ? `/monitors/assign?siteId=${siteId}&contractId=${value}` : `/monitors/assign?siteId=${siteId}`;
    onNavigate(withReturnTo(assignmentPath, backPath));
  }

  async function handleAdd(monitor: MonitorListItem) {
    if (!selectedContractId) {
      setError('Select a contract before assigning a monitor.');
      return;
    }
    setIsBusy(true);
    try {
      await addMonitorToContract(monitor.id, { contractId: selectedContractId });
      setContext(await getMonitorAssignment(siteId, selectedContractId));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
    }
  }

  async function handleRemove(monitor: MonitorListItem) {
    setIsBusy(true);
    try {
      await removeMonitorFromContract(monitor.id);
      setContext(await getMonitorAssignment(siteId, selectedContractId));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Site Assignment</p>
          <h2>{context?.siteName ?? 'Monitor Assignment'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>Back</button>
      </div>
      {error && <Notice tone="error" message={error} />}
      <FormField label="Contract">
        <select value={selectedContractId} onChange={(event) => handleContractChange(event.target.value)}>
          <option value="">Select a contract</option>
          {context?.contracts.map((contract) => (
            <option value={contract.value} key={contract.value}>{contract.label}</option>
          ))}
        </select>
      </FormField>
      <section className="split-grid">
        <div>
          <div className="subsection-heading">
            <Plus size={18} aria-hidden="true" />
            <h3>Available</h3>
          </div>
          <DataGrid
            columns={assignmentColumns}
            rows={context?.availableMonitors ?? []}
            getRowKey={(monitor) => monitor.id}
            emptyMessage="No available monitors."
            isLoading={!context}
            page={1}
            pageSize={Math.max(context?.availableMonitors.length ?? 0, 1)}
            total={context?.availableMonitors.length ?? 0}
            totalPages={(context?.availableMonitors.length ?? 0) > 0 ? 1 : 0}
            rowActions={[{
              label: 'Assign monitor',
              icon: <Plus size={16} aria-hidden="true" />,
              onClick: handleAdd,
              disabled: () => isBusy || !selectedContractId
            }]}
          />
        </div>
        <div>
          <div className="subsection-heading">
            <CheckCircle2 size={18} aria-hidden="true" />
            <h3>Assigned</h3>
          </div>
          <DataGrid
            columns={assignmentColumns}
            rows={context?.assignedMonitors ?? []}
            getRowKey={(monitor) => monitor.id}
            emptyMessage="No monitors are assigned."
            isLoading={!context}
            page={1}
            pageSize={Math.max(context?.assignedMonitors.length ?? 0, 1)}
            total={context?.assignedMonitors.length ?? 0}
            totalPages={(context?.assignedMonitors.length ?? 0) > 0 ? 1 : 0}
            rowActions={[{
              label: 'Remove monitor',
              icon: <Trash2 size={16} aria-hidden="true" />,
              onClick: handleRemove,
              disabled: () => isBusy
            }]}
          />
        </div>
      </section>
    </section>
  );
}

// Function summary: Renders the MonitorStatusBadge React component and wires its local UI behavior.
function MonitorStatusBadge({ monitor }: Readonly<{ monitor: MonitorListItem }>) {
  return (
    <span className={`status-chip ${monitorStatusClassName(monitor)}`}>
      {monitorStatusLabel(monitor)}
    </span>
  );
}

// Function summary: Handles the monitor status label workflow for this module.
function monitorStatusLabel(monitor: MonitorListItem) {
  if (!monitor.isAssigned) {
    return 'Not deployed';
  }
  return monitor.isOffline ? 'Offline' : 'Online';
}

// Function summary: Handles the monitor status class name workflow for this module.
function monitorStatusClassName(monitor: MonitorListItem) {
  if (monitor.isOffline) {
    return 'danger';
  }
  if (monitor.isAssigned) {
    return 'success';
  }
  return 'neutral';
}

// Function summary: Formats related data counts for unattached monitor removal candidates.
function removalImpactLabel(monitor: UnattachedMonitorListItem) {
  const impact = monitor.impact;
  if (!monitor.hasRelatedData) {
    return 'None';
  }

  const parts = [
    impact.deploymentCount ? `${impact.deploymentCount} deployments` : null,
    impact.notificationCount ? `${impact.notificationCount} notifications` : null,
    impact.alertRuleCount ? `${impact.alertRuleCount} alert rules` : null,
    impact.measurementRowCount ? `${impact.measurementRowCount} data rows` : null
  ].filter(Boolean);

  return parts.join(', ');
}

// Function summary: Renders the DetailItem React component and wires its local UI behavior.
function DetailItem({ label, value }: Readonly<{ label: string; value?: string | null }>) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value || 'None'}</strong>
    </div>
  );
}

const alertLevelColumns: DataGridColumn<MonitorAlertLevelItem>[] = [
  { key: 'alertField', header: 'Field', render: (level) => level.alertField },
  { key: 'alertType', header: 'Type', render: (level) => level.alertType },
  { key: 'limitOn', header: 'On', render: (level) => level.limitOn },
  { key: 'limitOff', header: 'Off', render: (level) => level.limitOff },
  { key: 'averagingPeriod', header: 'Average', render: (level) => `${level.averagingPeriod}s` },
  { key: 'isActive', header: 'Active', render: (level) => level.isActive ? 'Yes' : 'No' }
];

const notificationColumns: DataGridColumn<MonitorNotificationItem>[] = [
  { key: 'notificationTime', header: 'Time', render: (notification) => formatDateTime(notification.notificationTime) },
  { key: 'alertType', header: 'Type', render: (notification) => notification.alertType },
  { key: 'alertField', header: 'Field', render: (notification) => notification.alertField },
  { key: 'level', header: 'Level', render: (notification) => notification.level },
  { key: 'limitOn', header: 'Limit', render: (notification) => notification.limitOn },
  { key: 'closedTime', header: 'State', render: (notification) => notification.closedTime ? 'Closed' : 'Open' }
];

const assignmentColumns: DataGridColumn<MonitorListItem>[] = [
  { key: 'fleetNumber', header: 'Fleet', render: (monitor) => monitor.fleetNumber || 'Unassigned' },
  { key: 'serialId', header: 'Serial', render: (monitor) => monitor.serialId },
  { key: 'typeOfMonitor', header: 'Type', render: (monitor) => monitor.typeOfMonitor },
  { key: 'siteName', header: 'Site', render: (monitor) => monitor.siteName || 'Not deployed' }
];

// Function summary: Handles the monitor tabs workflow for this module.
function monitorTabs(canManage: boolean, installerOnly: boolean): Array<{ state: MonitorListState; label: string }> {
  if (installerOnly) {
    return [{ state: 'installer', label: 'Installer' }];
  }
  if (canManage) {
    return [
      { state: 'all', label: 'All' },
      { state: 'new', label: 'New' },
      { state: 'not-in-use', label: 'Not In Use' },
      { state: 'offline', label: 'Offline' },
      { state: 'online', label: 'Online' },
      { state: 'installer', label: 'Installer' }
    ];
  }
  return [
    { state: 'all', label: 'All' },
    { state: 'offline', label: 'Offline' },
    { state: 'online', label: 'Online' }
  ];
}

// Function summary: Handles the parse monitor route workflow for this module.
function parseMonitorRoute(locationPath: string): MonitorRoute {
  const url = new URL(locationPath, 'https://rvt.local');
  const segments = url.pathname.split('/').filter(Boolean);
  if (segments[0] !== 'monitors') {
    return { kind: 'list' };
  }
  if (segments[1] === 'assign') {
    return {
      kind: 'assignment',
      siteId: url.searchParams.get('siteId') ?? '',
      contractId: url.searchParams.get('contractId')
    };
  }
  if (segments[1] === 'unattached') {
    return { kind: 'unattached' };
  }
  if (!segments[1]) {
    return { kind: 'list' };
  }
  if (segments[2] === 'edit') {
    return { kind: 'edit', monitorId: segments[1] };
  }
  if (segments[2] === 'installer') {
    return { kind: 'installer', monitorId: segments[1] };
  }
  if (segments[2] === 'alert-levels') {
    return { kind: 'alert-levels', monitorId: segments[1] };
  }
  return { kind: 'detail', monitorId: segments[1] };
}

// Function summary: Builds monitors url data for callers.
function buildMonitorsUrl({
  searchText,
  page,
  sort,
  sortDir,
  state
}: {
  searchText: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
  state: MonitorListState;
}) {
  const params = new URLSearchParams();
  if (state !== 'all') {
    params.set('state', state);
  }
  if (searchText) {
    params.set('q', searchText);
  }
  if (page > 1) {
    params.set('page', String(page));
  }
  if (sort !== 'fleetNumber') {
    params.set('sort', sort);
  }
  if (sortDir !== 'Ascending') {
    params.set('sortDir', sortDir);
  }
  return pathWithQuery('/monitors', params);
}

// Function summary: Handles the path with query workflow for this module.
function pathWithQuery(path: string, params: URLSearchParams) {
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

// Function summary: Handles the normalize state workflow for this module.
function normalizeState(value: string | null, fallback: MonitorListState): MonitorListState {
  const states: MonitorListState[] = ['all', 'new', 'not-in-use', 'offline', 'online', 'installer'];
  return states.includes(value as MonitorListState) ? value as MonitorListState : fallback;
}

// Function summary: Handles the normalize sort direction workflow for this module.
function normalizeSortDirection(value: string | null): SortDirection {
  return value?.toLowerCase() === 'descending' || value?.toLowerCase() === 'desc' ? 'Descending' : 'Ascending';
}

// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number.parseInt(value ?? '', 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

// Function summary: Renders a legacy monitor metric summary card.
function MonitorMetricCard({
  title,
  metric,
  fallback
}: Readonly<{ title: string; metric?: MonitorMetricSummary | null; fallback: string }>) {
  return (
    <div className="metric-card">
      <span>{title}</span>
      <strong>{formatMetricValue(metric) || fallback}</strong>
      {metric?.label && <small>{metric.label}</small>}
      {metric?.detail && <small>{metric.detail}</small>}
      {metric?.sampleTime && <small>{formatDateTime(metric.sampleTime)}</small>}
    </div>
  );
}

// Function summary: Converts monitor detail coordinates into reusable map markers.
function monitorDetailMarkers(monitor: MonitorDetailResponse): MapMonitorMarker[] {
  if (typeof monitor.lat !== 'number' || typeof monitor.lng !== 'number') {
    return [];
  }

  return [{
    monitorId: monitor.id,
    deploymentId: monitor.deploymentId ?? monitor.id,
    latitude: monitor.lat,
    longitude: monitor.lng,
    typeOfMonitor: monitor.typeOfMonitor,
    offline: monitor.isOffline,
    alert: monitor.hasAlerts,
    caution: monitor.hasCautions,
    siteName: monitor.siteName,
    fleetNumber: monitor.fleetNumber,
    serialId: monitor.serialId,
    lastDataTime: monitor.lastDataTime,
    what3words: monitor.what3words
  }];
}

// Function summary: Formats monitor metric values with their unit for display.
function formatMetricValue(metric?: MonitorMetricSummary | null) {
  if (metric?.value === null || metric?.value === undefined) {
    return '';
  }

  const unit = metric.unit ? ` ${metric.unit}` : '';
  return `${metric.value}${unit}`;
}

function resetSearchPage(value: string, setSearchText: (nextValue: string) => void, setPage: (nextPage: number) => void) {
  setSearchText(value);
  setPage(1);
}

// Function summary: Maps date input into the shape required by callers.
function toDateInput(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

// Function summary: Handles the date or null workflow for this module.
function dateOrNull(value: string) {
  return value ? `${value}T00:00:00` : null;
}

// Function summary: Handles the number or null workflow for this module.
function numberOrNull(value: string) {
  if (!value.trim()) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

// Function summary: Handles the format date workflow for this module.
function formatDate(value?: string | null) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(new Date(value));
}

// Function summary: Handles the format date time workflow for this module.
function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

// Function summary: Handles the format coordinates workflow for this module.
function formatCoordinates(lat?: number | null, lng?: number | null) {
  if (typeof lat !== 'number' || typeof lng !== 'number') {
    return 'Not recorded';
  }
  return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

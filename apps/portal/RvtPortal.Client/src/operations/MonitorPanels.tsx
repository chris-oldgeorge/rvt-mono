// File summary: Renders the monitors route panel with the list, edit, and installer deployment views.
// Major updates:
// - 2026-07-30 pending Rejected blank and non-numeric installer coordinates instead of saving 0, 0.
// - 2026-07-30 pending Split detail, unattached-removal, and assignment panels into their own modules.
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

import { Edit3, Eye, Gauge, MapPinned, RefreshCcw, Save, Search, Trash2, Upload, Wrench } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  addDefaultMonitorAlertLevels,
  convertWhat3Words,
  getInstallerMonitor,
  getMonitor,
  isAbortError,
  queryInstallerMonitors,
  queryMonitors,
  updateInstallerDeployment,
  updateMonitor,
  uploadMonitorPicture,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { safeHref } from '../safeUrl';
import { AlertLevelsPanel } from './AlertLevelPanels';
import { MonitorAssignmentPanel } from './MonitorAssignmentPanel';
import { MonitorDetailPanel } from './MonitorDetailPanel';
import { UnattachedMonitorRemovalPanel } from './MonitorRemovalPanel';
import { normalizeSortDirection, parsePositiveInt } from '../gridQuery';
import { pageSize, resetSearchPage } from './monitorShared';
import type { ListExecution, MonitorsPanelProps } from './monitorShared';
import type {
  DefaultMonitorsResponse,
  MonitorDetailResponse,
  MonitorListItem,
  MonitorListState,
  MonitorMutationRequest,
  QueryMonitorsRequest,
  SortDirection,
} from '../dtos';

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
  installerOnly = false,
}: MonitorsPanelProps) {
  const mode = parseMonitorRoute(locationPath);
  if (mode.kind === 'unattached' && canManage) {
    return (
      <UnattachedMonitorRemovalPanel
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
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
    return (
      <MonitorEditPanel
        monitorId={mode.monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (mode.kind === 'installer' && canUseInstallerTools) {
    return (
      <InstallerDeploymentPanel
        monitorId={mode.monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
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
  installerOnly,
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
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QueryMonitorsRequest> | null>(null);
  const [isAddingDefaults, setIsAddingDefaults] = useState(false);
  const columns = useMemo<DataGridColumn<MonitorListItem>[]>(
    () => [
      {
        key: 'fleetNumber',
        header: 'Fleet',
        sortable: true,
        render: (monitor) => (
          <span className="cell-with-icon">
            <Gauge size={16} aria-hidden="true" />
            {monitor.fleetNumber || 'Unassigned'}
          </span>
        ),
      },
      { key: 'serialId', header: 'Serial', sortable: true, render: (monitor) => monitor.serialId },
      { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (monitor) => monitor.typeOfMonitor },
      { key: 'siteName', header: 'Site', sortable: true, render: (monitor) => monitor.siteName || 'Not deployed' },
      {
        key: 'contractNumber',
        header: 'Contract',
        sortable: true,
        render: (monitor) => monitor.contractNumber || 'None',
      },
      { key: 'online', header: 'Online', render: (monitor) => (monitor.isOffline ? 'No' : 'Yes') },
      { key: 'alerts', header: 'Alerts', render: (monitor) => (monitor.hasAlerts ? 'Yes' : 'No') },
      { key: 'cautions', header: 'Cautions', render: (monitor) => (monitor.hasCautions ? 'Yes' : 'No') },
      {
        key: 'lastDataTime',
        header: 'Status',
        sortable: true,
        render: (monitor) => <MonitorStatusBadge monitor={monitor} />,
      },
    ],
    [],
  );
  const effectiveState = tabs.some((tab) => tab.state === state) ? state : tabs[0].state;
  const query = useMemo<QueryMonitorsRequest>(
    () => ({
      searchText,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
      state: effectiveState,
    }),
    [effectiveState, page, searchText, sortDir, sortKey],
  );
  const execution = useMemo<ListExecution<QueryMonitorsRequest>>(() => ({ query }), [query]);
  const isLoading = completedExecution !== execution;
  const returnPath = currentRoutePath(locationPath);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(
      null,
      '',
      buildMonitorsUrl({ searchText, page, sort: sortKey, sortDir, state: effectiveState }),
    );
    const load = installerOnly ? queryInstallerMonitors : queryMonitors;
    load(execution.query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setMonitors(response.results);
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
  }, [effectiveState, execution, installerOnly, onRequestError, page, searchText, sortDir, sortKey]);

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
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo('/monitors/unattached', returnPath))}
            >
              <Trash2 size={17} aria-hidden="true" />
              <span>Unattached</span>
            </button>
            <button
              className="secondary-button"
              type="button"
              onClick={handleDefaultLevels}
              disabled={isAddingDefaults}
            >
              <RefreshCcw size={17} aria-hidden="true" />
              <span>{isAddingDefaults ? 'Adding defaults' : 'Default Alerts'}</span>
            </button>
          </div>
        )}
      </div>
      <div className="segmented-control" role="tablist" aria-label="Monitor list states">
        {tabs.map((tab) => (
          <button
            className={effectiveState === tab.state ? 'active' : ''}
            type="button"
            role="tab"
            aria-selected={effectiveState === tab.state}
            key={tab.state}
            onClick={() => handleState(tab.state)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input
          value={searchText}
          onChange={(event) => handleSearch(event.target.value)}
          placeholder="Search monitors"
        />
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
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}`, returnPath)),
          },
          {
            label: 'Edit monitor',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}/edit`, returnPath)),
            disabled: (monitor) => !canManage || !monitor.canEdit,
          },
          {
            label: 'Installer edit',
            icon: <Wrench size={16} aria-hidden="true" />,
            onClick: (monitor) => onNavigate(withReturnTo(`/monitors/${monitor.id}/installer`, returnPath)),
            disabled: (monitor) => !canUseInstallerTools || !monitor.canInstallerEdit,
          },
        ]}
      />
    </section>
  );
}

// Function summary: Renders the MonitorEditPanel React component and wires its local UI behavior.
function MonitorEditPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
}: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
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

  async function handleSubmit(event: SyntheticEvent) {
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
      lng: numberOrNull(lng),
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
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
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
              <a
                className="secondary-link"
                href={safeHref(monitor.pictureLink) ?? undefined}
                target="_blank"
                rel="noreferrer"
              >
                <Upload size={16} aria-hidden="true" />
                <span>Current picture</span>
              </a>
            )}
          </>
        )}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel="Save Monitor"
        />
      </form>
    </section>
  );
}

// Function summary: Renders the InstallerDeploymentPanel React component and wires its local UI behavior.
function InstallerDeploymentPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
}: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
  const [monitor, setMonitor] = useState<MonitorDetailResponse | null>(null);
  const [location, setLocation] = useState('');
  const [what3words, setWhat3words] = useState('');
  const [lat, setLat] = useState('');
  const [lng, setLng] = useState('');
  const [latError, setLatError] = useState<string | null>(null);
  const [lngError, setLngError] = useState<string | null>(null);
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
        setLatError(null);
        setLngError(null);
        setNotice(result.nearestPlace ? `Converted near ${result.nearestPlace}.` : result.message);
      } else {
        // A 200 without coordinates is the API's "could not resolve that address" answer and
        // carries its own message; silently doing nothing left the button looking broken.
        setNotice(null);
        setError(result.message || 'That what3words address could not be converted to coordinates.');
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsConverting(false);
    }
  }

  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    if (!monitor?.deploymentId) {
      setError('This monitor does not have a current deployment.');
      return;
    }

    // The deployment contract requires numbers, so a blank or mistyped coordinate has to be
    // rejected here: coercing it would pin the monitor at 0, 0 in the Gulf of Guinea, and
    // passing NaN through only earns an opaque 400.
    const nextLatError = coordinateError(lat, 'Latitude', 90);
    const nextLngError = coordinateError(lng, 'Longitude', 180);
    setLatError(nextLatError);
    setLngError(nextLngError);
    if (nextLatError || nextLngError) {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      const response = await updateInstallerDeployment(monitor.deploymentId, {
        location,
        what3words,
        lat: Number(lat),
        lng: Number(lng),
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
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
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
            <button
              className="icon-button"
              type="button"
              onClick={handleConvert}
              disabled={isConverting}
              aria-label="Convert what3words"
            >
              <MapPinned size={16} aria-hidden="true" />
            </button>
          </div>
        </FormField>
        <FormField label="Latitude" error={latError}>
          <input
            value={lat}
            inputMode="decimal"
            onChange={(event) => {
              setLat(event.target.value);
              setLatError(null);
            }}
          />
        </FormField>
        <FormField label="Longitude" error={lngError}>
          <input
            value={lng}
            inputMode="decimal"
            onChange={(event) => {
              setLng(event.target.value);
              setLngError(null);
            }}
          />
        </FormField>
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel="Save Deployment"
        />
      </form>
    </section>
  );
}

// Function summary: Validates one required deployment coordinate instead of coercing it to zero.
function coordinateError(value: string, field: 'Latitude' | 'Longitude', limit: number) {
  if (!value.trim()) {
    return `${field} is required. Convert the what3words address or type the coordinate.`;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return `${field} must be a number, for example ${field === 'Latitude' ? '51.5072' : '-0.1276'}.`;
  }
  if (parsed < -limit || parsed > limit) {
    return `${field} must be between -${limit} and ${limit}.`;
  }

  return null;
}

// Function summary: Renders the MonitorStatusBadge React component and wires its local UI behavior.
function MonitorStatusBadge({ monitor }: Readonly<{ monitor: MonitorListItem }>) {
  return <span className={`status-chip ${monitorStatusClassName(monitor)}`}>{monitorStatusLabel(monitor)}</span>;
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
      { state: 'installer', label: 'Installer' },
    ];
  }
  return [
    { state: 'all', label: 'All' },
    { state: 'offline', label: 'Offline' },
    { state: 'online', label: 'Online' },
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
      contractId: url.searchParams.get('contractId'),
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
  state,
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
  return states.includes(value as MonitorListState) ? (value as MonitorListState) : fallback;
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

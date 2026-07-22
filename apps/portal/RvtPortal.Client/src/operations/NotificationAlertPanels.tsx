// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for notification and alert-level list requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for notification and alert-level forms.
// - 2026-06-25 pending Surfaced closed notification notes in notification list and detail views.
// - 2026-06-25 pending Hid the Average column for vibration alert-level grids.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  Bell,
  Check,
  ChevronLeft,
  Edit3,
  Eye,
  Gauge,
  Plus,
  RefreshCcw,
  Save,
  Search,
  Trash2
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  batchCloseNotifications,
  closeNotification,
  createAlertLevel,
  deleteAlertLevel,
  getAlertLevel,
  getAlertLevelOptions,
  getNotification,
  isAbortError,
  queryAlertLevels,
  queryNotifications,
  updateAlertLevel,
  updateVibrationAlertLevels
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import type {
  AlertLevelItem,
  AlertLevelMutationRequest,
  AlertLevelOptionsResponse,
  NotificationDetailResponse,
  NotificationListItem,
  NotificationListState,
  QueryAlertLevelsRequest,
  QueryAlertLevelsResponse,
  QueryNotificationsRequest,
  SortDirection
} from '../dtos';

const pageSize = 10;

type OperationsPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type AlertLevelsPanelProps = Readonly<{
  monitorId: string;
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
  canManage?: boolean;
}>;

type AlertLevelRoute =
  | { kind: 'list' }
  | { kind: 'new' }
  | { kind: 'edit'; levelId: string }
  | { kind: 'vibration' };

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

// Function summary: Renders the NotificationsPanel React component and wires its local UI behavior.
export function NotificationsPanel({ locationPath, onNavigate, onRequestError }: OperationsPanelProps) {
  const route = parseNotificationRoute(locationPath);
  if (route.notificationId) {
    return <NotificationDetailPanel notificationId={route.notificationId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }

  return <NotificationListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the NotificationListPanel React component and wires its local UI behavior.
function NotificationListPanel({ locationPath, onNavigate, onRequestError }: OperationsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [state, setState] = useState<NotificationListState>(() => normalizeNotificationState(initialParams.get('state')));
  const [notifications, setNotifications] = useState<NotificationListItem[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'notificationTime');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir'), 'Descending'));
  const [closeNote, setCloseNote] = useState('');
  const [canClose, setCanClose] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const showClosedNoteColumn = notifications.some((notification) => hasText(notification.closedNote));
  const returnPath = currentRoutePath(locationPath);

  const columns = useMemo<DataGridColumn<NotificationListItem>[]>(() => {
    const nextColumns: DataGridColumn<NotificationListItem>[] = [
      {
        key: 'select',
        header: '',
        render: (notification) => (
          <input
            aria-label={`Select notification ${notification.fleetNumber || notification.serialId}`}
            checked={selectedIds.has(notification.id)}
            disabled={!notification.canClose || Boolean(notification.closedTime)}
            type="checkbox"
            onChange={(event) => toggleSelected(notification.id, event.target.checked)}
          />
        )
      },
      { key: 'notificationTime', header: 'Time', sortable: true, render: (notification) => formatDateTime(notification.notificationTime) },
      { key: 'fleetNumber', header: 'Fleet', sortable: true, render: (notification) => notification.fleetNumber || 'Unassigned' },
      { key: 'siteName', header: 'Site', sortable: true, render: (notification) => notification.siteName || 'Not deployed' },
      { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (notification) => notification.typeOfMonitor },
      { key: 'limitName', header: 'Limit', sortable: true, render: (notification) => notification.limitName || notification.alertField },
      { key: 'level', header: 'Level', sortable: true, align: 'end', render: (notification) => formatNumber(notification.level) },
      {
        key: 'alertStatus',
        header: 'State',
        sortable: true,
        render: (notification) => <NotificationStatusChip notification={notification} />
      }
    ];

    if (showClosedNoteColumn) {
      nextColumns.push({
        key: 'closedNote',
        header: 'Closed note',
        render: (notification) => notification.closedNote?.trim() ?? ''
      });
    }

    return nextColumns;
  }, [selectedIds, showClosedNoteColumn]);

  const query = useMemo<QueryNotificationsRequest>(() => ({
    searchText,
    page,
    pageSize,
    sort: sortKey,
    sortDir,
    state
  }), [page, searchText, sortDir, sortKey, state]);
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildNotificationsUrl({ state, searchText, page, sort: sortKey, sortDir }));
    setIsLoading(true);
    queryNotifications(query, { signal: controller.signal })
      .then((response) => {
        setNotifications(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setCanClose(response.canClose);
        setSelectedIds(new Set());
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
  }, [onRequestError, page, query, searchText, sortDir, sortKey, state]);

  // Function summary: Maps ggle selected into the shape required by callers.
  function toggleSelected(id: string, checked: boolean) {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  // Function summary: Handles the handle state workflow for this module.
  function handleState(nextState: NotificationListState) {
    setState(nextState);
    setPage(1);
  }

  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }

  async function handleBatchClose() {
    setIsClosing(true);
    setNotice(null);
    setError(null);
    try {
      const response = await batchCloseNotifications({ notificationIds: Array.from(selectedIds), note: closeNote });
      setNotice(`Closed ${response.closedIds.length} of ${response.requested} selected notifications.`);
      setCloseNote('');
      const refreshed = await queryNotifications(query);
      setNotifications(refreshed.results);
      setTotal(refreshed.total);
      setTotalPages(refreshed.totalPages);
      setCanClose(refreshed.canClose);
      setSelectedIds(new Set());
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsClosing(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Operations</p>
          <h2>Notifications</h2>
        </div>
        <Bell size={22} aria-hidden="true" />
      </div>
      <div className="segmented-control" role="tablist" aria-label="Notification list states">
        {notificationTabs.map((tab) => (
          <button
            className={state === tab.state ? 'active' : ''}
            key={tab.state}
            type="button"
            role="tab"
            aria-selected={state === tab.state}
            onClick={() => handleState(tab.state)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search notifications" />
      </label>
      {canClose && selectedIds.size > 0 && (
        <div className="batch-toolbar" aria-label="Batch notification close">
          <input
            value={closeNote}
            maxLength={255}
            onChange={(event) => setCloseNote(event.target.value)}
            placeholder="Close note"
          />
          <button className="secondary-button" type="button" onClick={handleBatchClose} disabled={isClosing}>
            <Check size={17} aria-hidden="true" />
            <span>{isClosing ? 'Closing' : `Close ${selectedIds.size}`}</span>
          </button>
        </div>
      )}
      {notice && <Notice tone="success" message={notice} />}
      <DataGrid
        columns={columns}
        rows={notifications}
        getRowKey={(notification) => notification.id}
        emptyMessage="No notifications match the current filters."
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
            label: 'View notification',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (notification) => onNavigate(withReturnTo(`/notifications/${notification.id}`, returnPath))
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the NotificationDetailPanel React component and wires its local UI behavior.
function NotificationDetailPanel({
  notificationId,
  locationPath,
  onNavigate,
  onRequestError
}: OperationsPanelProps & Readonly<{ notificationId: string }>) {
  const [notification, setNotification] = useState<NotificationDetailResponse | null>(null);
  const [closeNote, setCloseNote] = useState('');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isClosing, setIsClosing] = useState(false);
  const backPath = returnToOr(locationPath, '/notifications');
  const detailPath = currentRoutePath(locationPath);

  useEffect(() => {
    getNotification(notificationId)
      .then((response) => {
        setNotification(response.item ?? null);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [notificationId, onRequestError]);

  async function handleClose(event: FormEvent) {
    event.preventDefault();
    setIsClosing(true);
    setNotice(null);
    setError(null);
    try {
      const response = await closeNotification(notificationId, { note: closeNote });
      setNotification(response.item ?? null);
      setCloseNote('');
      setNotice('Notification has been closed.');
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsClosing(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Notification</p>
          <h2>{notification?.limitName || notification?.alertField || 'Loading notification'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Back</span>
          </button>
          {notification?.monitorId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${notification.monitorId}/alert-levels`, detailPath))}>
              <Gauge size={17} aria-hidden="true" />
              <span>Alert Levels</span>
            </button>
          )}
        </div>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      {notification && (
        <>
          <div className="detail-grid monitor-detail-grid">
            <DetailItem label="Fleet" value={notification.fleetNumber || 'Unassigned'} />
            <DetailItem label="Serial" value={notification.serialId} />
            <DetailItem label="Monitor Type" value={notification.typeOfMonitor} />
            <DetailItem label="Site" value={notification.siteName || 'Not deployed'} />
            <DetailItem label="Contract" value={notification.contractNumber || 'None'} />
            <DetailItem label="Alert Type" value={notification.alertType} />
            <DetailItem label="Parameter" value={notification.alertField} />
            <DetailItem label="Level" value={formatNumber(notification.level)} />
            <DetailItem label="Limit" value={formatNumber(notification.limitOn)} />
            <DetailItem label="Status" value={notification.closedTime ? 'Closed' : notification.alertStatus || 'Open'} />
            <DetailItem label="Raised" value={formatDateTime(notification.notificationTime)} />
            <DetailItem label="Closed" value={formatDateTime(notification.closedTime) || 'Open'} />
            {hasText(notification.closedNote) && <DetailItem label="Closed Note" value={notification.closedNote?.trim() ?? ''} />}
            <DetailItem label="Location" value={notification.location || 'Not recorded'} />
            <DetailItem label="What3words" value={notification.what3words || 'Not recorded'} />
            <DetailItem label="Graph Window" value={`${formatDateTime(notification.graphFromUtc)} to ${formatDateTime(notification.graphToUtc)}`} />
          </div>
          {notification.canClose && !notification.closedTime && (
            <form className="batch-toolbar" onSubmit={handleClose} aria-label="Close notification">
              <input
                value={closeNote}
                maxLength={255}
                onChange={(event) => setCloseNote(event.target.value)}
                placeholder="Close note"
              />
              <SubmitButton
                icon={<Check size={17} aria-hidden="true" />}
                isSubmitting={isClosing}
                idleLabel="Close Alert"
                submittingLabel="Closing"
              />
            </form>
          )}
          <section className="subsection">
            <div className="subsection-heading">
              <Bell size={18} aria-hidden="true" />
              <h3>Configured Alert Levels</h3>
            </div>
            <DataGrid
              columns={alertLevelColumnsForMonitorType(notification.typeOfMonitor)}
              rows={notification.alertLevels}
              getRowKey={(level) => level.id}
              emptyMessage="No alert levels are configured for this monitor."
              page={1}
              pageSize={Math.max(notification.alertLevels.length, 1)}
              total={notification.alertLevels.length}
              totalPages={notification.alertLevels.length > 0 ? 1 : 0}
            />
          </section>
          <section className="subsection">
            <div className="subsection-heading">
              <RefreshCcw size={18} aria-hidden="true" />
              <h3>Related Notifications</h3>
            </div>
            <DataGrid
              columns={relatedNotificationColumns}
              rows={notification.relatedNotifications}
              getRowKey={(related) => related.id}
              emptyMessage="No related notifications are recorded for this monitor."
              page={1}
              pageSize={Math.max(notification.relatedNotifications.length, 1)}
              total={notification.relatedNotifications.length}
              totalPages={notification.relatedNotifications.length > 0 ? 1 : 0}
              rowActions={[
                {
                  label: 'View notification',
                  icon: <Eye size={16} aria-hidden="true" />,
                  onClick: (related) => onNavigate(withReturnTo(`/notifications/${related.id}`, detailPath))
                }
              ]}
            />
          </section>
        </>
      )}
    </section>
  );
}

// Function summary: Renders the AlertLevelsPanel React component and wires its local UI behavior.
export function AlertLevelsPanel({ monitorId, locationPath, onNavigate, onRequestError, canManage = false }: AlertLevelsPanelProps) {
  const route = parseAlertLevelRoute(locationPath);
  if (route.kind === 'new' && canManage) {
    return <AlertLevelForm monitorId={monitorId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (route.kind === 'edit' && canManage) {
    return <AlertLevelForm levelId={route.levelId} monitorId={monitorId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (route.kind === 'vibration' && canManage) {
    return <VibrationAlertLevelForm monitorId={monitorId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  return (
    <AlertLevelsListPanel
      monitorId={monitorId}
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
      canManage={canManage}
    />
  );
}

// Function summary: Renders the AlertLevelsListPanel React component and wires its local UI behavior.
function AlertLevelsListPanel({ monitorId, locationPath, onNavigate, onRequestError, canManage }: AlertLevelsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [response, setResponse] = useState<QueryAlertLevelsResponse | null>(null);
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'alertField');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const manageAllowed = Boolean(canManage && response?.canManage);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}`);
  const returnPath = currentRoutePath(locationPath);

  const query = useMemo<QueryAlertLevelsRequest>(() => ({
    monitorId,
    page,
    pageSize,
    sort: sortKey,
    sortDir
  }), [monitorId, page, sortDir, sortKey]);
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildAlertLevelsUrl(monitorId, { page, sort: sortKey, sortDir }));
    setIsLoading(true);
    queryAlertLevels(query, { signal: controller.signal })
      .then((nextResponse) => {
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
  }, [monitorId, onRequestError, page, query, sortDir, sortKey]);

  async function handleDelete(level: AlertLevelItem) {
    if (!globalThis.confirm(`Delete ${level.alertType} ${level.alertField} alert level?`)) {
      return;
    }
    setNotice(null);
    setError(null);
    try {
      await deleteAlertLevel(level.id);
      setNotice('Alert level has been deleted.');
      const refreshed = await queryAlertLevels(query);
      setResponse(refreshed);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Monitor</p>
          <h2>{response?.fleetNumber || response?.serialId || 'Alert Levels'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Back</span>
          </button>
          {manageAllowed && response?.typeOfMonitor === 'Vibration' && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/vibration`, returnPath))}>
              <Edit3 size={17} aria-hidden="true" />
              <span>Vibration</span>
            </button>
          )}
          {manageAllowed && response?.typeOfMonitor !== 'Vibration' && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/new`, returnPath))}>
              <Plus size={17} aria-hidden="true" />
              <span>Add Level</span>
            </button>
          )}
        </div>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      {response?.typeOfMonitor === 'Vibration' && (
        <Notice tone="info" message="Vibration monitors use one Omnidots-backed alert/caution threshold pair." />
      )}
      <DataGrid
        columns={alertLevelColumnsForMonitorType(response?.typeOfMonitor)}
        rows={response?.results ?? []}
        getRowKey={(level) => level.id}
        emptyMessage="No alert levels are configured for this monitor."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={pageSize}
        total={response?.total ?? 0}
        totalPages={response?.totalPages ?? 0}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={manageAllowed && response?.typeOfMonitor !== 'Vibration'
          ? [
              {
                label: 'Edit alert level',
                icon: <Edit3 size={16} aria-hidden="true" />,
                onClick: (level) => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/${level.id}/edit`, returnPath))
              },
              {
                label: 'Delete alert level',
                icon: <Trash2 size={16} aria-hidden="true" />,
                onClick: handleDelete
              }
            ]
          : []}
      />
    </section>
  );
}

// Function summary: Renders the AlertLevelForm React component and wires its local UI behavior.
function AlertLevelForm({
  monitorId,
  levelId,
  locationPath,
  onNavigate,
  onRequestError
}: OperationsPanelProps & Readonly<{ monitorId: string; levelId?: string }>) {
  const [options, setOptions] = useState<AlertLevelOptionsResponse | null>(null);
  const [form, setForm] = useState<AlertLevelMutationRequest>(() => emptyAlertLevelForm(monitorId));
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}/alert-levels`);

  useEffect(() => {
    getAlertLevelOptions(monitorId)
      .then((nextOptions) => {
        setOptions(nextOptions);
        setForm((current) => ({
          ...current,
          alertField: current.alertField || nextOptions.alertFields[0]?.value || '',
          alertType: current.alertType || nextOptions.alertTypes[0]?.value || 'Alert',
          averagingPeriod: current.averagingPeriod || Number(nextOptions.averagingPeriods[0]?.value || 0)
        }));
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [monitorId, onRequestError]);

  useEffect(() => {
    if (!levelId) {
      return;
    }
    getAlertLevel(levelId)
      .then((response) => {
        const level = response.item;
        if (!level) {
          return;
        }
        setForm({
          monitorId: level.monitorId,
          alertField: level.alertField,
          limitOn: level.limitOn,
          limitOff: level.limitOff,
          alertType: level.alertType,
          averagingPeriod: level.averagingPeriod,
          weekdays: level.weekdays,
          saturdays: level.saturdays,
          sundays: level.sundays,
          startTime: level.startTime ?? '',
          endTime: level.endTime ?? ''
        });
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [levelId, onRequestError]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      if (levelId) {
        await updateAlertLevel(levelId, form);
      } else {
        await createAlertLevel(form);
      }
      onNavigate(backPath);
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
          <p>Alert Levels</p>
          <h2>{levelId ? 'Edit Alert Level' : 'Add Alert Level'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>Back</span>
        </button>
      </div>
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Parameter">
          <select value={form.alertField} onChange={(event) => setForm({ ...form, alertField: event.target.value })}>
            {options?.alertFields.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </FormField>
        <FormField label="Alert Type">
          <select value={form.alertType} onChange={(event) => setForm({ ...form, alertType: event.target.value })}>
            {options?.alertTypes.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </FormField>
        <FormField label="Limit On">
          <input value={form.limitOn || ''} inputMode="decimal" onChange={(event) => setForm({ ...form, limitOn: numberValue(event.target.value) })} />
        </FormField>
        <FormField label="Limit Off">
          <input value={form.limitOff || ''} inputMode="decimal" onChange={(event) => setForm({ ...form, limitOff: numberValue(event.target.value) })} />
        </FormField>
        <FormField label="Averaging Period">
          <select value={form.averagingPeriod} onChange={(event) => setForm({ ...form, averagingPeriod: Number(event.target.value) })}>
            {options?.averagingPeriods.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </FormField>
        <div className="checkbox-cluster" aria-label="Active days">
          <label className="checkbox-row">
            <input checked={form.weekdays} type="checkbox" onChange={(event) => setForm({ ...form, weekdays: event.target.checked })} />
            <span>Weekdays</span>
          </label>
          <label className="checkbox-row">
            <input checked={form.saturdays} type="checkbox" onChange={(event) => setForm({ ...form, saturdays: event.target.checked })} />
            <span>Saturday</span>
          </label>
          <label className="checkbox-row">
            <input checked={form.sundays} type="checkbox" onChange={(event) => setForm({ ...form, sundays: event.target.checked })} />
            <span>Sunday</span>
          </label>
        </div>
        {options?.typeOfMonitor === 'Noise' && form.averagingPeriod !== 0 && (
          <div className="time-grid">
            <FormField label="Start Time">
              <input value={form.startTime ?? ''} type="time" onChange={(event) => setForm({ ...form, startTime: event.target.value })} />
            </FormField>
            <FormField label="End Time">
              <input value={form.endTime ?? ''} type="time" onChange={(event) => setForm({ ...form, endTime: event.target.value })} />
            </FormField>
          </div>
        )}
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel="Save Alert Level" />
      </form>
    </section>
  );
}

// Function summary: Renders the VibrationAlertLevelForm React component and wires its local UI behavior.
function VibrationAlertLevelForm({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError
}: OperationsPanelProps & Readonly<{ monitorId: string }>) {
  const [alertLevel, setAlertLevel] = useState('');
  const [cautionLevel, setCautionLevel] = useState('');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}/alert-levels`);

  useEffect(() => {
    const controller = new AbortController();
    queryAlertLevels({ monitorId, page: 1, pageSize: 10, sort: 'alertType' }, { signal: controller.signal })
      .then((response) => {
        setAlertLevel(String(response.results.find((level) => level.alertType === 'Alert')?.limitOn ?? ''));
        setCautionLevel(String(response.results.find((level) => level.alertType === 'Caution')?.limitOn ?? ''));
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => controller.abort();
  }, [monitorId, onRequestError]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setNotice(null);
    setError(null);
    try {
      const response = await updateVibrationAlertLevels(monitorId, {
        alertLevel: numberValue(alertLevel),
        cautionLevel: numberValue(cautionLevel)
      });
      setNotice(response.externalSyncAttempted ? 'Vibration levels saved and synced.' : 'Vibration levels saved.');
      onNavigate(backPath);
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
          <p>Alert Levels</p>
          <h2>Vibration Thresholds</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>Back</span>
        </button>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Alert Level">
          <input value={alertLevel} inputMode="decimal" onChange={(event) => setAlertLevel(event.target.value)} />
        </FormField>
        <FormField label="Caution Level">
          <input value={cautionLevel} inputMode="decimal" onChange={(event) => setCautionLevel(event.target.value)} />
        </FormField>
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel="Save Vibration Levels" />
      </form>
    </section>
  );
}

const notificationTabs: Array<{ state: NotificationListState; label: string }> = [
  { state: 'open', label: 'Open Alerts' },
  { state: 'cautions', label: 'Cautions' },
  { state: 'all', label: 'All' }
];

const alertLevelColumns: DataGridColumn<AlertLevelItem>[] = [
  { key: 'alertField', header: 'Parameter', sortable: true, render: (level) => level.alertField },
  { key: 'alertType', header: 'Type', sortable: true, render: (level) => level.alertType },
  { key: 'limitOn', header: 'On', sortable: true, align: 'end', render: (level) => formatNumber(level.limitOn) },
  { key: 'limitOff', header: 'Off', sortable: true, align: 'end', render: (level) => formatNumber(level.limitOff) },
  { key: 'averagingPeriod', header: 'Average', sortable: true, render: (level) => level.averagingPeriodLabel || String(level.averagingPeriod) },
  { key: 'days', header: 'Days', render: (level) => formatDays(level) },
  { key: 'time', header: 'Time', render: (level) => level.startTime && level.endTime ? `${level.startTime}-${level.endTime}` : 'All day' }
];

function alertLevelColumnsForMonitorType(typeOfMonitor?: string | null): DataGridColumn<AlertLevelItem>[] {
  if (typeOfMonitor === 'Vibration') {
    return alertLevelColumns.filter((column) => column.key !== 'averagingPeriod');
  }

  return alertLevelColumns;
}

const relatedNotificationColumns: DataGridColumn<NotificationListItem>[] = [
  { key: 'notificationTime', header: 'Time', render: (notification) => formatDateTime(notification.notificationTime) },
  { key: 'alertType', header: 'Type', render: (notification) => notification.alertType },
  { key: 'limitName', header: 'Limit', render: (notification) => notification.limitName || notification.alertField },
  { key: 'level', header: 'Level', align: 'end', render: (notification) => formatNumber(notification.level) },
  { key: 'alertStatus', header: 'State', render: (notification) => <NotificationStatusChip notification={notification} /> }
];

// Function summary: Renders the NotificationStatusChip React component and wires its local UI behavior.
function NotificationStatusChip({ notification }: Readonly<{ notification: NotificationListItem }>) {
  return <span className={`status-chip ${notificationStatusClassName(notification)}`}>{notificationStatusLabel(notification)}</span>;
}

// Function summary: Handles the notification status label workflow for this module.
function notificationStatusLabel(notification: NotificationListItem) {
  if (notification.alertType === 'Caution') {
    return 'Caution';
  }
  if (notification.closedTime) {
    return 'Closed';
  }
  return 'Open';
}

// Function summary: Handles the notification status class name workflow for this module.
function notificationStatusClassName(notification: NotificationListItem) {
  if (notification.alertType === 'Caution') {
    return 'neutral';
  }
  if (notification.closedTime) {
    return 'success';
  }
  return 'danger';
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

// Function summary: Handles the parse notification route workflow for this module.
function parseNotificationRoute(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  const match = /^\/notifications\/([^/]+)$/i.exec(path);
  return { notificationId: match?.[1] ?? null };
}

// Function summary: Handles the parse alert level route workflow for this module.
function parseAlertLevelRoute(locationPath: string): AlertLevelRoute {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (/\/alert-levels\/new$/i.test(path)) {
    return { kind: 'new' };
  }
  if (/\/alert-levels\/vibration$/i.test(path)) {
    return { kind: 'vibration' };
  }
  const editMatch = /\/alert-levels\/([^/]+)\/edit$/i.exec(path);
  if (editMatch) {
    return { kind: 'edit', levelId: editMatch[1] };
  }
  return { kind: 'list' };
}

// Function summary: Builds notifications url data for callers.
function buildNotificationsUrl({
  state,
  searchText,
  page,
  sort,
  sortDir
}: {
  state: NotificationListState;
  searchText: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
}) {
  const params = new URLSearchParams({ state, page: String(page), sort, sortDir });
  if (searchText.trim()) {
    params.set('q', searchText.trim());
  }
  return `/notifications?${params.toString()}`;
}

// Function summary: Builds alert levels url data for callers.
function buildAlertLevelsUrl(monitorId: string, { page, sort, sortDir }: Readonly<{ page: number; sort: string; sortDir: SortDirection }>) {
  const params = new URLSearchParams({ page: String(page), sort, sortDir });
  return `/monitors/${monitorId}/alert-levels?${params.toString()}`;
}

// Function summary: Handles the normalize notification state workflow for this module.
function normalizeNotificationState(value: string | null): NotificationListState {
  return value === 'all' || value === 'cautions' || value === 'open' ? value : 'open';
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

// Function summary: Handles the empty alert level form workflow for this module.
function emptyAlertLevelForm(monitorId: string): AlertLevelMutationRequest {
  return {
    monitorId,
    alertField: '',
    limitOn: 0,
    limitOff: 0,
    alertType: 'Alert',
    averagingPeriod: 0,
    weekdays: true,
    saturdays: false,
    sundays: false,
    startTime: '',
    endTime: ''
  };
}

// Function summary: Handles the number value workflow for this module.
function numberValue(value: string) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
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

// Function summary: Handles the format number workflow for this module.
function formatNumber(value?: number | null) {
  return typeof value === 'number' ? value.toLocaleString(undefined, { maximumFractionDigits: 2 }) : '';
}

// Function summary: Handles optional text checks for conditional notification fields.
function hasText(value?: string | null) {
  return Boolean(value?.trim());
}

// Function summary: Handles the format days workflow for this module.
function formatDays(level: AlertLevelItem) {
  const days = [
    level.weekdays ? 'Weekdays' : null,
    level.saturdays ? 'Sat' : null,
    level.sundays ? 'Sun' : null
  ].filter(Boolean);
  return days.length > 0 ? days.join(', ') : 'Site hours';
}

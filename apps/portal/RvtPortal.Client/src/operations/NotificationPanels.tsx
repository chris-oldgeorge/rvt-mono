// File summary: Renders the notification list and detail panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-07-30 pending Split from NotificationAlertPanels.tsx so notifications and alert levels live in separate modules.

import { Bell, Check, ChevronLeft, Eye, Gauge, RefreshCcw, Search } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  batchCloseNotifications,
  closeNotification,
  getNotification,
  isAbortError,
  queryNotifications,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { formatDateTime, formatNumber } from '../format';
import { normalizeSortDirection, parsePositiveInt, useGridSortHandler } from '../gridQuery';
import { useRequestLifecycle } from '../requestLifecycle';
import { alertLevelColumnsForMonitorType } from './alertLevelColumns';
import { DetailItem } from './panelComponents';
import { pageSize } from './panelShared';
import type { ListExecution, OperationsRouteProps } from './panelShared';
import type {
  NotificationDetailResponse,
  NotificationListItem,
  NotificationListState,
  QueryNotificationsRequest,
  SortDirection,
} from '../dtos';

// Function summary: Renders the NotificationsPanel React component and wires its local UI behavior.
export function NotificationsPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const route = parseNotificationRoute(locationPath);
  if (route.notificationId) {
    return (
      <NotificationDetailPanel
        notificationId={route.notificationId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }

  return <NotificationListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the NotificationListPanel React component and wires its local UI behavior.
function NotificationListPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [state, setState] = useState<NotificationListState>(() =>
    normalizeNotificationState(initialParams.get('state')),
  );
  const [notifications, setNotifications] = useState<NotificationListItem[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'notificationTime');
  const [sortDir, setSortDir] = useState<SortDirection>(
    normalizeSortDirection(initialParams.get('sortDir'), 'Descending'),
  );
  const [closeNote, setCloseNote] = useState('');
  const [canClose, setCanClose] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QueryNotificationsRequest> | null>(null);
  const [refreshExecution, setRefreshExecution] = useState<ListExecution<QueryNotificationsRequest> | null>(null);
  const { claimRequest, ownsRequest, currentGeneration } = useRequestLifecycle();
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
        ),
      },
      {
        key: 'notificationTime',
        header: 'Time',
        sortable: true,
        render: (notification) => formatDateTime(notification.notificationTime),
      },
      {
        key: 'fleetNumber',
        header: 'Fleet',
        sortable: true,
        render: (notification) => notification.fleetNumber || 'Unassigned',
      },
      {
        key: 'siteName',
        header: 'Site',
        sortable: true,
        render: (notification) => notification.siteName || 'Not deployed',
      },
      { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (notification) => notification.typeOfMonitor },
      {
        key: 'limitName',
        header: 'Limit',
        sortable: true,
        render: (notification) => notification.limitName || notification.alertField,
      },
      {
        key: 'level',
        header: 'Level',
        sortable: true,
        align: 'end',
        render: (notification) => formatNumber(notification.level),
      },
      {
        key: 'alertStatus',
        header: 'State',
        sortable: true,
        render: (notification) => <NotificationStatusChip notification={notification} />,
      },
    ];

    if (showClosedNoteColumn) {
      nextColumns.push({
        key: 'closedNote',
        header: 'Closed note',
        render: (notification) => notification.closedNote?.trim() ?? '',
      });
    }

    return nextColumns;
  }, [selectedIds, showClosedNoteColumn]);

  const query = useMemo<QueryNotificationsRequest>(
    () => ({
      searchText,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
      state,
    }),
    [page, searchText, sortDir, sortKey, state],
  );
  const execution = useMemo<ListExecution<QueryNotificationsRequest>>(() => ({ query }), [query]);
  const currentExecution = refreshExecution?.query === query ? refreshExecution : execution;
  const isLoading = completedExecution !== currentExecution;
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);

  const refreshNotifications = useCallback(async () => {
    const nextExecution: ListExecution<QueryNotificationsRequest> = { query };
    const { controller, generation } = claimRequest();
    setRefreshExecution(nextExecution);
    setCompletedExecution(null);
    try {
      const response = await queryNotifications(nextExecution.query, { signal: controller.signal });
      if (!ownsRequest(controller, generation)) {
        return;
      }
      setNotifications(response.results);
      setTotal(response.total);
      setTotalPages(response.totalPages);
      setCanClose(response.canClose);
      setSelectedIds(new Set());
      setError(null);
      setCompletedExecution(nextExecution);
    } catch (err) {
      if (!ownsRequest(controller, generation) || isAbortError(err)) {
        return;
      }
      setError((err as Error).message);
      onRequestError(err);
      setCompletedExecution(nextExecution);
    }
  }, [claimRequest, onRequestError, ownsRequest, query]);

  useEffect(() => {
    const { controller, generation } = claimRequest();
    globalThis.history.replaceState(
      null,
      '',
      buildNotificationsUrl({ state, searchText, page, sort: sortKey, sortDir }),
    );
    queryNotifications(execution.query, { signal: controller.signal })
      .then((response) => {
        if (!ownsRequest(controller, generation)) {
          return;
        }
        setNotifications(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setCanClose(response.canClose);
        setSelectedIds(new Set());
        setError(null);
        setCompletedExecution(execution);
      })
      .catch((err: Error) => {
        if (!ownsRequest(controller, generation) || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedExecution(execution);
      });
    return () => controller.abort();
  }, [claimRequest, execution, onRequestError, ownsRequest, page, searchText, sortDir, sortKey, state]);

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
    const mutationGeneration = currentGeneration();
    setIsClosing(true);
    setNotice(null);
    setError(null);
    try {
      const response = await batchCloseNotifications({ notificationIds: Array.from(selectedIds), note: closeNote });
      setNotice(`Closed ${response.closedIds.length} of ${response.requested} selected notifications.`);
      setCloseNote('');
      if (currentGeneration() !== mutationGeneration) {
        return;
      }
      await refreshNotifications();
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
        <input
          value={searchText}
          onChange={(event) => handleSearch(event.target.value)}
          placeholder="Search notifications"
        />
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
            onClick: (notification) => onNavigate(withReturnTo(`/notifications/${notification.id}`, returnPath)),
          },
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
  onRequestError,
}: OperationsRouteProps & Readonly<{ notificationId: string }>) {
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

  async function handleClose(event: SyntheticEvent) {
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
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${notification.monitorId}/alert-levels`, detailPath))}
            >
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
            <DetailItem
              label="Status"
              value={notification.closedTime ? 'Closed' : notification.alertStatus || 'Open'}
            />
            <DetailItem label="Raised" value={formatDateTime(notification.notificationTime)} />
            <DetailItem label="Closed" value={formatDateTime(notification.closedTime) || 'Open'} />
            {hasText(notification.closedNote) && (
              <DetailItem label="Closed Note" value={notification.closedNote?.trim() ?? ''} />
            )}
            <DetailItem label="Location" value={notification.location || 'Not recorded'} />
            <DetailItem label="What3words" value={notification.what3words || 'Not recorded'} />
            <DetailItem
              label="Graph Window"
              value={`${formatDateTime(notification.graphFromUtc)} to ${formatDateTime(notification.graphToUtc)}`}
            />
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
                  onClick: (related) => onNavigate(withReturnTo(`/notifications/${related.id}`, detailPath)),
                },
              ]}
            />
          </section>
        </>
      )}
    </section>
  );
}

const notificationTabs: Array<{ state: NotificationListState; label: string }> = [
  { state: 'open', label: 'Open Alerts' },
  { state: 'cautions', label: 'Cautions' },
  { state: 'all', label: 'All' },
];

const relatedNotificationColumns: DataGridColumn<NotificationListItem>[] = [
  { key: 'notificationTime', header: 'Time', render: (notification) => formatDateTime(notification.notificationTime) },
  { key: 'alertType', header: 'Type', render: (notification) => notification.alertType },
  { key: 'limitName', header: 'Limit', render: (notification) => notification.limitName || notification.alertField },
  { key: 'level', header: 'Level', align: 'end', render: (notification) => formatNumber(notification.level) },
  {
    key: 'alertStatus',
    header: 'State',
    render: (notification) => <NotificationStatusChip notification={notification} />,
  },
];

// Function summary: Renders the NotificationStatusChip React component and wires its local UI behavior.
function NotificationStatusChip({ notification }: Readonly<{ notification: NotificationListItem }>) {
  return (
    <span className={`status-chip ${notificationStatusClassName(notification)}`}>
      {notificationStatusLabel(notification)}
    </span>
  );
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

// Function summary: Handles the parse notification route workflow for this module.
function parseNotificationRoute(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  const match = /^\/notifications\/([^/]+)$/i.exec(path);
  return { notificationId: match?.[1] ?? null };
}

// Function summary: Builds notifications url data for callers.
function buildNotificationsUrl({
  state,
  searchText,
  page,
  sort,
  sortDir,
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

// Function summary: Handles the normalize notification state workflow for this module.
function normalizeNotificationState(value: string | null): NotificationListState {
  return value === 'all' || value === 'cautions' || value === 'open' ? value : 'open';
}

// Function summary: Handles optional text checks for conditional notification fields.
function hasText(value?: string | null) {
  return Boolean(value?.trim());
}

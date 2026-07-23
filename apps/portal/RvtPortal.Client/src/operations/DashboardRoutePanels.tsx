// File summary: Hosts dashboard-adjacent route panels that are lazy-loaded outside the core SPA shell.
// Major updates:
// - 2026-07-08 pending Split map and calendar panels from the eager dashboard module to reduce the initial bundle.

import { AlertTriangle, CalendarDays, ChevronLeft, MapPin, RefreshCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import {
  getCalendarDay,
  getCalendarMonth,
  getDashboardSummary,
  isAbortError,
  queryMapMarkers
} from '../api/client';
import { Notice } from '../components/FormControls';
import { MonitorMap, MonitorMarkerList } from '../components/MonitorMap';
import type {
  CalendarDayResponse,
  CalendarMonthDayItem,
  CalendarMonthResponse,
  DashboardNotificationItem,
  DashboardSummaryResponse,
  MapMarkersResponse,
  OptionItem
} from '../dtos';

type DashboardRoutePanelProps = Readonly<{
  locationPath: string;
  onRequestError: (error: unknown) => void;
}>;

// Function summary: Renders the MapPanel React component and wires its local UI behavior.
export function MapPanel({ locationPath, onRequestError }: DashboardRoutePanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [siteId, setSiteId] = useState(initialParams.get('siteId') ?? '');
  const [summary, setSummary] = useState<DashboardSummaryResponse | null>(null);
  const [markers, setMarkers] = useState<MapMarkersResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', `/maps${mapQuery(siteId)}`);
    setIsLoading(true);
    Promise.all([
      getDashboardSummary({ signal: controller.signal }),
      queryMapMarkers(mapMarkersRequest(siteId), { signal: controller.signal })
    ])
      .then(([nextSummary, nextMarkers]) => {
        setSummary(nextSummary);
        setMarkers(nextMarkers);
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
  }, [onRequestError, siteId]);

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Site and user maps</p>
          <h2>Monitor Map</h2>
        </div>
        <MapPin size={22} aria-hidden="true" />
      </div>
      <label className="form-field compact-select">
        <span>Site</span>
        <select value={siteId} onChange={(event) => setSiteId(event.target.value)}>
          <option value="">All visible sites</option>
          {(summary?.sites ?? []).map((site) => (
            <option value={site.value} key={site.value}>{site.label}</option>
          ))}
        </select>
      </label>
      {error && <Notice tone="error" message={error} />}
      {isLoading && <LoadingInline label="Loading map" />}
      <MonitorMap markers={markers?.markers ?? []} />
      <MonitorMarkerList markers={markers?.markers ?? []} />
    </section>
  );
}

// Function summary: Renders the CalendarPanel React component and wires its local UI behavior.
export function CalendarPanel({ locationPath, onRequestError }: DashboardRoutePanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const initialDate = useMemo(() => initialCalendarDate(initialParams), [initialParams]);
  const [deployments, setDeployments] = useState<OptionItem[]>([]);
  const [deploymentId, setDeploymentId] = useState(initialParams.get('deploymentId') ?? '');
  const [year, setYear] = useState(initialDate.year);
  const [month, setMonth] = useState(initialDate.month);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [monthData, setMonthData] = useState<CalendarMonthResponse | null>(null);
  const [dayData, setDayData] = useState<CalendarDayResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    getDashboardSummary({ signal: controller.signal })
      .then((summary) => {
        setDeployments(summary.calendarDeployments);
        if (!deploymentId && summary.calendarDeployments[0]) {
          setDeploymentId(summary.calendarDeployments[0].value);
        }
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => controller.abort();
  }, [deploymentId, onRequestError]);

  useEffect(() => {
    if (!deploymentId) {
      return;
    }
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildCalendarUrl(deploymentId, year, month));
    setIsLoading(true);
    getCalendarMonth({ deploymentId, year, month }, { signal: controller.signal })
      .then((response) => {
        setMonthData(response);
        setDeployments(response.deployments);
        setYear(response.year);
        setMonth(response.month);
        setSelectedDate(defaultSelectedDate(response));
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
  }, [deploymentId, month, onRequestError, year]);

  useEffect(() => {
    if (!monthData || !selectedDate) {
      setDayData(null);
      return;
    }
    const controller = new AbortController();
    const date = parseCalendarDate(selectedDate);
    getCalendarDay({ monitorId: monthData.monitorId, ...date }, { signal: controller.signal })
      .then((response) => {
        if (!controller.signal.aborted) {
          setDayData(response);
        }
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => controller.abort();
  }, [monthData, onRequestError, selectedDate]);

  // Function summary: Handles the move month workflow for this module.
  function moveMonth(direction: -1 | 1) {
    const next = new Date(year, month - 1 + direction, 1);
    setYear(next.getFullYear());
    setMonth(next.getMonth() + 1);
  }

  return (
    <section className="calendar-layout">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>Monitor calendar</p>
            <h2>Calendar</h2>
          </div>
          <CalendarDays size={22} aria-hidden="true" />
        </div>
        <div className="calendar-toolbar">
          <label className="form-field compact-select">
            <span>Deployment</span>
            <select value={deploymentId} onChange={(event) => setDeploymentId(event.target.value)}>
              <option value="">Select deployment</option>
              {deployments.map((deployment) => (
                <option value={deployment.value} key={deployment.value}>{deployment.label}</option>
              ))}
            </select>
          </label>
          <div className="button-row">
            <button className="secondary-button" type="button" onClick={() => moveMonth(-1)} disabled={!deploymentId}>
              <ChevronLeft size={17} aria-hidden="true" />
              <span>Previous</span>
            </button>
            <button className="secondary-button" type="button" onClick={() => moveMonth(1)} disabled={!deploymentId}>
              <span>Next</span>
            </button>
          </div>
        </div>
        {error && <Notice tone="error" message={error} />}
        {isLoading && <LoadingInline label="Loading calendar" />}
        {monthData && (
          <>
            <div className="calendar-heading">
              <strong>{monthName(monthData.year, monthData.month)}</strong>
              <span>{monthData.fleetNumber || monthData.serialId} / {monthData.typeOfMonitor}</span>
            </div>
            <div className="calendar-grid" role="grid" aria-label="Monitor month calendar">
              {weekdayLabels.map((label) => (
                <div className="calendar-weekday" key={label}>{label}</div>
              ))}
              {monthData.days.map((day) => (
                <CalendarDayButton
                  day={day}
                  selectedDate={selectedDate}
                  key={day.date}
                  onSelect={setSelectedDate}
                />
              ))}
            </div>
          </>
        )}
      </section>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>Selected day</p>
            <h2>{dayData ? formatDate(dayData.displayDay) : 'Day Detail'}</h2>
          </div>
          <AlertTriangle size={22} aria-hidden="true" />
        </div>
        {dayData ? <CalendarDayDetail day={dayData} /> : <p className="muted-text">Select a deployment and day to view readings.</p>}
      </section>
    </section>
  );
}

// Function summary: Renders the CalendarDayDetail React component and wires its local UI behavior.
function CalendarDayDetail({ day }: Readonly<{ day: CalendarDayResponse }>) {
  return (
    <div className="detail-stack">
      <div className="detail-grid">
        <DetailItem label="Monitor" value={`${day.fleetNumber} / ${day.typeOfMonitor}`} />
        <DetailItem label="Unit" value={day.unit || 'n/a'} />
        <DetailItem label="Notifications" value={String(day.notifications.length)} />
      </div>
      <section>
        <h3>Values</h3>
        {day.values.length === 0 && <p className="muted-text">No alert readings were recorded for this day.</p>}
        {day.values.map((value) => (
          <div className="readonly-row" key={value.label}>
            <span>{value.label}</span>
            <strong>{formatNumber(value.value)} {day.unit}</strong>
          </div>
        ))}
      </section>
      <section>
        <h3>Alert Levels</h3>
        {day.alertLevels.length === 0 && <p className="muted-text">No alert levels are configured for this monitor.</p>}
        {day.alertLevels.map((level) => (
          <div className="readonly-row" key={level.id}>
            <span>{level.alertField} / {level.alertType}</span>
            <strong>{formatNumber(level.limitOn)} on, {formatNumber(level.limitOff)} off</strong>
          </div>
        ))}
      </section>
      <section>
        <h3>Notifications</h3>
        <NotificationList notifications={day.notifications} />
      </section>
    </div>
  );
}

// Function summary: Renders the CalendarDayButton React component and wires its local UI behavior.
function CalendarDayButton({
  day,
  selectedDate,
  onSelect
}: Readonly<{
  day: CalendarMonthDayItem;
  selectedDate: string | null;
  onSelect: (date: string) => void;
}>) {
  const date = new Date(day.date);
  const dayNumber = date.getDate();
  const isSelected = selectedDate === day.date;
  return (
    <button
      className={calendarDayClassName(day, isSelected)}
      type="button"
      onClick={() => onSelect(day.date)}
    >
      <span>{dayNumber}</span>
      <strong>{day.status}</strong>
      {day.notificationCount > 0 && <em>{day.notificationCount}</em>}
    </button>
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

// Function summary: Renders the DetailItem React component and wires its local UI behavior.
function DetailItem({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

// Function summary: Renders an inline route-loading indicator without pulling in a heavier panel module.
function LoadingInline({ label }: Readonly<{ label: string }>) {
  return (
    <div className="loading-inline">
      <RefreshCcw size={16} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}

// Function summary: Maps query into the shape required by callers.
function mapQuery(siteId: string) {
  if (!siteId) {
    return '';
  }

  return `?siteId=${encodeURIComponent(siteId)}`;
}

// Function summary: Maps markers request into the shape required by callers.
function mapMarkersRequest(siteId: string) {
  if (!siteId) {
    return {};
  }

  return { siteId };
}

// Function summary: Handles the notification tone workflow for this module.
function notificationTone(notification: DashboardNotificationItem) {
  if (notification.alertType === 'Alert') {
    return 'danger';
  }

  return 'neutral';
}

// Function summary: Handles the calendar day class name workflow for this module.
function calendarDayClassName(day: CalendarMonthDayItem, isSelected: boolean) {
  const classes = ['calendar-day', day.status.toLowerCase()];
  if (!day.isCurrentMonth) {
    classes.push('muted');
  }
  if (isSelected) {
    classes.push('selected');
  }

  return classes.join(' ');
}

// Function summary: Handles the default selected day workflow for this module.
function defaultSelectedDate(month: CalendarMonthResponse) {
  const alertDay = month.days.find((day) => day.isCurrentMonth && day.notificationCount > 0);
  if (alertDay) {
    return alertDay.date;
  }

  const currentMonthDay = month.days.find((day) => day.isCurrentMonth);
  if (!currentMonthDay) {
    return null;
  }

  return currentMonthDay.date;
}

// Function summary: Converts a calendar ISO date into the day-query fields without browser time-zone conversion.
function parseCalendarDate(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return { year, month, day };
}

// Function summary: Handles the initial calendar date workflow for this module.
function initialCalendarDate(params: URLSearchParams) {
  const now = new Date();
  return {
    year: parsePositiveInt(params.get('year'), now.getFullYear()),
    month: parsePositiveInt(params.get('month'), now.getMonth() + 1)
  };
}

// Function summary: Builds calendar url data for callers.
function buildCalendarUrl(deploymentId: string, year: number, month: number) {
  const params = new URLSearchParams({ deploymentId, year: String(year), month: String(month) });
  return `/calendar?${params.toString()}`;
}

// Function summary: Handles the month name workflow for this module.
function monthName(year: number, month: number) {
  return new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
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

// Function summary: Handles the format number workflow for this module.
function formatNumber(value: number) {
  return new Intl.NumberFormat('en-GB', { maximumFractionDigits: 2 }).format(value);
}

// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number.parseInt(value ?? '', 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallback;
  }

  return parsed;
}

const weekdayLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

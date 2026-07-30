// File summary: Renders the monitor detail panel with metric cards, map context, and notification drill-through.
// Major updates:
// - 2026-07-30 pending Confirmed contract removal before it runs.
// - 2026-07-30 pending Extracted from MonitorPanels.tsx during the monitor panel split.

import { BarChart3, Bell, Edit3, Eye, Image, MapPinned, SlidersHorizontal, Trash2, Wrench } from 'lucide-react';
import { useEffect, useState } from 'react';
import {
  ApiError,
  getInstallerMonitor,
  getInstallerMonitorStatus,
  getMonitor,
  isAbortError,
  removeMonitorFromContract,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { ConfirmDialog, Notice } from '../components/FormControls';
import { MonitorMap, MonitorMarkerList } from '../components/MonitorMap';
import { formatDate, formatDateTime } from '../format';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import type { MonitorsPanelProps } from './monitorShared';
import type {
  InstallerMonitorStatusResponse,
  MapMonitorMarker,
  MonitorAlertLevelItem,
  MonitorDetailResponse,
  MonitorMetricSummary,
  MonitorNotificationItem,
} from '../dtos';

// Function summary: Renders the MonitorDetailPanel React component and wires its local UI behavior.
export function MonitorDetailPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage,
  canUseInstallerTools,
  installerOnly,
}: MonitorsPanelProps & Readonly<{ monitorId: string }>) {
  const [monitor, setMonitor] = useState<MonitorDetailResponse | null>(null);
  const [status, setStatus] = useState<InstallerMonitorStatusResponse | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const [confirmRemove, setConfirmRemove] = useState(false);
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
    const controller = new AbortController();
    getInstallerMonitorStatus(monitorId, { signal: controller.signal })
      .then((response) => {
        setStatus(response);
        setStatusError(null);
      })
      .catch((err: Error) => {
        if (controller.signal.aborted || isAbortError(err)) {
          return;
        }
        setStatus(null);
        // A 404 is the API's "no status recorded" answer; anything else is a
        // failed check and must not silently render as a healthy fallback.
        setStatusError(err instanceof ApiError && err.status === 404 ? null : err.message);
      });
    return () => controller.abort();
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
      setConfirmRemove(false);
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
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            Back
          </button>
          {canManage && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/edit`, detailPath))}
              disabled={!monitor}
            >
              <Edit3 size={17} aria-hidden="true" />
              <span>Edit</span>
            </button>
          )}
          {canUseInstallerTools && monitor?.canInstallerEdit && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/installer`, detailPath))}
            >
              <Wrench size={17} aria-hidden="true" />
              <span>Deployment</span>
            </button>
          )}
          {canManage && monitor?.siteId && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/assign?siteId=${monitor.siteId}`, detailPath))}
            >
              <SlidersHorizontal size={17} aria-hidden="true" />
              <span>Assignments</span>
            </button>
          )}
          {canManage && monitor?.isAssigned && (
            <button
              className="danger-button"
              type="button"
              onClick={() => setConfirmRemove(true)}
              disabled={isRemoving}
            >
              <Trash2 size={17} aria-hidden="true" />
              <span>{isRemoving ? 'Removing' : 'Remove'}</span>
            </button>
          )}
          {!installerOnly && monitor && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels`, detailPath))}
            >
              <Bell size={17} aria-hidden="true" />
              <span>Alert Levels</span>
            </button>
          )}
          {!installerOnly && monitor?.deploymentId && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/data?deploymentId=${monitor.deploymentId}`, detailPath))}
            >
              <BarChart3 size={17} aria-hidden="true" />
              <span>Data</span>
            </button>
          )}
          {!installerOnly && monitor?.siteId && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/maps?siteId=${monitor.siteId}`, detailPath))}
            >
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
            <DetailItem
              label="Status"
              value={
                statusError
                  ? 'Status check failed'
                  : status?.status || monitor.statusLabel || (monitor.isOffline ? 'Offline' : 'Online')
              }
            />
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
              rowActions={[
                {
                  label: 'View notification',
                  icon: <Eye size={16} aria-hidden="true" />,
                  onClick: (notification) => onNavigate(withReturnTo(`/notifications/${notification.id}`, detailPath)),
                },
              ]}
            />
          </section>
        </>
      )}
      <ConfirmDialog
        open={confirmRemove}
        title="Remove monitor from contract"
        message={`Remove ${monitor?.fleetNumber || monitor?.serialId || 'this monitor'} from ${monitor?.contractNumber || 'its contract'}? The monitor returns to the unassigned pool.`}
        confirmLabel="Remove from contract"
        isBusy={isRemoving}
        onCancel={() => setConfirmRemove(false)}
        onConfirm={handleRemoveAssignment}
      />
    </section>
  );
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
  { key: 'isActive', header: 'Active', render: (level) => (level.isActive ? 'Yes' : 'No') },
];

const notificationColumns: DataGridColumn<MonitorNotificationItem>[] = [
  { key: 'notificationTime', header: 'Time', render: (notification) => formatDateTime(notification.notificationTime) },
  { key: 'alertType', header: 'Type', render: (notification) => notification.alertType },
  { key: 'alertField', header: 'Field', render: (notification) => notification.alertField },
  { key: 'level', header: 'Level', render: (notification) => notification.level },
  { key: 'limitOn', header: 'Limit', render: (notification) => notification.limitOn },
  { key: 'closedTime', header: 'State', render: (notification) => (notification.closedTime ? 'Closed' : 'Open') },
];

// Function summary: Renders a legacy monitor metric summary card.
function MonitorMetricCard({
  title,
  metric,
  fallback,
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

  return [
    {
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
      what3words: monitor.what3words,
    },
  ];
}

// Function summary: Formats monitor metric values with their unit for display.
function formatMetricValue(metric?: MonitorMetricSummary | null) {
  if (metric?.value === null || metric?.value === undefined) {
    return '';
  }

  const unit = metric.unit ? ` ${metric.unit}` : '';
  return `${metric.value}${unit}`;
}

// Function summary: Handles the format coordinates workflow for this module.
function formatCoordinates(lat?: number | null, lng?: number | null) {
  if (typeof lat !== 'number' || typeof lng !== 'number') {
    return 'Not recorded';
  }
  return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

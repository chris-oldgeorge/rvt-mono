// File summary: Renders the site detail panel with its assignments, map, and notification-settings sections.
// Major updates:
// - 2026-07-30 pending Closed the archive confirmation on failure so the error notice is not covered.
// - 2026-07-30 pending Extracted from ContractSitePanels.tsx during the contracts/sites split.

import {
  Archive,
  BarChart3,
  Bell,
  CalendarDays,
  Edit3,
  Eye,
  FileText,
  Gauge,
  MapPinned,
  Save,
  Settings,
  Star,
  Trash2,
  UserPlus,
  UserRound,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  addUserToSite,
  archiveSite,
  getSite,
  getSiteAssignments,
  getSiteNotificationSettings,
  removeSiteContactUser,
  removeUserFromSite,
  setSiteContactUser,
  updateSiteNotificationSetting,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { ConfirmDialog, Notice } from '../components/FormControls';
import { MonitorMap, MonitorMarkerList } from '../components/MonitorMap';
import { ReadOnlyRow } from '../components/ReadOnlyRow';
import { formatDate, formatDateTime } from '../format';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { notificationSettingDraft, withoutNotificationDraft } from './notificationDrafts';
import type { NotificationDraftOverrides } from './notificationDrafts';
import { normalizeOperatingHours } from './contractSiteShared';
import type { OperationsPanelCallbacks } from './contractSiteShared';
import type {
  MapMonitorMarker,
  SiteAssignmentResponse,
  SiteDetailResponse,
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
  SiteNotificationSettingsResponse,
  SiteUserAssignmentItem,
} from '../dtos';

type SiteDetailPanelProps = OperationsPanelCallbacks &
  Readonly<{
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

// Function summary: Renders the SiteDetailPanel React component and wires its local UI behavior.
export function SiteDetailPanel({
  siteId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage = false,
  currentUserId,
}: SiteDetailPanelProps) {
  const [site, setSite] = useState<SiteDetailResponse | null>(null);
  const [settings, setSettings] = useState<SiteNotificationSettingsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmArchive, setConfirmArchive] = useState(false);
  const [isArchiving, setIsArchiving] = useState(false);
  // One array per site: rebuilding it inline gave the map, the marker list and the
  // visibility check three different arrays, remounting Leaflet on every render.
  const markers = useMemo(() => (site ? siteMonitorMarkers(site) : []), [site]);
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
    setError(null);
    try {
      const response = await archiveSite(siteId);
      setSite(response.item ?? null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      // Close on failure too: the error notice renders on the panel, which a modal
      // confirmation would cover, so leaving it open hides the reason it failed.
      setConfirmArchive(false);
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
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            Back
          </button>
          {site && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/maps?siteId=${site.id}`, detailPath))}
            >
              <MapPinned size={17} aria-hidden="true" />
              <span>Open map</span>
            </button>
          )}
          {site?.monitors[0]?.deploymentId && (
            <>
              <button
                className="secondary-button"
                type="button"
                onClick={() =>
                  onNavigate(withReturnTo(`/data?deploymentId=${site.monitors[0].deploymentId}`, detailPath))
                }
              >
                <BarChart3 size={17} aria-hidden="true" />
                <span>Open data</span>
              </button>
              <button
                className="secondary-button"
                type="button"
                onClick={() =>
                  onNavigate(withReturnTo(`/calendar?deploymentId=${site.monitors[0].deploymentId}`, detailPath))
                }
              >
                <CalendarDays size={17} aria-hidden="true" />
                <span>Open calendar</span>
              </button>
            </>
          )}
          {site && (
            <button
              className="secondary-button"
              type="button"
              onClick={() =>
                onNavigate(withReturnTo(`/notifications?q=${encodeURIComponent(site.siteName)}`, detailPath))
              }
            >
              <Bell size={17} aria-hidden="true" />
              <span>Open notifications</span>
            </button>
          )}
          {canManage && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/sites/${siteId}/edit`, detailPath))}
              disabled={!site || site.archived}
            >
              <Edit3 size={17} aria-hidden="true" />
              <span>Edit</span>
            </button>
          )}
          {canManage && (
            <button
              className="danger-button"
              type="button"
              onClick={() => setConfirmArchive(true)}
              disabled={!site || site.archived}
            >
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
              {site.archive && (
                <ReadOnlyRow
                  label="Archived"
                  value={`${formatDate(site.archive.archived)} by ${site.archive.createdBy || 'Unknown'}`}
                />
              )}
            </div>
          </div>
          {markers.length > 0 && (
            <NestedSection title="Map" icon={<MapPinned size={18} aria-hidden="true" />}>
              <MonitorMap markers={markers} label="Site detail map" />
              <MonitorMarkerList markers={markers} />
            </NestedSection>
          )}
          <NestedSection title="Contracts" icon={<FileText size={18} aria-hidden="true" />}>
            <DataGrid
              columns={[
                { key: 'contractNumber', header: 'Contract', render: (contract) => contract.contractNumber },
                { key: 'companyName', header: 'Company', render: (contract) => contract.companyName || 'None' },
                { key: 'onHireDate', header: 'On Hire', render: (contract) => formatDate(contract.onHireDate) },
                {
                  key: 'offHireDate',
                  header: 'Off Hire',
                  render: (contract) => formatDate(contract.offHireDate) || 'Open',
                },
              ]}
              rows={site.contractList}
              getRowKey={(contract) => contract.id}
              emptyMessage="No contracts are assigned to this site."
              page={1}
              pageSize={Math.max(site.contractList.length, 1)}
              total={site.contractList.length}
              totalPages={site.contractList.length > 0 ? 1 : 0}
              rowActions={
                canManage
                  ? [
                      {
                        label: 'View contract',
                        icon: <Eye size={16} aria-hidden="true" />,
                        onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}`, detailPath)),
                      },
                    ]
                  : []
              }
            />
          </NestedSection>
          <NestedSection title="Current Monitors" icon={<Gauge size={18} aria-hidden="true" />}>
            <DataGrid
              columns={[
                { key: 'fleetNumber', header: 'Fleet Nr', render: (monitor) => monitor.fleetNumber || 'None' },
                { key: 'serialId', header: 'Serial', render: (monitor) => monitor.serialId || 'None' },
                { key: 'typeOfMonitor', header: 'Type', render: (monitor) => monitor.typeOfMonitor },
                { key: 'contractNumber', header: 'Contract', render: (monitor) => monitor.contractNumber },
                {
                  key: 'lastDataTime',
                  header: 'Last Data',
                  render: (monitor) => formatDateTime(monitor.lastDataTime) || 'None',
                },
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
                {
                  key: 'fleetNumber',
                  header: 'Fleet Nr',
                  render: (notification) => notification.fleetNumber || 'None',
                },
                { key: 'alertField', header: 'Field', render: (notification) => notification.alertField || 'None' },
                { key: 'level', header: 'Level', render: (notification) => notification.level ?? '' },
                { key: 'limitOn', header: 'Limit', render: (notification) => notification.limitOn ?? '' },
                {
                  key: 'notificationTime',
                  header: 'Time',
                  render: (notification) => formatDateTime(notification.notificationTime),
                },
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
          {canManage && <SiteAssignmentsPanel siteId={siteId} onRequestError={onRequestError} />}
          {settings && (
            <NotificationSettingsPanel
              key={settings.siteId}
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

// Function summary: Renders the SiteAssignmentsPanel React component and wires its local UI behavior.
function SiteAssignmentsPanel({
  siteId,
  onRequestError,
}: Readonly<{ siteId: string; onRequestError: (error: unknown) => void }>) {
  const [assignments, setAssignments] = useState<SiteAssignmentResponse | null>(null);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [confirmRemoveUser, setConfirmRemoveUser] = useState<SiteUserAssignmentItem | null>(null);
  useEffect(() => {
    let isCurrent = true;
    getSiteAssignments(siteId)
      .then((response) => {
        if (!isCurrent) {
          return;
        }
        setAssignments(response.item ?? null);
        setError(null);
      })
      .catch((err: Error) => {
        if (!isCurrent) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => {
      isCurrent = false;
    };
  }, [onRequestError, siteId]);
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
  const assignedColumns = useMemo<DataGridColumn<SiteUserAssignmentItem>[]>(
    () => [
      {
        key: 'email',
        header: 'User',
        render: (user) => (
          <span className="cell-with-icon">
            <UserRound size={16} aria-hidden="true" />
            {user.email}
          </span>
        ),
      },
      { key: 'name', header: 'Name', render: (user) => user.name || 'None' },
      { key: 'companyRole', header: 'Role', render: (user) => user.companyRole || 'None' },
      {
        key: 'siteContact',
        header: 'Contact',
        render: (user) => (user.siteContact ? <span className="status-chip">Contact</span> : 'No'),
      },
    ],
    [],
  );
  return (
    <NestedSection title="Site Users" icon={<UserRound size={18} aria-hidden="true" />}>
      {error && <Notice tone="error" message={error} />}
      {assignments && (
        <>
          <div className="assignment-toolbar">
            <select
              value={selectedUserId}
              onChange={(event) => setSelectedUserId(event.target.value)}
              disabled={isBusy}
            >
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
                disabled: (user) => isBusy || user.siteContact,
              },
              {
                label: 'Unset site contact',
                icon: <X size={16} aria-hidden="true" />,
                onClick: (user) => runMutation(() => removeSiteContactUser({ siteId, userId: user.id })),
                disabled: (user) => isBusy || !user.siteContact,
              },
              {
                label: 'Remove user from site',
                icon: <Trash2 size={16} aria-hidden="true" />,
                onClick: (user) => setConfirmRemoveUser(user),
                disabled: () => isBusy,
              },
            ]}
          />
        </>
      )}
      <ConfirmDialog
        open={confirmRemoveUser !== null}
        title="Remove user from site"
        message={`Remove ${confirmRemoveUser?.email ?? 'this user'} from this site?`}
        confirmLabel="Remove"
        onCancel={() => setConfirmRemoveUser(null)}
        onConfirm={() => {
          const user = confirmRemoveUser;
          setConfirmRemoveUser(null);
          if (user) {
            void runMutation(() => removeUserFromSite({ siteId, userId: user.id }));
          }
        }}
      />
    </NestedSection>
  );
}

// Function summary: Renders the NotificationSettingsPanel React component and wires its local UI behavior.
function NotificationSettingsPanel({
  settings,
  canManage,
  currentUserId,
  onUpdated,
  onRequestError,
}: NotificationSettingsPanelProps) {
  const visibleSettings = canManage
    ? settings.settings
    : settings.settings.filter((setting) => setting.userId.toLowerCase() === (currentUserId ?? '').toLowerCase());
  const [draftOverrides, setDraftOverrides] = useState<NotificationDraftOverrides>({});
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  async function handleSave(setting: SiteNotificationSettingItem) {
    setSavingId(setting.siteUserId);
    setError(null);
    try {
      const draft = notificationSettingDraft(setting, draftOverrides);
      const response = await updateSiteNotificationSetting(settings.siteId, setting.siteUserId, {
        email: draft.email,
        sms: draft.sms,
        startTime: draft.startTime || null,
        endTime: draft.endTime || null,
      });
      const updatedItem = response.item;
      if (updatedItem) {
        onUpdated({
          ...settings,
          settings: settings.settings.map((item) => (item.siteUserId === updatedItem.siteUserId ? updatedItem : item)),
        });
        setDraftOverrides((current) =>
          notificationDraftMatches(current[setting.siteUserId], draft)
            ? withoutNotificationDraft(current, setting.siteUserId)
            : current,
        );
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setSavingId(null);
    }
  }
  // Function summary: Updates draft data for the current workflow.
  function updateDraft(setting: SiteNotificationSettingItem, patch: Partial<SiteNotificationSettingMutationRequest>) {
    setDraftOverrides((current) => ({
      ...current,
      [setting.siteUserId]: {
        ...notificationSettingDraft(setting, current),
        ...patch,
      },
    }));
  }
  return (
    <NestedSection title="Notification Settings" icon={<Settings size={18} aria-hidden="true" />}>
      {error && <Notice tone="error" message={error} />}
      {visibleSettings.length === 0 && (
        <Notice tone="info" message="No notification settings are available for this site." />
      )}
      {visibleSettings.length > 0 && (
        <div className="settings-list">
          {visibleSettings.map((setting) => {
            const draft = notificationSettingDraft(setting, draftOverrides);
            return (
              <div className="setting-row" key={setting.siteUserId}>
                <div>
                  <strong>{setting.userName || setting.userEmail}</strong>
                  <span>{setting.siteContact ? 'Site contact' : setting.userEmail}</span>
                </div>
                <label className="checkbox-row">
                  <input
                    checked={draft.email}
                    onChange={(event) => updateDraft(setting, { email: event.target.checked })}
                    type="checkbox"
                  />
                  <span>Email</span>
                </label>
                <label className="checkbox-row">
                  <input
                    checked={draft.sms}
                    onChange={(event) => updateDraft(setting, { sms: event.target.checked })}
                    type="checkbox"
                  />
                  <span>SMS</span>
                </label>
                <input
                  aria-label={`${setting.userEmail} notification start time`}
                  value={draft.startTime ?? ''}
                  onChange={(event) => updateDraft(setting, { startTime: event.target.value })}
                  type="time"
                />
                <input
                  aria-label={`${setting.userEmail} notification end time`}
                  value={draft.endTime ?? ''}
                  onChange={(event) => updateDraft(setting, { endTime: event.target.value })}
                  type="time"
                />
                <button
                  className="secondary-button"
                  type="button"
                  onClick={() => handleSave(setting)}
                  disabled={savingId === setting.siteUserId}
                >
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

function notificationDraftMatches(
  current: SiteNotificationSettingMutationRequest | undefined,
  submitted: SiteNotificationSettingMutationRequest,
) {
  return (
    current?.email === submitted.email &&
    current.sms === submitted.sms &&
    current.startTime === submitted.startTime &&
    current.endTime === submitted.endTime
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
      alert: site.openNotifications.some(
        (notification) => notification.monitorId === monitor.id && notification.alertType === 'Alert',
      ),
      caution: site.openNotifications.some(
        (notification) => notification.monitorId === monitor.id && notification.alertType === 'Caution',
      ),
      siteName: site.siteName,
      fleetNumber: monitor.fleetNumber,
      serialId: monitor.serialId ?? '',
      lastDataTime: monitor.lastDataTime,
      what3words: monitor.what3words ?? '',
    }));
}

// Function summary: Handles the format time range workflow for this module.
function formatTimeRange(start?: string | null, end?: string | null) {
  return start && end ? `${start} - ${end}` : 'Not set';
}

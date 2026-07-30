// File summary: Shares the small presentational pieces every operations panel had its own copy of.
// Major updates:
// - 2026-07-30 pending Collapsed the DetailItem, LoadingInline and NotificationList duplicates from the M9 splits.

import { RefreshCcw } from 'lucide-react';
import { formatDateTime, formatNumber } from '../format';
import type { DashboardNotificationItem } from '../dtos';

// Function summary: Renders one labelled read-only value inside a detail grid.
export function DetailItem({ label, value }: Readonly<{ label: string; value?: string | null }>) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value || 'None'}</strong>
    </div>
  );
}

// Function summary: Renders an inline route-loading indicator without pulling in a heavier panel module.
export function LoadingInline({ label }: Readonly<{ label: string }>) {
  return (
    <div className="loading-inline">
      <RefreshCcw size={16} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}

// Function summary: Renders the open-notification stack shared by the dashboard and map/calendar panels.
export function NotificationList({
  notifications,
}: Readonly<{ notifications: ReadonlyArray<DashboardNotificationItem> }>) {
  if (notifications.length === 0) {
    return <p className="muted-text">No open notifications in this view.</p>;
  }

  return (
    <div className="notification-stack">
      {notifications.map((notification) => (
        <div className="notification-card" key={notification.id}>
          <span className={`status-chip ${notificationTone(notification)}`}>{notification.alertType}</span>
          <strong>{notification.fleetNumber || notification.serialId}</strong>
          <span>
            {notification.alertField} / {formatNumber(notification.level)}
          </span>
          <time>{formatDateTime(notification.notificationTime)}</time>
        </div>
      ))}
    </div>
  );
}

// Function summary: Maps a notification onto its status-chip tone.
function notificationTone(notification: DashboardNotificationItem) {
  return notification.alertType === 'Alert' ? 'danger' : 'neutral';
}

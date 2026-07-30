// File summary: Defines the shared alert-level grid columns used by the alert-level and notification panels.
// Major updates:
// - 2026-07-30 pending Extracted from NotificationAlertPanels.tsx during the notifications/alert-levels split.

import type { DataGridColumn } from '../components/DataGrid';
import { formatNumber } from '../format';
import type { AlertLevelItem } from '../dtos';

export const alertLevelColumns: DataGridColumn<AlertLevelItem>[] = [
  { key: 'alertField', header: 'Parameter', sortable: true, render: (level) => level.alertField },
  { key: 'alertType', header: 'Type', sortable: true, render: (level) => level.alertType },
  { key: 'limitOn', header: 'On', sortable: true, align: 'end', render: (level) => formatNumber(level.limitOn) },
  { key: 'limitOff', header: 'Off', sortable: true, align: 'end', render: (level) => formatNumber(level.limitOff) },
  {
    key: 'averagingPeriod',
    header: 'Average',
    sortable: true,
    render: (level) => level.averagingPeriodLabel || String(level.averagingPeriod),
  },
  { key: 'days', header: 'Days', render: (level) => formatDays(level) },
  {
    key: 'time',
    header: 'Time',
    render: (level) => (level.startTime && level.endTime ? `${level.startTime}-${level.endTime}` : 'All day'),
  },
];

// Function summary: Selects alert-level columns appropriate for the monitor type.
export function alertLevelColumnsForMonitorType(typeOfMonitor?: string | null): DataGridColumn<AlertLevelItem>[] {
  if (typeOfMonitor === 'Vibration') {
    return alertLevelColumns.filter((column) => column.key !== 'averagingPeriod');
  }

  return alertLevelColumns;
}

// Function summary: Handles the format days workflow for this module.
function formatDays(level: AlertLevelItem) {
  const days = [
    level.weekdays ? 'Weekdays' : null,
    level.saturdays ? 'Sat' : null,
    level.sundays ? 'Sun' : null,
  ].filter(Boolean);
  return days.length > 0 ? days.join(', ') : 'Site hours';
}

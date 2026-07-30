// File summary: Shares props types, paging constants, and site operating-hours helpers across the contract/site panels.
// Major updates:
// - 2026-07-30 pending Extracted from ContractSitePanels.tsx during the contracts/sites split.

import type { SiteOperatingHours } from '../dtos';

export const pageSize = 10;
export type ListExecution<TQuery> = Readonly<{ query: TQuery }>;

export type OperationsPanelCallbacks = Readonly<{
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

export type OperationsRouteProps = OperationsPanelCallbacks &
  Readonly<{
    locationPath: string;
  }>;

export const siteOperatingDays: SiteOperatingHours[] = [
  { dayOfWeek: 1, dayName: 'Monday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 2, dayName: 'Tuesday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 3, dayName: 'Wednesday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 4, dayName: 'Thursday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 5, dayName: 'Friday', startTime: '08:00', endTime: '18:00', isClosed: false },
  { dayOfWeek: 6, dayName: 'Saturday', startTime: '', endTime: '', isClosed: true },
  { dayOfWeek: 7, dayName: 'Sunday', startTime: '', endTime: '', isClosed: true },
];

type LegacySiteHours = {
  startTime?: string | null;
  endTime?: string | null;
  satStartTime?: string | null;
  satEndTime?: string | null;
  sunStartTime?: string | null;
  sunEndTime?: string | null;
} | null;

// Function summary: Normalizes API and legacy site-hour values into the seven-day editor/detail model.
export function normalizeOperatingHours(operatingHours?: SiteOperatingHours[] | null, legacy?: LegacySiteHours) {
  const byDay = new Map((operatingHours ?? []).map((hours) => [hours.dayOfWeek, hours]));
  return siteOperatingDays.map((day) => {
    const existing = byDay.get(day.dayOfWeek);
    if (existing) {
      return {
        ...day,
        ...existing,
        startTime: existing.startTime ?? '',
        endTime: existing.endTime ?? '',
      };
    }
    return legacyOperatingHours(day, legacy);
  });
}

// Function summary: Converts the older weekday/Saturday/Sunday fields into one per-day operating-hours row.
function legacyOperatingHours(day: SiteOperatingHours, legacy?: LegacySiteHours) {
  if (!legacy) {
    return { ...day };
  }
  if (day.dayOfWeek === 6) {
    return { ...day, startTime: legacy.satStartTime ?? '', endTime: legacy.satEndTime ?? '' };
  }
  if (day.dayOfWeek === 7) {
    return { ...day, startTime: legacy.sunStartTime ?? '', endTime: legacy.sunEndTime ?? '' };
  }
  return { ...day, startTime: legacy.startTime ?? '', endTime: legacy.endTime ?? '' };
}

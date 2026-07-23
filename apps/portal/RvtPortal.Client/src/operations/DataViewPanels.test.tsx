import { describe, expect, it } from 'vitest';
import { formatDateTime } from './DataViewPanels';

describe('DataViewPanels UTC timestamp presentation', () => {
  const activeTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const expectedByTimeZone: Record<string, string> = {
    'Europe/London': '1 Jul 2026, 15:30',
    UTC: '1 Jul 2026, 14:30'
  };
  const expected = expectedByTimeZone[activeTimeZone];

  it.skipIf(!expected)('interprets the API UTC instant before local DST presentation', () => {
    expect(formatDateTime('2026-07-01T14:30:00Z')).toBe(expected);
  });
});

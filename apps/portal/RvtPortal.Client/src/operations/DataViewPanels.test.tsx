import { describe, expect, it } from 'vitest';
import { formatDateTime, fromDateToApi } from './DataViewPanels';

describe('DataViewPanels UTC timestamp presentation', () => {
  it('renders one UTC instant in explicit UTC and Europe/London zones', () => {
    expect(formatDateTime('2026-07-01T14:30:00Z', 'UTC')).toBe('1 Jul 2026, 14:30');
    expect(formatDateTime('2026-07-01T14:30:00Z', 'Europe/London')).toBe('1 Jul 2026, 15:30');
  });

  it('converts a datetime-local wall time to a UTC API instant', () => {
    const value = '2026-07-01T14:30';
    expect(fromDateToApi(value)).toBe(new Date(value).toISOString());
  });
});

// File summary: Pins the Portal's single en-GB locale policy for dates and numbers.

import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime, formatMonthYear, formatNumber } from './format';

describe('format', () => {
  it('formats dates in en-GB regardless of the browser locale', () => {
    expect(formatDate('2026-07-29T12:00:00Z')).toBe('29 Jul 2026');
  });

  it('formats timestamps in en-GB with an explicit time zone', () => {
    expect(formatDateTime('2026-07-29T12:34:00Z', 'UTC')).toBe('29 Jul 2026, 12:34');
  });

  it('renders empty input as an empty string', () => {
    expect(formatDate(null)).toBe('');
    expect(formatDate(undefined)).toBe('');
    expect(formatDateTime('')).toBe('');
  });

  it('returns malformed input verbatim instead of throwing during render', () => {
    expect(formatDate('not-a-date')).toBe('not-a-date');
    expect(formatDate('2026-13-45T99:99:99Z')).toBe('2026-13-45T99:99:99Z');
    expect(formatDateTime('garbage', 'UTC')).toBe('garbage');
  });

  it('formats calendar months', () => {
    expect(formatMonthYear(2026, 7)).toBe('July 2026');
  });

  it('formats numbers with en-GB grouping and bounded fractions', () => {
    expect(formatNumber(1234.5678)).toBe('1,234.57');
    expect(formatNumber(1234.5678, 4)).toBe('1,234.5678');
    expect(formatNumber(null)).toBe('');
    expect(formatNumber(undefined)).toBe('');
  });
});

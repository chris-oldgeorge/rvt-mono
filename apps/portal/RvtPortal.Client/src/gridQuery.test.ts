// File summary: Pins the shared grid query-string semantics that seven screens used to reimplement.

import { describe, expect, it } from 'vitest';
import { normalizeSortDirection, parsePositiveInt } from './gridQuery';

describe('parsePositiveInt', () => {
  it('parses whole positive integers', () => {
    expect(parsePositiveInt('3', 1)).toBe(3);
  });

  it.each([null, '', '0', '-2', '2.5', '2abc', 'abc'])(
    'falls back for %j (strict semantics: parseInt-style prefixes are rejected)',
    (value) => {
      expect(parsePositiveInt(value, 7)).toBe(7);
    },
  );
});

describe('normalizeSortDirection', () => {
  it.each(['Descending', 'descending', 'DESC', 'desc'])('honors %s on every screen', (value) => {
    expect(normalizeSortDirection(value)).toBe('Descending');
  });

  it.each(['Ascending', 'ascending', 'ASC', 'asc'])('honors %s', (value) => {
    expect(normalizeSortDirection(value, 'Descending')).toBe('Ascending');
  });

  it('falls back to Ascending by default', () => {
    expect(normalizeSortDirection(null)).toBe('Ascending');
    expect(normalizeSortDirection('sideways')).toBe('Ascending');
  });

  it('respects a screen-specific default', () => {
    expect(normalizeSortDirection(null, 'Descending')).toBe('Descending');
    expect(normalizeSortDirection('sideways', 'Descending')).toBe('Descending');
  });
});

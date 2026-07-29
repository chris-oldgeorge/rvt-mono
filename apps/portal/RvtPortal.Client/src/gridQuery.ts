// File summary: Shared grid query-string parsing and sort-state handling for list screens.
// Seven screens carried their own parsePositiveInt (in two semantics: Number()
// rejected '2abc' while parseInt accepted it) and six carried their own
// normalizeSortDirection (in three semantics: '?sortDir=descending' was honored
// on some screens and silently ignored on others). This module is the one
// place, with the strict integer semantics and lenient, case-insensitive sort
// directions everywhere.

import { useCallback } from 'react';
import type { GridSortDirection } from './components/DataGrid';
import type { SortDirection } from './dtos';

// Function summary: Parses a whole positive integer, treating anything else ('2abc', '0', '-1') as the fallback.
export function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

// Function summary: Normalizes a sort-direction query value, accepting Ascending/Descending and asc/desc in any case.
export function normalizeSortDirection(value: string | null, fallback: SortDirection = 'Ascending'): SortDirection {
  const normalized = value?.trim().toLowerCase();
  if (normalized === 'descending' || normalized === 'desc') {
    return 'Descending';
  }
  if (normalized === 'ascending' || normalized === 'asc') {
    return 'Ascending';
  }
  return fallback;
}

// Function summary: Returns the standard sort-change handler that updates key and direction and resets to page one.
export function useGridSortHandler(
  setSortKey: (key: string) => void,
  setSortDir: (direction: SortDirection) => void,
  setPage: (page: number) => void,
) {
  return useCallback(
    (key: string, direction: GridSortDirection) => {
      setSortKey(key);
      setSortDir(direction);
      setPage(1);
    },
    [setPage, setSortDir, setSortKey],
  );
}

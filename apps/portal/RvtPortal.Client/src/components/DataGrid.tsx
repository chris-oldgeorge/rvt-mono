// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { ChevronLeft, ChevronRight, ChevronsUpDown } from 'lucide-react';
import type { ReactNode } from 'react';

export type GridSortDirection = 'Ascending' | 'Descending';

export type DataGridColumn<T> = {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  sortable?: boolean;
  align?: 'start' | 'end';
};

export type DataGridRowAction<T> = {
  label: string;
  icon?: ReactNode;
  onClick: (row: T) => void;
  disabled?: (row: T) => boolean;
};

type DataGridProps<T> = Readonly<{
  columns: ReadonlyArray<DataGridColumn<T>>;
  rows: ReadonlyArray<T>;
  getRowKey: (row: T) => string;
  emptyMessage: string;
  error?: string | null;
  isLoading?: boolean;
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  sortKey?: string;
  sortDirection?: GridSortDirection;
  rowActions?: ReadonlyArray<DataGridRowAction<T>>;
  onPageChange?: (page: number) => void;
  onSortChange?: (key: string, direction: GridSortDirection) => void;
}>;

export function DataGrid<T>({
  columns,
  rows,
  getRowKey,
  emptyMessage,
  error,
  isLoading = false,
  page,
  pageSize,
  total,
  totalPages,
  sortKey,
  sortDirection = 'Ascending',
  rowActions = [],
  onPageChange,
  onSortChange
}: DataGridProps<T>) {
  const hasActions = rowActions.length > 0;
  const visibleTotalPages = Math.max(totalPages, total > 0 ? Math.ceil(total / pageSize) : 0);
  const canGoBack = page > 1 && !isLoading;
  const canGoForward = visibleTotalPages > 0 && page < visibleTotalPages && !isLoading;

  // Function summary: Handles the next sort direction workflow for this module.
  function nextSortDirection(columnKey: string): GridSortDirection {
    return sortKey === columnKey && sortDirection === 'Ascending' ? 'Descending' : 'Ascending';
  }

  // Function summary: Handles the aria sort value workflow for this module.
  function ariaSortValue(columnKey: string) {
    if (sortKey !== columnKey) {
      return undefined;
    }
    return sortDirection === 'Ascending' ? 'ascending' : 'descending';
  }

  return (
    <div className="data-grid" aria-busy={isLoading}>
      <div className="table-shell">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  className={column.align === 'end' ? 'align-end' : undefined}
                  key={column.key}
                  scope="col"
                  aria-sort={column.sortable ? ariaSortValue(column.key) : undefined}
                >
                  {column.sortable && onSortChange ? (
                    <button
                      className="column-sort"
                      type="button"
                      onClick={() => onSortChange(column.key, nextSortDirection(column.key))}
                    >
                      <span>{column.header}</span>
                      <ChevronsUpDown size={15} aria-hidden="true" />
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              ))}
              {hasActions && <th scope="col">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={columns.length + (hasActions ? 1 : 0)}>Loading data...</td>
              </tr>
            )}
            {!isLoading && error && (
              <tr>
                <td colSpan={columns.length + (hasActions ? 1 : 0)}>{error}</td>
              </tr>
            )}
            {!isLoading && !error && rows.length === 0 && (
              <tr>
                <td colSpan={columns.length + (hasActions ? 1 : 0)}>{emptyMessage}</td>
              </tr>
            )}
            {!isLoading && !error && rows.map((row) => (
              <tr key={getRowKey(row)}>
                {columns.map((column) => (
                  <td className={column.align === 'end' ? 'align-end' : undefined} key={column.key}>
                    {column.render(row)}
                  </td>
                ))}
                {hasActions && (
                  <td>
                    <div className="row-actions">
                      {rowActions.map((action) => (
                        <button
                          className="icon-button"
                          type="button"
                          key={action.label}
                          onClick={() => action.onClick(row)}
                          disabled={action.disabled?.(row) ?? false}
                          title={action.label}
                          aria-label={action.label}
                        >
                          {action.icon}
                          <span className="sr-only">{action.label}</span>
                        </button>
                      ))}
                    </div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="data-grid-footer">
        <span>
          Page {visibleTotalPages === 0 ? 0 : page} of {visibleTotalPages} | {total} records
        </span>
        <div className="pager">
          <button type="button" onClick={() => onPageChange?.(page - 1)} disabled={!canGoBack} aria-label="Previous page">
            <ChevronLeft size={18} aria-hidden="true" />
          </button>
          <button type="button" onClick={() => onPageChange?.(page + 1)} disabled={!canGoForward} aria-label="Next page">
            <ChevronRight size={18} aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  );
}

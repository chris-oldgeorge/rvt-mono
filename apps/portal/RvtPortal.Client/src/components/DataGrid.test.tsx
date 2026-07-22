// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DataGrid } from './DataGrid';

type Row = {
  id: string;
  name: string;
  users: number;
};

const columns = [
  { key: 'name', header: 'Name', sortable: true, render: (row: Row) => row.name },
  { key: 'users', header: 'Users', sortable: true, align: 'end' as const, render: (row: Row) => row.users }
];

describe('DataGrid', () => {
  it('renders loading, empty, error, and data states', () => {
    const baseProps = {
      columns,
      getRowKey: (row: Row) => row.id,
      emptyMessage: 'No rows',
      page: 1,
      pageSize: 10,
      total: 0,
      totalPages: 0
    };

    const { rerender } = render(<DataGrid {...baseProps} rows={[]} isLoading />);
    expect(screen.getByText(/loading data/i)).toBeInTheDocument();

    rerender(<DataGrid {...baseProps} rows={[]} />);
    expect(screen.getByText('No rows')).toBeInTheDocument();

    rerender(<DataGrid {...baseProps} rows={[]} error="Could not load rows" />);
    expect(screen.getByText('Could not load rows')).toBeInTheDocument();

    rerender(<DataGrid {...baseProps} rows={[{ id: '1', name: 'RVT Group', users: 3 }]} total={1} totalPages={1} />);
    expect(screen.getByText('RVT Group')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 1 | 1 records')).toBeInTheDocument();
  });

  it('raises sort, paging, and row action events', async () => {
    const user = userEvent.setup();
    const onSortChange = vi.fn();
    const onPageChange = vi.fn();
    const onInspect = vi.fn();

    render(
      <DataGrid
        columns={columns}
        rows={[{ id: '1', name: 'RVT Group', users: 3 }]}
        getRowKey={(row) => row.id}
        emptyMessage="No rows"
        page={1}
        pageSize={1}
        total={2}
        totalPages={2}
        sortKey="name"
        sortDirection="Ascending"
        onSortChange={onSortChange}
        onPageChange={onPageChange}
        rowActions={[{ label: 'Inspect row', onClick: onInspect }]}
      />
    );

    await user.click(screen.getByRole('button', { name: /name/i }));
    await user.click(screen.getByRole('button', { name: /next page/i }));
    await user.click(screen.getByRole('button', { name: /inspect row/i }));

    expect(onSortChange).toHaveBeenCalledWith('name', 'Descending');
    expect(onPageChange).toHaveBeenCalledWith(2);
    expect(onInspect).toHaveBeenCalledWith({ id: '1', name: 'RVT Group', users: 3 });
  });
});

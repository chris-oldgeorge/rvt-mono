// File summary: Covers data-view timestamp presentation and the URL date handling behind the grid request.
// Major updates:
// - 2026-07-30 pending Exercised date conversion through the panel instead of a test-only export.

import { render, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { formatDateTime } from '../format';
import { DataViewsPanel } from './DataViewPanels';

const api = vi.hoisted(() => ({
  getDashboardSummary: vi.fn(),
  getMonitorGraph: vi.fn(),
  getMonitorTrace: vi.fn(),
  queryMonitorDataGrid: vi.fn(),
  queryMonitorTraces: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  isAbortError: () => false,
}));

// Function summary: Builds the minimal grid response the data view needs to settle.
function gridResponse() {
  return {
    deploymentId: 'deployment-a',
    monitorId: 'monitor-a',
    monitorName: 'Grid monitor',
    monitorType: 'Dust',
    minDate: '2026-07-01T09:00:00Z',
    maxDate: '2026-07-01T15:00:00Z',
    fromDate: '2026-07-01T09:00:00Z',
    toDate: '2026-07-01T15:00:00Z',
    fromDateChanged: false,
    toDateChanged: false,
    maxDuration: null,
    filterOption: '',
    filterOptions: [],
    columns: [],
    rows: [],
    total: 0,
    page: 1,
    pageSize: 10,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
    sort: 'sampleTime',
    sortDir: 'Descending',
  };
}

// Function summary: Reads the date range the panel put on its most recent grid request.
function lastRequestedRange() {
  const calls = api.queryMonitorDataGrid.mock.calls;
  return calls[calls.length - 1][1];
}

describe('DataViewPanels UTC timestamp presentation', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.queryMonitorDataGrid.mockResolvedValue(gridResponse());
    api.getDashboardSummary.mockResolvedValue({
      role: 'RVTAdmin',
      monitorCounts: { new: 0, notUsed: 0, online: 0, offline: 0, assigned: 0 },
      openAlerts: 0,
      openCautions: 0,
      recentNotifications: [],
      sites: [],
      calendarDeployments: [{ value: 'deployment-a', label: 'Deployment A' }],
    });
  });

  it('renders one UTC instant in explicit UTC and Europe/London zones', () => {
    expect(formatDateTime('2026-07-01T14:30:00Z', 'UTC')).toBe('1 Jul 2026, 14:30');
    expect(formatDateTime('2026-07-01T14:30:00Z', 'Europe/London')).toBe('1 Jul 2026, 15:30');
  });

  it('converts the datetime-local wall time in the URL to a UTC API instant', async () => {
    render(
      <DataViewsPanel
        locationPath="/data?deploymentId=deployment-a&fromDate=2026-07-01T14:30"
        onRequestError={vi.fn()}
      />,
    );

    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalled());
    expect(lastRequestedRange()).toMatchObject({ fromDate: new Date('2026-07-01T14:30').toISOString() });
  });

  it('drops malformed and absent URL dates instead of throwing', async () => {
    const { unmount } = render(
      <DataViewsPanel
        locationPath="/data?deploymentId=deployment-a&fromDate=not-a-date&toDate=2026-13-45T99:99"
        onRequestError={vi.fn()}
      />,
    );

    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalled());
    expect(lastRequestedRange()).toMatchObject({ fromDate: null, toDate: null });
    unmount();

    render(<DataViewsPanel locationPath="/data?deploymentId=deployment-a" onRequestError={vi.fn()} />);

    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalledTimes(2));
    expect(lastRequestedRange()).toMatchObject({ fromDate: null, toDate: null });
  });
});

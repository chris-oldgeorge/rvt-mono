import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DashboardPanel } from './DashboardPanels';
import { CalendarPanel, MapPanel } from './DashboardRoutePanels';
import { DataViewsPanel } from './DataViewPanels';

const api = vi.hoisted(() => ({
  getCalendarDay: vi.fn(),
  getCalendarMonth: vi.fn(),
  getDashboardSummary: vi.fn(),
  getMonitorTrace: vi.fn(),
  queryMapMarkers: vi.fn(),
  queryMonitorDataGrid: vi.fn(),
  queryMonitorTraces: vi.fn(),
  querySites: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  isAbortError: () => false,
}));

const date = '2026-05-24';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((nextResolve, nextReject) => {
    resolve = nextResolve;
    reject = nextReject;
  });
  return { promise, reject, resolve };
}

function dashboardSummary() {
  return {
    role: 'RVTAdmin',
    monitorCounts: { new: 0, notUsed: 0, online: 1, offline: 0, assigned: 1 },
    openAlerts: 0,
    openCautions: 0,
    recentNotifications: [],
    sites: [
      { value: 'site-a', label: 'Site A' },
      { value: 'site-b', label: 'Site B' },
    ],
    calendarDeployments: [
      { value: 'deployment-a', label: 'Deployment A' },
      { value: 'deployment-b', label: 'Deployment B' },
    ],
  };
}

function mapResponse(label: string) {
  return {
    siteId: label.toLowerCase().replace(' ', '-'),
    siteName: label,
    isScopedToCurrentUser: false,
    markers: [
      {
        monitorId: `${label}-monitor`,
        deploymentId: `${label}-deployment`,
        latitude: 51.5,
        longitude: -0.1,
        typeOfMonitor: 'Dust',
        offline: false,
        alert: false,
        caution: false,
        siteName: label,
        fleetNumber: label,
        serialId: `${label}-serial`,
        lastDataTime: '2026-05-24T10:00:00Z',
        what3words: null,
      },
    ],
  };
}

function calendarMonth(deploymentId: string, monitorId: string) {
  return {
    monitorId,
    deploymentId,
    fleetNumber: deploymentId,
    serialId: `${deploymentId}-serial`,
    typeOfMonitor: 'Dust',
    year: 2026,
    month: 5,
    startDate: '2026-05-01T00:00:00Z',
    endDate: '2026-05-31T00:00:00Z',
    unit: 'ug/m3',
    deployments: dashboardSummary().calendarDeployments,
    days: [{ date, isCurrentMonth: true, status: 'Alert', average: 1, notificationCount: 1 }],
  };
}

function calendarDay(monitorId: string, fleetNumber: string) {
  return {
    monitorId,
    displayDay: '2026-05-24T00:00:00Z',
    fleetNumber,
    serialId: `${fleetNumber}-serial`,
    typeOfMonitor: 'Dust',
    unit: 'ug/m3',
    values: [{ label: 'pm10', value: 1 }],
    alertLevels: [],
    notifications: [],
  };
}

function siteResponse(name: string) {
  return {
    results: [
      {
        id: name.toLowerCase(),
        siteName: name,
        siteAddress: '1 Test Street',
        companyName: 'RVT',
        contracts: 'None',
        archived: false,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 50,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}

function gridResponse(
  name: string,
  filterOption = '',
  filterOptions: Array<{ value: string; label: string }> = [],
) {
  return {
    deploymentId: 'deployment-a',
    monitorId: 'monitor-a',
    monitorName: name,
    monitorType: 'Dust',
    minDate: '2026-05-24T09:00:00Z',
    maxDate: '2026-05-24T10:00:00Z',
    fromDate: '2026-05-24T09:00:00Z',
    toDate: '2026-05-24T10:00:00Z',
    fromDateChanged: false,
    toDateChanged: false,
    maxDuration: null,
    filterOption,
    filterOptions,
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

function tracesResponse(deploymentId = 'deployment-a', traceIds = ['trace-a', 'trace-b']) {
  return {
    deploymentId,
    monitorId: `${deploymentId}-monitor`,
    monitorName: 'Trace monitor',
    monitorType: 'Vibration',
    traces: traceIds.map((id, index) => ({
      id,
      startTime: `2026-05-24T${10 + index}:00:00Z`,
      endTime: `2026-05-24T${10 + index}:01:00Z`,
      durationSeconds: 60,
    })),
  };
}

function traceDetail(traceId: string, monitorName: string, deploymentId = 'deployment-a') {
  return {
    deploymentId,
    monitorId: `${deploymentId}-monitor`,
    traceId,
    monitorName,
    fromDate: '2026-05-24T10:00:00Z',
    toDate: '2026-05-24T10:01:00Z',
    samples: [],
  };
}

describe('Portal dashboard request ownership', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.getDashboardSummary.mockResolvedValue(dashboardSummary());
  });

  it('hides a previous calendar day when a same-date deployment change has no completed day response', async () => {
    const deploymentAMonth = deferred<ReturnType<typeof calendarMonth>>();
    const deploymentBMonth = deferred<ReturnType<typeof calendarMonth>>();
    const deploymentADay = deferred<ReturnType<typeof calendarDay>>();
    const deploymentBDay = deferred<ReturnType<typeof calendarDay>>();
    api.getCalendarMonth.mockReturnValueOnce(deploymentAMonth.promise).mockReturnValueOnce(deploymentBMonth.promise);
    api.getCalendarDay.mockReturnValueOnce(deploymentADay.promise).mockReturnValueOnce(deploymentBDay.promise);

    render(
      <CalendarPanel locationPath="/calendar?deploymentId=deployment-a&year=2026&month=5" onRequestError={vi.fn()} />,
    );
    await act(async () => deploymentAMonth.resolve(calendarMonth('deployment-a', 'monitor-a')));
    await act(async () => deploymentADay.resolve(calendarDay('monitor-a', 'Deployment A')));
    expect(await screen.findByText('Deployment A / Dust')).toBeInTheDocument();

    await act(async () => fireEvent.change(screen.getByLabelText('Deployment'), { target: { value: 'deployment-b' } }));

    expect(screen.queryByText('Deployment A / Dust')).not.toBeInTheDocument();
    await act(async () => deploymentBMonth.resolve(calendarMonth('deployment-b', 'monitor-b')));
    await act(async () => deploymentBDay.resolve(calendarDay('monitor-b', 'Deployment B')));
    expect(await screen.findByText('Deployment B / Dust')).toBeInTheDocument();
  });

  it('keeps the newest map response when an older site response resolves after it', async () => {
    const initialMarkers = deferred<ReturnType<typeof mapResponse>>();
    const siteAMarkers = deferred<ReturnType<typeof mapResponse>>();
    const siteBMarkers = deferred<ReturnType<typeof mapResponse>>();
    api.queryMapMarkers
      .mockReturnValueOnce(initialMarkers.promise)
      .mockReturnValueOnce(siteAMarkers.promise)
      .mockReturnValueOnce(siteBMarkers.promise);

    render(<MapPanel locationPath="/maps" onRequestError={vi.fn()} />);
    await act(async () => initialMarkers.resolve(mapResponse('All Sites')));
    await screen.findByRole('option', { name: 'Site A' });
    fireEvent.change(screen.getByLabelText('Site'), { target: { value: 'site-a' } });
    fireEvent.change(screen.getByLabelText('Site'), { target: { value: 'site-b' } });
    await act(async () => siteBMarkers.resolve(mapResponse('Site B')));
    const markerList = screen.getByLabelText('Map marker list');
    expect(await within(markerList).findByText('Site B (Dust)')).toBeInTheDocument();
    await act(async () => siteAMarkers.resolve(mapResponse('Site A')));

    expect(within(markerList).getByText('Site B (Dust)')).toBeInTheDocument();
    expect(within(markerList).queryByText('Site A (Dust)')).not.toBeInTheDocument();
  });

  it('keeps map site options interactive while an intermediate marker request is pending', async () => {
    const initialMarkers = deferred<ReturnType<typeof mapResponse>>();
    const siteAMarkers = deferred<ReturnType<typeof mapResponse>>();
    const siteBMarkers = deferred<ReturnType<typeof mapResponse>>();
    api.queryMapMarkers
      .mockReturnValueOnce(initialMarkers.promise)
      .mockReturnValueOnce(siteAMarkers.promise)
      .mockReturnValueOnce(siteBMarkers.promise);

    render(<MapPanel locationPath="/maps" onRequestError={vi.fn()} />);
    await act(async () => initialMarkers.resolve(mapResponse('All Sites')));
    const siteSelector = await screen.findByLabelText('Site');
    fireEvent.change(siteSelector, { target: { value: 'site-a' } });
    await waitFor(() => expect(api.queryMapMarkers).toHaveBeenCalledTimes(2));

    expect(screen.getByRole('option', { name: 'Site B' })).toBeInTheDocument();
    fireEvent.change(siteSelector, { target: { value: 'site-b' } });
    await waitFor(() => expect(api.queryMapMarkers).toHaveBeenCalledTimes(3));
    await act(async () => siteBMarkers.resolve(mapResponse('Site B')));

    expect(within(screen.getByLabelText('Map marker list')).getByText('Site B (Dust)')).toBeInTheDocument();
    expect(screen.queryByText('All Sites (Dust)')).not.toBeInTheDocument();
  });

  it('keeps the newest live site search result when an older query resolves last', async () => {
    const initialSearch = deferred<ReturnType<typeof siteResponse>>();
    const alphaSearch = deferred<ReturnType<typeof siteResponse>>();
    const betaSearch = deferred<ReturnType<typeof siteResponse>>();
    api.querySites
      .mockReturnValueOnce(initialSearch.promise)
      .mockReturnValueOnce(alphaSearch.promise)
      .mockReturnValueOnce(betaSearch.promise);

    render(
      <DashboardPanel
        auth={{
          isAuthenticated: true,
          user: { id: 'admin', email: 'admin@rvt.test', name: 'Admin', roles: ['RVTAdmin'] },
        }}
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );
    await act(async () => initialSearch.resolve(siteResponse('Initial Site')));
    const search = await screen.findByPlaceholderText('Search sites');
    fireEvent.change(search, { target: { value: 'alpha' } });
    fireEvent.change(search, { target: { value: 'beta' } });
    await act(async () => betaSearch.resolve(siteResponse('Beta Site')));
    expect(await screen.findByText('Beta Site')).toBeInTheDocument();
    await act(async () => alphaSearch.resolve(siteResponse('Alpha Site')));

    expect(screen.getByText('Beta Site')).toBeInTheDocument();
    expect(screen.queryByText('Alpha Site')).not.toBeInTheDocument();
  });

  it('keeps the newest grid response when an older filter request resolves after it', async () => {
    const firstGrid = deferred<ReturnType<typeof gridResponse>>();
    const secondGrid = deferred<ReturnType<typeof gridResponse>>();
    api.queryMonitorDataGrid.mockReturnValueOnce(firstGrid.promise).mockReturnValueOnce(secondGrid.promise);

    render(<DataViewsPanel locationPath="/data?deploymentId=deployment-a" onRequestError={vi.fn()} />);
    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-05-24T09:00' } });
    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalledTimes(2));
    await act(async () => secondGrid.resolve(gridResponse('Newest grid')));
    expect(await screen.findByText('Newest grid')).toBeInTheDocument();
    await act(async () => firstGrid.resolve(gridResponse('Old grid')));

    expect(screen.getByText('Newest grid')).toBeInTheDocument();
    expect(screen.queryByText('Old grid')).not.toBeInTheDocument();
  });

  it('keeps averaging controls interactive while an intermediate grid request is pending', async () => {
    const rawGrid = deferred<ReturnType<typeof gridResponse>>();
    const hourlyGrid = deferred<ReturnType<typeof gridResponse>>();
    const options = [
      { value: 'raw', label: 'Raw' },
      { value: 'hourly', label: 'Hourly' },
    ];
    api.queryMonitorDataGrid
      .mockResolvedValueOnce(gridResponse('Initial grid', '', options))
      .mockReturnValueOnce(rawGrid.promise)
      .mockReturnValueOnce(hourlyGrid.promise);

    render(<DataViewsPanel locationPath="/data?deploymentId=deployment-a" onRequestError={vi.fn()} />);
    expect(await screen.findByText('Initial grid')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Raw' }));
    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalledTimes(2));

    expect(screen.queryByText('Initial grid')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Hourly' }));
    await waitFor(() => expect(api.queryMonitorDataGrid).toHaveBeenCalledTimes(3));
    await act(async () => hourlyGrid.resolve(gridResponse('Hourly grid', 'hourly', options)));

    expect(await screen.findByText('Hourly grid')).toBeInTheDocument();
  });

  it('keeps trace detail owned by the latest selected trace', async () => {
    const firstTrace = deferred<ReturnType<typeof traceDetail>>();
    const secondTrace = deferred<ReturnType<typeof traceDetail>>();
    api.queryMonitorTraces.mockResolvedValue(tracesResponse());
    api.getMonitorTrace.mockReturnValueOnce(firstTrace.promise).mockReturnValueOnce(secondTrace.promise);

    render(<DataViewsPanel locationPath="/data?deploymentId=deployment-a&view=traces" onRequestError={vi.fn()} />);
    const traceButtons = await screen.findAllByRole('button', { name: /24 may 2026/i });
    await waitFor(() => expect(api.getMonitorTrace).toHaveBeenCalledTimes(1));
    fireEvent.click(traceButtons[1]);
    await waitFor(() => expect(api.getMonitorTrace).toHaveBeenCalledTimes(2));
    await act(async () => secondTrace.resolve(traceDetail('trace-b', 'Trace B detail')));
    expect(await screen.findByText('Trace B detail')).toBeInTheDocument();
    await act(async () => firstTrace.resolve(traceDetail('trace-a', 'Trace A detail')));

    expect(screen.getByText('Trace B detail')).toBeInTheDocument();
    expect(screen.queryByText('Trace A detail')).not.toBeInTheDocument();
  });

  it('does not reuse same-id trace detail across deployments', async () => {
    const deploymentBTraces = deferred<ReturnType<typeof tracesResponse>>();
    const deploymentBDetail = deferred<ReturnType<typeof traceDetail>>();
    api.queryMonitorTraces
      .mockResolvedValueOnce(tracesResponse('deployment-a', ['shared-trace']))
      .mockReturnValueOnce(deploymentBTraces.promise);
    api.getMonitorTrace
      .mockResolvedValueOnce(traceDetail('shared-trace', 'Deployment A detail', 'deployment-a'))
      .mockReturnValueOnce(deploymentBDetail.promise);

    render(<DataViewsPanel locationPath="/data?deploymentId=deployment-a&view=traces" onRequestError={vi.fn()} />);
    expect(await screen.findByText('Deployment A detail')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Deployment'), { target: { value: 'deployment-b' } });
    await act(async () => deploymentBTraces.resolve(tracesResponse('deployment-b', ['shared-trace'])));
    await waitFor(() => expect(api.getMonitorTrace).toHaveBeenCalledTimes(2));

    expect(screen.queryByText('Deployment A detail')).not.toBeInTheDocument();
    await act(async () =>
      deploymentBDetail.resolve(traceDetail('shared-trace', 'Deployment B detail', 'deployment-b')),
    );
    expect(await screen.findByText('Deployment B detail')).toBeInTheDocument();
  });
});

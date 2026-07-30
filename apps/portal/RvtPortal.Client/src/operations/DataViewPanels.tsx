// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for data-view grid, graph, and trace requests.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { BarChart3, Download, FileDown, ListFilter, RefreshCcw, Route, Search, Table2 } from 'lucide-react';
import {
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
  type ChartData,
  type ChartOptions,
} from 'chart.js';
import { Line } from 'react-chartjs-2';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  downloadMonitorDataCsv,
  downloadMonitorTraceCsv,
  getDashboardSummary,
  getMonitorGraph,
  getMonitorTrace,
  isAbortError,
  queryMonitorDataGrid,
  queryMonitorTraces,
  type DownloadedFile,
} from '../api/client';
import { Notice } from '../components/FormControls';
import { formatDateTime, formatNumber } from '../format';
import { normalizeSortDirection, parsePositiveInt } from '../gridQuery';
import type {
  DashboardSummaryResponse,
  MonitorDataGridRequest,
  MonitorDataGridResponse,
  MonitorDataRow,
  MonitorGraphDataset,
  MonitorGraphResponse,
  OptionItem,
  SortDirection,
  TraceDetailResponse,
  TraceListResponse,
} from '../dtos';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Legend);

const pageSize = 10;
const defaultSort = 'sampleTime';
const defaultSortDir: SortDirection = 'Descending';
const panelModes = ['grid', 'graph', 'traces'] as const;

type PanelMode = (typeof panelModes)[number];

type DataViewsPanelProps = Readonly<{
  locationPath: string;
  onRequestError: (error: unknown) => void;
}>;

type DataViewQuery = Readonly<{
  deploymentId: string;
  mode: PanelMode;
  filterOption: string;
  fromDate: string;
  toDate: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
}>;

type RequestExecution<T> = Readonly<{
  query: T;
}>;

type DataViewResult = Readonly<{
  execution: RequestExecution<DataViewQuery>;
  grid: MonitorDataGridResponse | null;
  graph: MonitorGraphResponse | null;
  traces: TraceListResponse | null;
  error: string | null;
}>;

type TraceDetailResult = Readonly<{
  execution: RequestExecution<Readonly<{ deploymentId: string; traceId: string }>>;
  item: TraceDetailResponse | null;
  error: string | null;
}>;

type FilterOptionsResult = Readonly<{
  deploymentId: string;
  items: OptionItem[];
}>;

// Function summary: Renders the DataViewsPanel React component and wires its local UI behavior.
export function DataViewsPanel({ locationPath, onRequestError }: DataViewsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [summary, setSummary] = useState<DashboardSummaryResponse | null>(null);
  const [deploymentId, setDeploymentId] = useState(initialParams.get('deploymentId') ?? '');
  const [mode, setMode] = useState<PanelMode>(normalizeMode(initialParams.get('view')));
  const [filterOption, setFilterOption] = useState(initialParams.get('filterOption') ?? '');
  const [fromDate, setFromDate] = useState(toDateTimeInput(initialParams.get('fromDate')));
  const [toDate, setToDate] = useState(toDateTimeInput(initialParams.get('toDate')));
  const [dataViewResult, setDataViewResult] = useState<DataViewResult | null>(null);
  const [traceDetailResult, setTraceDetailResult] = useState<TraceDetailResult | null>(null);
  const [filterOptionsResult, setFilterOptionsResult] = useState<FilterOptionsResult | null>(null);
  const [selectedTraceId, setSelectedTraceId] = useState('');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sort, setSort] = useState(initialParams.get('sort') ?? defaultSort);
  const [sortDir, setSortDir] = useState<SortDirection>(
    normalizeSortDirection(initialParams.get('sortDir'), defaultSortDir),
  );
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);
  const query = useMemo<DataViewQuery>(
    () => ({ deploymentId, mode, filterOption, fromDate, toDate, page, sort, sortDir }),
    [deploymentId, filterOption, fromDate, mode, page, sort, sortDir, toDate],
  );
  const execution = useMemo<RequestExecution<DataViewQuery>>(() => ({ query }), [query]);
  const activeResult = dataViewResult?.execution === execution ? dataViewResult : null;
  const grid = activeResult?.grid ?? null;
  const graph = activeResult?.graph ?? null;
  const traces = activeResult?.traces ?? null;
  const traceDetailExecution = useMemo<RequestExecution<Readonly<{ deploymentId: string; traceId: string }>> | null>(
    () =>
      mode === 'traces' && deploymentId && selectedTraceId
        ? { query: { deploymentId, traceId: selectedTraceId } }
        : null,
    [deploymentId, mode, selectedTraceId],
  );
  const activeTraceDetailResult =
    traceDetailExecution && traceDetailResult?.execution === traceDetailExecution ? traceDetailResult : null;
  const traceDetail = activeTraceDetailResult?.item ?? null;
  const traceDetailError = activeTraceDetailResult?.error ?? null;
  const filterOptions = filterOptionsResult?.deploymentId === deploymentId ? filterOptionsResult.items : [];
  const requestError = activeResult?.error ?? null;
  const isLoading = Boolean(deploymentId) && dataViewResult?.execution !== execution;

  const handleError = useCallback(
    (err: unknown) => {
      setError(err instanceof Error ? err.message : 'Unexpected data view error.');
      onRequestError(err);
    },
    [onRequestError],
  );

  const setFilterOptionFromResponse = useCallback((value: string) => {
    if (!value) {
      return;
    }

    setFilterOption((current) => current || value);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    getDashboardSummary({ signal: controller.signal })
      .then((response) => {
        setSummary(response);
        const firstDeployment = response.calendarDeployments[0]?.value;
        if (firstDeployment) {
          // Functional update keeps deploymentId out of the effect deps so
          // switching deployments does not refetch the summary.
          setDeploymentId((current) => current || firstDeployment);
        }
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => controller.abort();
  }, [onRequestError]);

  useEffect(() => {
    if (!execution.query.deploymentId) {
      return;
    }

    const { deploymentId, filterOption, fromDate, mode, page, sort, sortDir, toDate } = execution.query;
    globalThis.history.replaceState(
      null,
      '',
      buildDataUrl({ deploymentId, mode, filterOption, fromDate, toDate, page, sort, sortDir }),
    );
    const controller = new AbortController();
    const request = buildGridRequest({ filterOption, fromDate, toDate, page, sort, sortDir });
    if (mode === 'grid') {
      queryMonitorDataGrid(deploymentId, request, { signal: controller.signal })
        .then((response) => {
          if (!controller.signal.aborted) {
            setDataViewResult({ execution, grid: response, graph: null, traces: null, error: null });
            setFilterOptionsResult({ deploymentId, items: response.filterOptions });
            setError(null);
            setFilterOptionFromResponse(response.filterOption);
          }
        })
        .catch((err: Error) => {
          if (!isAbortError(err) && !controller.signal.aborted) {
            setDataViewResult({ execution, grid: null, graph: null, traces: null, error: err.message });
            onRequestError(err);
          }
        });
      return () => controller.abort();
    }

    if (mode === 'graph') {
      getMonitorGraph(deploymentId, graphRequest(request), { signal: controller.signal })
        .then((response) => {
          if (!controller.signal.aborted) {
            setDataViewResult({ execution, grid: null, graph: response, traces: null, error: null });
            setFilterOptionsResult({ deploymentId, items: response.filterOptions });
            setError(null);
            setFilterOptionFromResponse(response.filterOption);
          }
        })
        .catch((err: Error) => {
          if (!isAbortError(err) && !controller.signal.aborted) {
            setDataViewResult({ execution, grid: null, graph: null, traces: null, error: err.message });
            onRequestError(err);
          }
        });
      return () => controller.abort();
    }

    queryMonitorTraces(deploymentId, traceRequest(request), { signal: controller.signal })
      .then((response) => {
        if (!controller.signal.aborted) {
          setDataViewResult({ execution, grid: null, graph: null, traces: response, error: null });
          setError(null);
          const firstTrace = response.traces[0];
          setSelectedTraceId((current) => current || firstTrace?.id || '');
        }
      })
      .catch((err: Error) => {
        if (!isAbortError(err) && !controller.signal.aborted) {
          setDataViewResult({ execution, grid: null, graph: null, traces: null, error: err.message });
          onRequestError(err);
        }
      });
    return () => controller.abort();
  }, [execution, onRequestError, setFilterOptionFromResponse]);

  useEffect(() => {
    if (!traceDetailExecution) {
      return;
    }

    const controller = new AbortController();
    getMonitorTrace(traceDetailExecution.query.deploymentId, traceDetailExecution.query.traceId, {
      signal: controller.signal,
    })
      .then((item) => {
        if (!controller.signal.aborted) {
          setTraceDetailResult({ execution: traceDetailExecution, item, error: null });
        }
      })
      .catch((err: Error) => {
        if (!isAbortError(err) && !controller.signal.aborted) {
          setTraceDetailResult({
            execution: traceDetailExecution,
            item: null,
            error: err.message,
          });
          onRequestError(err);
        }
      });
    return () => controller.abort();
  }, [onRequestError, traceDetailExecution]);

  // Function summary: Handles the handle mode workflow for this module.
  function handleMode(nextMode: PanelMode) {
    setMode(nextMode);
    setPage(1);
  }

  // Function summary: Handles the handle deployment workflow for this module.
  function handleDeployment(value: string) {
    setDeploymentId(value);
    setPage(1);
    setSelectedTraceId('');
  }

  // Function summary: Handles the handle submit workflow for this module.
  function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    setPage(1);
  }

  // Function summary: Handles the handle sort workflow for this module.
  function handleSort(column: string) {
    if (sort === column) {
      setSortDir(nextSortDirection(sortDir));
    } else {
      setSort(column);
      setSortDir('Ascending');
    }
    setPage(1);
  }

  async function handleDataDownload() {
    if (!deploymentId) {
      return;
    }
    setIsDownloading(true);
    setNotice(null);
    try {
      const file = await downloadMonitorDataCsv(
        deploymentId,
        buildGridRequest({ filterOption, fromDate, toDate, page, sort, sortDir }),
      );
      triggerDownload(file);
      setNotice(`Downloaded ${file.fileName}`);
    } catch (err) {
      handleError(err);
    } finally {
      setIsDownloading(false);
    }
  }

  async function handleTraceDownload() {
    if (!deploymentId || !selectedTraceId) {
      return;
    }
    setIsDownloading(true);
    setNotice(null);
    try {
      const file = await downloadMonitorTraceCsv(deploymentId, selectedTraceId);
      triggerDownload(file);
      setNotice(`Downloaded ${file.fileName}`);
    } catch (err) {
      handleError(err);
    } finally {
      setIsDownloading(false);
    }
  }

  return (
    <section className="data-view-layout">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>Measurements and traces</p>
            <h2>Data Views</h2>
          </div>
          <Table2 size={22} aria-hidden="true" />
        </div>
        <form className="data-filter-bar" onSubmit={handleSubmit}>
          <label className="form-field compact-select">
            <span>Deployment</span>
            <select value={deploymentId} onChange={(event) => handleDeployment(event.target.value)}>
              <option value="">Select deployment</option>
              {(summary?.calendarDeployments ?? []).map((deployment) => (
                <option value={deployment.value} key={deployment.value}>
                  {deployment.label}
                </option>
              ))}
            </select>
          </label>
          <label className="form-field compact-date">
            <span>From</span>
            <input value={fromDate} type="datetime-local" onChange={(event) => setFromDate(event.target.value)} />
          </label>
          <label className="form-field compact-date">
            <span>To</span>
            <input value={toDate} type="datetime-local" onChange={(event) => setToDate(event.target.value)} />
          </label>
          <button className="primary-button compact-action" type="submit" disabled={!deploymentId}>
            <Search size={17} aria-hidden="true" />
            <span>Search</span>
          </button>
        </form>
        <div className="segmented-control" role="tablist" aria-label="Data views">
          {panelModes.map((item) => (
            <button
              className={mode === item ? 'active' : ''}
              type="button"
              role="tab"
              aria-selected={mode === item}
              key={item}
              onClick={() => handleMode(item)}
            >
              {modeLabel(item)}
            </button>
          ))}
        </div>
        {filterOptions.length > 0 && mode !== 'traces' && (
          <div className="filter-chip-row" aria-label="Averaging options">
            {filterOptions.map((option) => (
              <button
                className={filterOption === option.value ? 'active' : ''}
                type="button"
                key={option.value}
                onClick={() => {
                  setFilterOption(option.value);
                  setPage(1);
                }}
              >
                <ListFilter size={15} aria-hidden="true" />
                <span>{option.label}</span>
              </button>
            ))}
          </div>
        )}
        {(requestError ?? traceDetailError ?? error) && (
          <Notice tone="error" message={requestError ?? traceDetailError ?? error ?? ''} />
        )}
        {notice && <Notice tone="success" message={notice} />}
        {isLoading && <LoadingInline label="Loading data" />}
        {mode === 'grid' && grid && (
          <DataGridView
            grid={grid}
            isDownloading={isDownloading}
            onDownload={handleDataDownload}
            onPage={setPage}
            onSort={handleSort}
          />
        )}
        {mode === 'graph' && graph && <GraphView graph={graph} />}
        {mode === 'traces' && traces && (
          <TraceView
            traces={traces}
            detail={traceDetail}
            selectedTraceId={selectedTraceId}
            isDownloading={isDownloading}
            onSelect={setSelectedTraceId}
            onDownload={handleTraceDownload}
          />
        )}
      </section>
    </section>
  );
}

// Function summary: Renders the DataGridView React component and wires its local UI behavior.
function DataGridView({
  grid,
  isDownloading,
  onDownload,
  onPage,
  onSort,
}: Readonly<{
  grid: MonitorDataGridResponse;
  isDownloading: boolean;
  onDownload: () => void;
  onPage: (page: number) => void;
  onSort: (column: string) => void;
}>) {
  return (
    <section className="subsection">
      <div className="subsection-heading split">
        <div>
          <h3>{grid.monitorName}</h3>
          <span>
            {formatDateTime(grid.fromDate)} to {formatDateTime(grid.toDate)}
          </span>
        </div>
        <button
          className="secondary-button"
          type="button"
          onClick={onDownload}
          disabled={grid.total === 0 || isDownloading}
        >
          <Download size={17} aria-hidden="true" />
          <span>{isDownloading ? 'Downloading' : 'Download CSV'}</span>
        </button>
      </div>
      <div className="table-shell">
        <table>
          <thead>
            <tr>
              {grid.columns.map((column) => (
                <th key={column.key}>
                  <button className="table-sort-button" type="button" onClick={() => onSort(column.key)}>
                    <span>{column.label}</span>
                    {grid.sort === column.key && <strong>{sortArrow(grid.sortDir)}</strong>}
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {grid.rows.map((row) => (
              <DataRow row={row} columns={grid.columns} key={rowKey(row)} />
            ))}
            {grid.rows.length === 0 && (
              <tr>
                <td colSpan={Math.max(grid.columns.length, 1)}>There are no matching records.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <Pagination page={grid.page} totalPages={grid.totalPages} onPage={onPage} />
    </section>
  );
}

// Function summary: Renders the DataRow React component and wires its local UI behavior.
function DataRow({
  row,
  columns,
}: Readonly<{ row: MonitorDataRow; columns: ReadonlyArray<{ key: string; label: string }> }>) {
  return (
    <tr>
      {columns.map((column) => (
        <td key={column.key}>{dataCell(row, column.key)}</td>
      ))}
    </tr>
  );
}

// Function summary: Renders the GraphView React component and wires its local UI behavior.
function GraphView({ graph }: Readonly<{ graph: MonitorGraphResponse }>) {
  const chartData = useMemo(() => buildChartData(graph.datasets), [graph.datasets]);
  const chartOptions = useMemo(() => buildChartOptions(graph), [graph]);
  return (
    <section className="subsection">
      <div className="subsection-heading split">
        <div>
          <h3>{graph.graphName}</h3>
          <span>
            {graph.xAxisLabel} / {graph.yAxisLabel}
          </span>
        </div>
        <BarChart3 size={20} aria-hidden="true" />
      </div>
      <div className="chart-shell">
        {graph.datasets.length > 0 ? (
          <Line data={chartData} options={chartOptions} />
        ) : (
          <p className="muted-text">No graph data for this range.</p>
        )}
      </div>
      {graph.thresholds.length > 0 && (
        <div className="threshold-list">
          {graph.thresholds.map((threshold) => (
            <span className="status-chip neutral" key={threshold.id}>
              {threshold.field} {threshold.alertType} {formatNumber(threshold.limitOn, 4)}
            </span>
          ))}
        </div>
      )}
    </section>
  );
}

// Function summary: Renders the TraceView React component and wires its local UI behavior.
function TraceView({
  traces,
  detail,
  selectedTraceId,
  isDownloading,
  onSelect,
  onDownload,
}: Readonly<{
  traces: TraceListResponse;
  detail: TraceDetailResponse | null;
  selectedTraceId: string;
  isDownloading: boolean;
  onSelect: (traceId: string) => void;
  onDownload: () => void;
}>) {
  return (
    <section className="trace-layout">
      <div className="trace-list" aria-label="Trace list">
        {traces.traces.map((trace) => (
          <button
            className={selectedTraceId === trace.id ? 'active' : ''}
            type="button"
            key={trace.id}
            onClick={() => onSelect(trace.id)}
          >
            <Route size={17} aria-hidden="true" />
            <span>{formatDateTime(trace.startTime)}</span>
            <em>{formatDuration(trace.durationSeconds)}</em>
          </button>
        ))}
        {traces.traces.length === 0 && <p className="muted-text">No traces were recorded for this deployment.</p>}
      </div>
      <div className="trace-detail">
        <div className="subsection-heading split">
          <div>
            <h3>{detail?.monitorName ?? traces.monitorName}</h3>
            <span>
              {detail ? `${formatDateTime(detail.fromDate)} to ${formatDateTime(detail.toDate)}` : 'Select a trace'}
            </span>
          </div>
          <button className="secondary-button" type="button" onClick={onDownload} disabled={!detail || isDownloading}>
            <FileDown size={17} aria-hidden="true" />
            <span>{isDownloading ? 'Downloading' : 'Trace CSV'}</span>
          </button>
        </div>
        {detail && (
          <div className="table-shell">
            <table>
              <thead>
                <tr>
                  <th>Index</th>
                  <th>X</th>
                  <th>Y</th>
                  <th>Z</th>
                </tr>
              </thead>
              <tbody>
                {detail.samples.map((sample) => (
                  <tr key={sample.index}>
                    <td>{sample.index}</td>
                    <td>{formatNumber(sample.x, 4)}</td>
                    <td>{formatNumber(sample.y, 4)}</td>
                    <td>{formatNumber(sample.z, 4)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}

// Function summary: Renders the Pagination React component and wires its local UI behavior.
function Pagination({
  page,
  totalPages,
  onPage,
}: Readonly<{ page: number; totalPages: number; onPage: (page: number) => void }>) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="pagination-row">
      <button className="secondary-button" type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </button>
      <span>
        Page {page} of {totalPages}
      </span>
      <button className="secondary-button" type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>
        Next
      </button>
    </div>
  );
}

// Function summary: Renders the LoadingInline React component and wires its local UI behavior.
function LoadingInline({ label }: Readonly<{ label: string }>) {
  return (
    <div className="loading-inline">
      <RefreshCcw size={16} aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}

// Function summary: Builds grid request data for callers.
function buildGridRequest({
  filterOption,
  fromDate,
  toDate,
  page,
  sort,
  sortDir,
}: {
  filterOption: string;
  fromDate: string;
  toDate: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
}): MonitorDataGridRequest {
  return {
    filterOption: filterOption || null,
    fromDate: fromDateToApi(fromDate),
    toDate: fromDateToApi(toDate),
    page,
    pageSize,
    sort,
    sortDir,
  };
}

// Function summary: Handles the graph request workflow for this module.
function graphRequest(request: MonitorDataGridRequest) {
  return {
    filterOption: request.filterOption,
    fromDate: request.fromDate,
    toDate: request.toDate,
  };
}

// Function summary: Handles the trace request workflow for this module.
function traceRequest(request: MonitorDataGridRequest) {
  return {
    fromDate: request.fromDate,
    toDate: request.toDate,
  };
}

// Function summary: Builds data url data for callers.
function buildDataUrl({
  deploymentId,
  mode,
  filterOption,
  fromDate,
  toDate,
  page,
  sort,
  sortDir,
}: {
  deploymentId: string;
  mode: PanelMode;
  filterOption: string;
  fromDate: string;
  toDate: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
}) {
  const params = new URLSearchParams({ deploymentId, view: mode });
  if (filterOption) {
    params.set('filterOption', filterOption);
  }
  if (fromDate) {
    params.set('fromDate', fromDate);
  }
  if (toDate) {
    params.set('toDate', toDate);
  }
  if (mode === 'grid' && page > 1) {
    params.set('page', String(page));
  }
  if (sort !== defaultSort) {
    params.set('sort', sort);
  }
  if (sortDir !== defaultSortDir) {
    params.set('sortDir', sortDir);
  }

  return `/data?${params.toString()}`;
}

// Function summary: Builds chart data data for callers.
function buildChartData(datasets: ReadonlyArray<MonitorGraphDataset>): ChartData<'line'> {
  const labels = chartLabels(datasets);
  return {
    labels,
    datasets: datasets.map((dataset, index) => ({
      label: dataset.label,
      data: dataset.points.map((point) => point.y ?? null),
      borderColor: chartColor(index),
      backgroundColor: chartColor(index),
      tension: 0.2,
      pointRadius: 2,
    })),
  };
}

// Function summary: Builds chart options data for callers.
function buildChartOptions(graph: MonitorGraphResponse): ChartOptions<'line'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom',
      },
    },
    scales: {
      x: {
        title: {
          display: true,
          text: graph.xAxisLabel,
        },
      },
      y: {
        title: {
          display: true,
          text: graph.yAxisLabel,
        },
      },
    },
  };
}

// Function summary: Handles the chart labels workflow for this module.
function chartLabels(datasets: ReadonlyArray<MonitorGraphDataset>) {
  const first = datasets[0];
  if (!first) {
    return [];
  }

  return first.points.map((point) => {
    if (point.time) {
      return formatDateTime(point.time);
    }
    return formatNumber(point.x, 4);
  });
}

// Function summary: Handles the chart color workflow for this module.
function chartColor(index: number) {
  const colors = ['#2563eb', '#dc2626', '#16a34a', '#9333ea', '#ea580c', '#0891b2', '#4b5563', '#be123c'];
  return colors[index % colors.length];
}

// Function summary: Handles the trigger download workflow for this module.
function triggerDownload(file: DownloadedFile) {
  const url = globalThis.URL.createObjectURL(file.blob);
  const anchor = globalThis.document.createElement('a');
  anchor.href = url;
  anchor.download = file.fileName;
  globalThis.document.body.append(anchor);
  anchor.click();
  anchor.remove();
  globalThis.URL.revokeObjectURL(url);
}

// Function summary: Handles the data cell workflow for this module.
function dataCell(row: MonitorDataRow, key: string) {
  if (key === 'sampleTime') {
    return formatDateTime(row.sampleTime);
  }

  return formatNumber(row.values[key], 4);
}

// Function summary: Handles the row key workflow for this module.
function rowKey(row: MonitorDataRow) {
  return row.sampleTime ?? JSON.stringify(row.values);
}

// Function summary: Handles the mode label workflow for this module.
function modeLabel(mode: PanelMode) {
  if (mode === 'grid') {
    return 'Data Grid';
  }
  if (mode === 'graph') {
    return 'Graph';
  }

  return 'Traces';
}

// Function summary: Handles the normalize mode workflow for this module.
function normalizeMode(value: string | null): PanelMode {
  if (value === 'graph' || value === 'traces') {
    return value;
  }

  return 'grid';
}

// Function summary: Handles the next sort direction workflow for this module.
function nextSortDirection(value: SortDirection): SortDirection {
  if (value === 'Ascending') {
    return 'Descending';
  }

  return 'Ascending';
}

// Function summary: Handles the sort arrow workflow for this module.
function sortArrow(value: SortDirection) {
  if (value === 'Ascending') {
    return 'Asc';
  }

  return 'Desc';
}

// Function summary: Maps date time input into the shape required by callers.
function toDateTimeInput(value: string | null) {
  if (!value) {
    return '';
  }

  return value.slice(0, 16);
}

// Function summary: Converts a datetime-local wall time into a UTC API instant, or null when absent or malformed.
// The value is seeded from the query string, so a hand-edited or bookmarked URL can carry
// a value Date cannot parse; toISOString() then threw inside the request effect and the
// error boundary replaced the whole shell.
export function fromDateToApi(value: string) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toISOString();
}

// Function summary: Handles the format duration workflow for this module.
function formatDuration(seconds: number) {
  if (seconds < 60) {
    return `${seconds}s`;
  }
  const minutes = Math.round(seconds / 60);
  return `${minutes}m`;
}

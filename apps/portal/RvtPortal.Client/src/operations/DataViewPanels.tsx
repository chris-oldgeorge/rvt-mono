// File summary: Renders React operational panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for data-view grid, graph, and trace requests.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  BarChart3,
  Download,
  FileDown,
  ListFilter,
  RefreshCcw,
  Route,
  Search,
  Table2
} from 'lucide-react';
import {
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
  type ChartData,
  type ChartOptions
} from 'chart.js';
import { Line } from 'react-chartjs-2';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  downloadMonitorDataCsv,
  downloadMonitorTraceCsv,
  getDashboardSummary,
  getMonitorGraph,
  getMonitorTrace,
  isAbortError,
  queryMonitorDataGrid,
  queryMonitorTraces,
  type DownloadedFile
} from '../api/client';
import { Notice } from '../components/FormControls';
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
  TraceListResponse
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

// Function summary: Renders the DataViewsPanel React component and wires its local UI behavior.
export function DataViewsPanel({ locationPath, onRequestError }: DataViewsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [summary, setSummary] = useState<DashboardSummaryResponse | null>(null);
  const [deploymentId, setDeploymentId] = useState(initialParams.get('deploymentId') ?? '');
  const [mode, setMode] = useState<PanelMode>(normalizeMode(initialParams.get('view')));
  const [filterOption, setFilterOption] = useState(initialParams.get('filterOption') ?? '');
  const [fromDate, setFromDate] = useState(toDateTimeInput(initialParams.get('fromDate')));
  const [toDate, setToDate] = useState(toDateTimeInput(initialParams.get('toDate')));
  const [grid, setGrid] = useState<MonitorDataGridResponse | null>(null);
  const [graph, setGraph] = useState<MonitorGraphResponse | null>(null);
  const [traces, setTraces] = useState<TraceListResponse | null>(null);
  const [traceDetail, setTraceDetail] = useState<TraceDetailResponse | null>(null);
  const [selectedTraceId, setSelectedTraceId] = useState('');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sort, setSort] = useState(initialParams.get('sort') ?? defaultSort);
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);

  const handleError = useCallback((err: unknown) => {
    setError(err instanceof Error ? err.message : 'Unexpected data view error.');
    onRequestError(err);
  }, [onRequestError]);

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
        if (!deploymentId && response.calendarDeployments[0]) {
          setDeploymentId(response.calendarDeployments[0].value);
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
  }, [deploymentId, onRequestError]);

  useEffect(() => {
    if (!deploymentId) {
      return;
    }

    globalThis.history.replaceState(null, '', buildDataUrl({ deploymentId, mode, filterOption, fromDate, toDate, page, sort, sortDir }));
    setIsLoading(true);
    setError(null);
    const controller = new AbortController();
    const request = buildGridRequest({ filterOption, fromDate, toDate, page, sort, sortDir });
    if (mode === 'grid') {
      queryMonitorDataGrid(deploymentId, request, { signal: controller.signal })
        .then((response) => {
          setGrid(response);
          setFilterOptionFromResponse(response.filterOption);
        })
        .catch((err: Error) => {
          if (!isAbortError(err)) {
            handleError(err);
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) {
            setIsLoading(false);
          }
        });
      return () => controller.abort();
    }

    if (mode === 'graph') {
      getMonitorGraph(deploymentId, graphRequest(request), { signal: controller.signal })
        .then((response) => {
          setGraph(response);
          setFilterOptionFromResponse(response.filterOption);
        })
        .catch((err: Error) => {
          if (!isAbortError(err)) {
            handleError(err);
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) {
            setIsLoading(false);
          }
        });
      return () => controller.abort();
    }

    queryMonitorTraces(deploymentId, traceRequest(request), { signal: controller.signal })
      .then((response) => {
        setTraces(response);
        const firstTrace = response.traces[0];
        setSelectedTraceId((current) => current || firstTrace?.id || '');
      })
      .catch((err: Error) => {
        if (!isAbortError(err)) {
          handleError(err);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [deploymentId, filterOption, fromDate, handleError, mode, page, setFilterOptionFromResponse, sort, sortDir, toDate]);

  useEffect(() => {
    if (mode !== 'traces' || !deploymentId || !selectedTraceId) {
      setTraceDetail(null);
      return;
    }

    const controller = new AbortController();
    getMonitorTrace(deploymentId, selectedTraceId, { signal: controller.signal })
      .then(setTraceDetail)
      .catch((err: Error) => {
        if (!isAbortError(err)) {
          handleError(err);
        }
      });
    return () => controller.abort();
  }, [deploymentId, handleError, mode, selectedTraceId]);

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
  function handleSubmit(event: FormEvent) {
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
      const file = await downloadMonitorDataCsv(deploymentId, buildGridRequest({ filterOption, fromDate, toDate, page, sort, sortDir }));
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

  const filterOptions = currentFilterOptions(grid, graph);

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
                <option value={deployment.value} key={deployment.value}>{deployment.label}</option>
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
        {error && <Notice tone="error" message={error} />}
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
  onSort
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
          <span>{formatDateTime(grid.fromDate)} to {formatDateTime(grid.toDate)}</span>
        </div>
        <button className="secondary-button" type="button" onClick={onDownload} disabled={grid.total === 0 || isDownloading}>
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
function DataRow({ row, columns }: Readonly<{ row: MonitorDataRow; columns: ReadonlyArray<{ key: string; label: string }> }>) {
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
          <span>{graph.xAxisLabel} / {graph.yAxisLabel}</span>
        </div>
        <BarChart3 size={20} aria-hidden="true" />
      </div>
      <div className="chart-shell">
        {graph.datasets.length > 0 ? <Line data={chartData} options={chartOptions} /> : <p className="muted-text">No graph data for this range.</p>}
      </div>
      {graph.thresholds.length > 0 && (
        <div className="threshold-list">
          {graph.thresholds.map((threshold) => (
            <span className="status-chip neutral" key={threshold.id}>
              {threshold.field} {threshold.alertType} {formatNumber(threshold.limitOn)}
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
  onDownload
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
            <span>{detail ? `${formatDateTime(detail.fromDate)} to ${formatDateTime(detail.toDate)}` : 'Select a trace'}</span>
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
                    <td>{formatNumber(sample.x)}</td>
                    <td>{formatNumber(sample.y)}</td>
                    <td>{formatNumber(sample.z)}</td>
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
function Pagination({ page, totalPages, onPage }: Readonly<{ page: number; totalPages: number; onPage: (page: number) => void }>) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="pagination-row">
      <button className="secondary-button" type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {totalPages}</span>
      <button className="secondary-button" type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</button>
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

// Function summary: Handles the current filter options workflow for this module.
function currentFilterOptions(grid: MonitorDataGridResponse | null, graph: MonitorGraphResponse | null): OptionItem[] {
  if (grid?.filterOptions.length) {
    return grid.filterOptions;
  }
  if (graph?.filterOptions.length) {
    return graph.filterOptions;
  }

  return [];
}

// Function summary: Builds grid request data for callers.
function buildGridRequest({
  filterOption,
  fromDate,
  toDate,
  page,
  sort,
  sortDir
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
    sortDir
  };
}

// Function summary: Handles the graph request workflow for this module.
function graphRequest(request: MonitorDataGridRequest) {
  return {
    filterOption: request.filterOption,
    fromDate: request.fromDate,
    toDate: request.toDate
  };
}

// Function summary: Handles the trace request workflow for this module.
function traceRequest(request: MonitorDataGridRequest) {
  return {
    fromDate: request.fromDate,
    toDate: request.toDate
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
  sortDir
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
      pointRadius: 2
    }))
  };
}

// Function summary: Builds chart options data for callers.
function buildChartOptions(graph: MonitorGraphResponse): ChartOptions<'line'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom'
      }
    },
    scales: {
      x: {
        title: {
          display: true,
          text: graph.xAxisLabel
        }
      },
      y: {
        title: {
          display: true,
          text: graph.yAxisLabel
        }
      }
    }
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
    return formatNumber(point.x);
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

  return formatNumber(row.values[key]);
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

// Function summary: Handles the normalize sort direction workflow for this module.
function normalizeSortDirection(value: string | null): SortDirection {
  if (value === 'Ascending' || value === 'Descending') {
    return value;
  }

  return defaultSortDir;
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

// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number.parseInt(value ?? '', 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallback;
  }

  return parsed;
}

// Function summary: Maps date time input into the shape required by callers.
function toDateTimeInput(value: string | null) {
  if (!value) {
    return '';
  }

  return value.slice(0, 16);
}

// Function summary: Handles the from date to API workflow for this module.
function fromDateToApi(value: string) {
  if (!value) {
    return null;
  }

  return value;
}

// Function summary: Handles the format date time workflow for this module.
function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }

  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

// Function summary: Handles the format duration workflow for this module.
function formatDuration(seconds: number) {
  if (seconds < 60) {
    return `${seconds}s`;
  }
  const minutes = Math.round(seconds / 60);
  return `${minutes}m`;
}

// Function summary: Handles the format number workflow for this module.
function formatNumber(value?: number | null) {
  if (typeof value !== 'number') {
    return '';
  }

  return new Intl.NumberFormat('en-GB', { maximumFractionDigits: 4 }).format(value);
}

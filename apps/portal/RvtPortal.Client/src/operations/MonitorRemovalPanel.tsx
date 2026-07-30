// File summary: Renders the admin unattached-monitor removal panel with archive/delete confirmation.
// Major updates:
// - 2026-07-30 pending Closed the removal confirmation on failure so the error notice is not covered.
// - 2026-07-30 pending Moved the removal reason inside the confirmation so it can actually be typed.
// - 2026-07-30 pending Extracted from MonitorPanels.tsx during the monitor panel split.

import { Search, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { isAbortError, queryUnattachedMonitors, removeUnattachedMonitor } from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice } from '../components/FormControls';
import { returnToOr } from '../navigation';
import { useRequestLifecycle } from '../requestLifecycle';
import { pageSize, resetSearchPage } from './monitorShared';
import type { ListExecution, MonitorsPanelProps } from './monitorShared';
import type { QueryMonitorsRequest, SortDirection, UnattachedMonitorListItem } from '../dtos';

// Function summary: Renders the UnattachedMonitorRemovalPanel React component and wires its local UI behavior.
export function UnattachedMonitorRemovalPanel({ locationPath, onNavigate, onRequestError }: MonitorsPanelProps) {
  const [monitors, setMonitors] = useState<UnattachedMonitorListItem[]>([]);
  const [selectedMonitor, setSelectedMonitor] = useState<UnattachedMonitorListItem | null>(null);
  const [reason, setReason] = useState('');
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState('');
  const [page, setPage] = useState(1);
  const [sortKey, setSortKey] = useState('fleetNumber');
  const [sortDir, setSortDir] = useState<SortDirection>('Ascending');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QueryMonitorsRequest> | null>(null);
  const [refreshExecution, setRefreshExecution] = useState<ListExecution<QueryMonitorsRequest> | null>(null);
  const { claimRequest, ownsRequest, currentGeneration } = useRequestLifecycle();
  const [isRemoving, setIsRemoving] = useState(false);
  const backPath = returnToOr(locationPath, '/monitors');
  const columns = useMemo<DataGridColumn<UnattachedMonitorListItem>[]>(
    () => [
      { key: 'fleetNumber', header: 'Fleet', sortable: true, render: (monitor) => monitor.fleetNumber || 'Unassigned' },
      { key: 'serialId', header: 'Serial', sortable: true, render: (monitor) => monitor.serialId },
      { key: 'typeOfMonitor', header: 'Type', sortable: true, render: (monitor) => monitor.typeOfMonitor },
      { key: 'model', header: 'Model', render: (monitor) => monitor.model || 'Unknown' },
      { key: 'impact', header: 'Related data', render: (monitor) => removalImpactLabel(monitor) },
      {
        key: 'removalMode',
        header: 'Removal',
        render: (monitor) => (monitor.willArchiveOnRemoval ? 'Archive' : 'Delete'),
      },
    ],
    [],
  );
  const query = useMemo<QueryMonitorsRequest>(
    () => ({
      searchText,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
    }),
    [page, searchText, sortDir, sortKey],
  );
  const effectExecution = useMemo<ListExecution<QueryMonitorsRequest>>(() => ({ query }), [query]);
  const currentExecution = refreshExecution?.query === query ? refreshExecution : effectExecution;
  const isLoading = completedExecution !== currentExecution;

  // Function summary: Refreshes unattached monitor removal candidates after an event-owned mutation.
  const refreshMonitors = useCallback(async () => {
    const execution: ListExecution<QueryMonitorsRequest> = { query };
    const { controller, generation } = claimRequest();
    setRefreshExecution(execution);
    setCompletedExecution(null);
    try {
      const response = await queryUnattachedMonitors(execution.query, { signal: controller.signal });
      if (!ownsRequest(controller, generation)) {
        return;
      }
      setMonitors(response.results);
      setTotal(response.total);
      setTotalPages(response.totalPages);
      setError(null);
      setCompletedExecution(execution);
    } catch (err) {
      if (!ownsRequest(controller, generation) || isAbortError(err)) {
        return;
      }
      setError((err as Error).message);
      onRequestError(err);
      setCompletedExecution(execution);
    }
  }, [claimRequest, onRequestError, ownsRequest, query]);

  useEffect(() => {
    const { controller, generation } = claimRequest();
    queryUnattachedMonitors(effectExecution.query, { signal: controller.signal })
      .then((response) => {
        if (!ownsRequest(controller, generation)) {
          return;
        }
        setMonitors(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
        setCompletedExecution(effectExecution);
      })
      .catch((err: Error) => {
        if (!ownsRequest(controller, generation) || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedExecution(effectExecution);
      });
    return () => controller.abort();
  }, [claimRequest, effectExecution, onRequestError, ownsRequest]);

  // Function summary: Handles search text changes for unattached monitor removal candidates.
  function handleSearch(value: string) {
    resetSearchPage(value, setSearchText, setPage);
  }

  // Function summary: Handles sort changes for unattached monitor removal candidates.
  function handleSortChange(key: string, direction: GridSortDirection) {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }

  // Function summary: Dismisses the removal confirmation and drops the reason typed inside it.
  function handleCancelRemoval() {
    setSelectedMonitor(null);
    setReason('');
  }

  // Function summary: Removes or archives the selected unattached monitor.
  async function handleRemove() {
    if (!selectedMonitor) {
      return;
    }

    const removeGeneration = currentGeneration();
    setIsRemoving(true);
    setError(null);
    try {
      const response = await removeUnattachedMonitor(selectedMonitor.id, { reason });
      setNotice(response.message);
      if (currentGeneration() !== removeGeneration) {
        return;
      }
      await refreshMonitors();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      // Close on failure too: the error notice renders on the panel, which the modal
      // confirmation would cover, so leaving it open hides the reason it failed.
      setSelectedMonitor(null);
      setReason('');
      setIsRemoving(false);
    }
  }

  const selectedMonitorName = selectedMonitor?.fleetNumber || selectedMonitor?.serialId || 'this monitor';
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Admin</p>
          <h2>Unattached Monitors</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input
          value={searchText}
          onChange={(event) => handleSearch(event.target.value)}
          placeholder="Search unattached monitors"
        />
      </label>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      <DataGrid
        columns={columns}
        rows={monitors}
        getRowKey={(monitor) => monitor.id}
        emptyMessage="No unattached monitors match the current filters."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={pageSize}
        total={total}
        totalPages={totalPages}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={[
          {
            label: 'Remove monitor',
            icon: <Trash2 size={16} aria-hidden="true" />,
            onClick: (monitor) => setSelectedMonitor(monitor),
          },
        ]}
      />
      <ConfirmDialog
        open={Boolean(selectedMonitor)}
        title={selectedMonitor?.willArchiveOnRemoval ? 'Archive monitor' : 'Delete monitor'}
        message={
          selectedMonitor?.willArchiveOnRemoval
            ? `Archive ${selectedMonitorName}? Related data will be retained.`
            : `Delete ${selectedMonitorName}? This monitor has no related data.`
        }
        confirmLabel={selectedMonitor?.willArchiveOnRemoval ? 'Archive' : 'Delete'}
        isBusy={isRemoving}
        onCancel={handleCancelRemoval}
        onConfirm={handleRemove}
      >
        <FormField label="Removal reason">
          <input
            value={reason}
            maxLength={512}
            disabled={isRemoving}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Reason recorded for audit history"
          />
        </FormField>
      </ConfirmDialog>
    </section>
  );
}

// Function summary: Formats related data counts for unattached monitor removal candidates.
function removalImpactLabel(monitor: UnattachedMonitorListItem) {
  const impact = monitor.impact;
  if (!monitor.hasRelatedData) {
    return 'None';
  }

  const parts = [
    impact.deploymentCount ? `${impact.deploymentCount} deployments` : null,
    impact.notificationCount ? `${impact.notificationCount} notifications` : null,
    impact.alertRuleCount ? `${impact.alertRuleCount} alert rules` : null,
    impact.measurementRowCount ? `${impact.measurementRowCount} data rows` : null,
  ].filter(Boolean);

  return parts.join(', ');
}

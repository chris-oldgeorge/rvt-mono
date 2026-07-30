// File summary: Renders the site monitor-assignment panel for adding and removing monitors on a contract.
// Major updates:
// - 2026-07-30 pending Confirmed contract removal before it runs.
// - 2026-07-30 pending Extracted from MonitorPanels.tsx during the monitor panel split.

import { CheckCircle2, Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { addMonitorToContract, getMonitorAssignment, removeMonitorFromContract } from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice } from '../components/FormControls';
import { returnToOr, withReturnTo } from '../navigation';
import type { MonitorsPanelProps } from './panelShared';
import type { MonitorAssignmentContextResponse, MonitorListItem } from '../dtos';

// Function summary: Renders the MonitorAssignmentPanel React component and wires its local UI behavior.
export function MonitorAssignmentPanel({
  siteId,
  contractId,
  locationPath,
  onNavigate,
  onRequestError,
}: MonitorsPanelProps & Readonly<{ siteId: string; contractId?: string | null }>) {
  const [context, setContext] = useState<MonitorAssignmentContextResponse | null>(null);
  const [selectedContractId, setSelectedContractId] = useState(contractId ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [pendingRemoval, setPendingRemoval] = useState<MonitorListItem | null>(null);
  const backPath = returnToOr(locationPath, '/monitors');
  useEffect(() => {
    getMonitorAssignment(siteId, contractId)
      .then((response) => {
        setContext(response);
        setSelectedContractId(response.contractId ?? contractId ?? '');
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError, siteId]);

  // Function summary: Handles the handle contract change workflow for this module.
  function handleContractChange(value: string) {
    setSelectedContractId(value);
    const assignmentPath = value
      ? `/monitors/assign?siteId=${siteId}&contractId=${value}`
      : `/monitors/assign?siteId=${siteId}`;
    onNavigate(withReturnTo(assignmentPath, backPath));
  }

  async function handleAdd(monitor: MonitorListItem) {
    if (!selectedContractId) {
      setError('Select a contract before assigning a monitor.');
      return;
    }
    setIsBusy(true);
    try {
      await addMonitorToContract(monitor.id, { contractId: selectedContractId });
      setContext(await getMonitorAssignment(siteId, selectedContractId));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
    }
  }

  async function handleRemove() {
    const monitor = pendingRemoval;
    if (!monitor) {
      return;
    }

    setIsBusy(true);
    try {
      await removeMonitorFromContract(monitor.id);
      setContext(await getMonitorAssignment(siteId, selectedContractId));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsBusy(false);
      setPendingRemoval(null);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Site Assignment</p>
          <h2>{context?.siteName ?? 'Monitor Assignment'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      {error && <Notice tone="error" message={error} />}
      <FormField label="Contract">
        <select value={selectedContractId} onChange={(event) => handleContractChange(event.target.value)}>
          <option value="">Select a contract</option>
          {context?.contracts.map((contract) => (
            <option value={contract.value} key={contract.value}>
              {contract.label}
            </option>
          ))}
        </select>
      </FormField>
      <section className="split-grid">
        <div>
          <div className="subsection-heading">
            <Plus size={18} aria-hidden="true" />
            <h3>Available</h3>
          </div>
          <DataGrid
            columns={assignmentColumns}
            rows={context?.availableMonitors ?? []}
            getRowKey={(monitor) => monitor.id}
            emptyMessage="No available monitors."
            isLoading={!context}
            page={1}
            pageSize={Math.max(context?.availableMonitors.length ?? 0, 1)}
            total={context?.availableMonitors.length ?? 0}
            totalPages={(context?.availableMonitors.length ?? 0) > 0 ? 1 : 0}
            rowActions={[
              {
                label: 'Assign monitor',
                icon: <Plus size={16} aria-hidden="true" />,
                onClick: handleAdd,
                disabled: () => isBusy || !selectedContractId,
              },
            ]}
          />
        </div>
        <div>
          <div className="subsection-heading">
            <CheckCircle2 size={18} aria-hidden="true" />
            <h3>Assigned</h3>
          </div>
          <DataGrid
            columns={assignmentColumns}
            rows={context?.assignedMonitors ?? []}
            getRowKey={(monitor) => monitor.id}
            emptyMessage="No monitors are assigned."
            isLoading={!context}
            page={1}
            pageSize={Math.max(context?.assignedMonitors.length ?? 0, 1)}
            total={context?.assignedMonitors.length ?? 0}
            totalPages={(context?.assignedMonitors.length ?? 0) > 0 ? 1 : 0}
            rowActions={[
              {
                label: 'Remove monitor',
                icon: <Trash2 size={16} aria-hidden="true" />,
                onClick: setPendingRemoval,
                disabled: () => isBusy,
              },
            ]}
          />
        </div>
      </section>
      <ConfirmDialog
        open={Boolean(pendingRemoval)}
        title="Remove monitor from contract"
        message={`Remove ${pendingRemoval?.fleetNumber || pendingRemoval?.serialId || 'this monitor'} from ${context?.siteName || 'this site'}? The monitor returns to the unassigned pool.`}
        confirmLabel="Remove from contract"
        isBusy={isBusy}
        onCancel={() => setPendingRemoval(null)}
        onConfirm={handleRemove}
      />
    </section>
  );
}

const assignmentColumns: DataGridColumn<MonitorListItem>[] = [
  { key: 'fleetNumber', header: 'Fleet', render: (monitor) => monitor.fleetNumber || 'Unassigned' },
  { key: 'serialId', header: 'Serial', render: (monitor) => monitor.serialId },
  { key: 'typeOfMonitor', header: 'Type', render: (monitor) => monitor.typeOfMonitor },
  { key: 'siteName', header: 'Site', render: (monitor) => monitor.siteName || 'Not deployed' },
];

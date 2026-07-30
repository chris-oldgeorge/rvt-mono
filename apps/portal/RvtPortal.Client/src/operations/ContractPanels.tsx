// File summary: Renders the contract list, detail, and form panels for day-to-day RVT monitoring workflows.
// Major updates:
// - 2026-07-30 pending Split from ContractSitePanels.tsx so contract and site panels live in separate modules.

import { Edit3, Eye, FileText, MapPinned, Plus, Save, Search, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  createContract,
  deleteContract,
  getContract,
  getContractOptions,
  isAbortError,
  queryContracts,
  updateContract,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { ReadOnlyRow } from '../components/ReadOnlyRow';
import { formatDate } from '../format';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { normalizeSortDirection, parsePositiveInt, useGridSortHandler } from '../gridQuery';
import { pageSize } from './contractSiteShared';
import type { ListExecution, OperationsRouteProps } from './contractSiteShared';
import type {
  ContractDetailResponse,
  ContractListItem,
  ContractMutationRequest,
  ContractOptionsResponse,
  QueryContractsRequest,
  SortDirection,
} from '../dtos';

// Function summary: Renders the ContractsPanel React component and wires its local UI behavior.
export function ContractsPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const mode = parseContractsMode(locationPath);
  if (mode.kind === 'create') {
    return <ContractFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return (
      <ContractFormPanel
        contractId={mode.contractId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (mode.kind === 'detail') {
    return (
      <ContractDetailPanel
        contractId={mode.contractId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  return <ContractListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the ContractListPanel React component and wires its local UI behavior.
function ContractListPanel({ locationPath, onNavigate, onRequestError }: OperationsRouteProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [contracts, setContracts] = useState<ContractListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'contractNumber');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [error, setError] = useState<string | null>(null);
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QueryContractsRequest> | null>(null);
  const columns = useMemo<DataGridColumn<ContractListItem>[]>(
    () => [
      {
        key: 'contractNumber',
        header: 'Contract',
        sortable: true,
        render: (contract) => (
          <span className="cell-with-icon">
            <FileText size={16} aria-hidden="true" />
            {contract.contractNumber}
          </span>
        ),
      },
      { key: 'siteName', header: 'Site', sortable: true, render: (contract) => contract.siteName || 'Unassigned' },
      { key: 'companyName', header: 'Company', sortable: true, render: (contract) => contract.companyName || 'None' },
      { key: 'onHireDate', header: 'On Hire', sortable: true, render: (contract) => formatDate(contract.onHireDate) },
      {
        key: 'offHireDate',
        header: 'Off Hire',
        sortable: true,
        render: (contract) => formatDate(contract.offHireDate) || 'Open',
      },
    ],
    [],
  );
  const query = useMemo<QueryContractsRequest>(
    () => ({
      searchText,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
    }),
    [page, searchText, sortDir, sortKey],
  );
  const execution = useMemo<ListExecution<QueryContractsRequest>>(() => ({ query }), [query]);
  const isLoading = completedExecution !== execution;
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);
  const returnPath = currentRoutePath(locationPath);
  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildContractsUrl({ searchText, page, sort: sortKey, sortDir }));
    queryContracts(execution.query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setContracts(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
        setCompletedExecution(execution);
      })
      .catch((err: Error) => {
        if (controller.signal.aborted || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedExecution(execution);
      });
    return () => controller.abort();
  }, [execution, onRequestError, page, searchText, sortDir, sortKey]);
  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Operations</p>
          <h2>Contracts</h2>
        </div>
        <button
          className="secondary-button"
          type="button"
          onClick={() => onNavigate(withReturnTo('/contracts/new', returnPath))}
        >
          <Plus size={17} aria-hidden="true" />
          <span>Create Contract</span>
        </button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input
          value={searchText}
          onChange={(event) => handleSearch(event.target.value)}
          placeholder="Search contracts"
        />
      </label>
      <DataGrid
        columns={columns}
        rows={contracts}
        getRowKey={(contract) => contract.id}
        emptyMessage="No contracts match the current search."
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
            label: 'View contract',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}`, returnPath)),
          },
          {
            label: 'Edit contract',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (contract) => onNavigate(withReturnTo(`/contracts/${contract.id}/edit`, returnPath)),
          },
        ]}
      />
    </section>
  );
}

// Function summary: Renders the ContractDetailPanel React component and wires its local UI behavior.
function ContractDetailPanel({
  contractId,
  locationPath,
  onNavigate,
  onRequestError,
}: OperationsRouteProps & Readonly<{ contractId: string }>) {
  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const backPath = returnToOr(locationPath, '/contracts');
  const detailPath = currentRoutePath(locationPath);
  useEffect(() => {
    getContract(contractId)
      .then((response) => {
        setContract(response.item ?? null);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError]);
  async function handleDelete() {
    setIsDeleting(true);
    setError(null);
    try {
      await deleteContract(contractId);
      onNavigate(backPath);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setConfirmDelete(false);
      setIsDeleting(false);
    }
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Contract</p>
          <h2>{contract?.contractNumber ?? 'Loading contract'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            Back
          </button>
          <button
            className="secondary-button"
            type="button"
            onClick={() => onNavigate(withReturnTo(`/contracts/${contractId}/edit`, detailPath))}
            disabled={!contract}
          >
            <Edit3 size={17} aria-hidden="true" />
            <span>Edit</span>
          </button>
          <button className="danger-button" type="button" onClick={() => setConfirmDelete(true)} disabled={!contract}>
            <Trash2 size={17} aria-hidden="true" />
            <span>Delete</span>
          </button>
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {contract && (
        <div className="detail-stack">
          <ReadOnlyRow label="Contract Number" value={contract.contractNumber} />
          <ReadOnlyRow label="Company" value={contract.companyName || 'None'} />
          <ReadOnlyRow label="Site" value={contract.siteName || 'Unassigned'} />
          <ReadOnlyRow label="On Hire Date" value={formatDate(contract.onHireDate)} />
          <ReadOnlyRow label="Off Hire Date" value={formatDate(contract.offHireDate) || 'Open'} />
          {contract.siteId && (
            <button
              className="secondary-button inline"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/sites/${contract.siteId}`, detailPath))}
            >
              <MapPinned size={17} aria-hidden="true" />
              <span>Open site</span>
            </button>
          )}
        </div>
      )}
      <ConfirmDialog
        open={confirmDelete}
        title="Delete contract"
        message={`Delete ${contract?.contractNumber ?? 'this contract'}?`}
        confirmLabel="Delete"
        isBusy={isDeleting}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={handleDelete}
      />
    </section>
  );
}

type ContractFormState = {
  contractNumber: string;
  companyId: string;
  siteId: string;
  onHireDate: string;
  offHireDate: string;
};

// Function summary: Renders the ContractFormPanel React component and wires its local UI behavior.
function ContractFormPanel({
  contractId,
  locationPath,
  onNavigate,
  onRequestError,
}: OperationsRouteProps & Readonly<{ contractId?: string }>) {
  const isEdit = Boolean(contractId);
  const backPath = returnToOr(locationPath, contractId ? `/contracts/${contractId}` : '/contracts');
  const [form, setForm] = useState<ContractFormState>({
    contractNumber: '',
    companyId: '',
    siteId: '',
    onHireDate: toDateInput(new Date().toISOString()),
    offHireDate: '',
  });
  const [options, setOptions] = useState<ContractOptionsResponse>({ companies: [], sites: [] });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  useEffect(() => {
    getContractOptions()
      .then((nextOptions) => {
        setOptions(nextOptions);
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError]);
  useEffect(() => {
    if (!contractId) {
      return;
    }
    getContract(contractId)
      .then((response) => {
        const item = response.item;
        if (item) {
          setForm({
            contractNumber: item.contractNumber,
            companyId: item.companyId,
            siteId: item.siteId ?? '',
            onHireDate: toDateInput(item.onHireDate),
            offHireDate: toDateInput(item.offHireDate),
          });
          setOptions({ companies: item.companies, sites: item.sites });
        }
        setError(null);
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [contractId, onRequestError]);
  async function handleCompanyChange(companyId: string) {
    setForm((current) => ({ ...current, companyId, siteId: '' }));
    setStatus(null);
    try {
      setOptions(await getContractOptions(companyId || undefined));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }
  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const payload: ContractMutationRequest = {
        contractNumber: form.contractNumber,
        companyId: form.companyId,
        siteId: form.siteId || null,
        onHireDate: form.onHireDate,
        offHireDate: form.offHireDate || null,
      };
      const response = isEdit && contractId ? await updateContract(contractId, payload) : await createContract(payload);
      const saved = response.item;
      setStatus(isEdit ? 'Contract updated.' : 'Contract created.');
      if (saved?.id) {
        onNavigate(`/contracts/${saved.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }
  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Contract</p>
          <h2>{isEdit ? 'Edit Contract' : 'Add Contract'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Contract Number">
          <input
            value={form.contractNumber}
            onChange={(event) => setForm({ ...form, contractNumber: event.target.value })}
            maxLength={20}
          />
        </FormField>
        <FormField label="Company">
          <select value={form.companyId} onChange={(event) => handleCompanyChange(event.target.value)}>
            <option value="">Select a Company</option>
            {options.companies.map((company) => (
              <option value={company.value} key={company.value}>
                {company.label}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Site">
          <select value={form.siteId} onChange={(event) => setForm({ ...form, siteId: event.target.value })}>
            <option value="">Unassigned</option>
            {options.sites.map((site) => (
              <option value={site.value} key={site.value}>
                {site.label}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="On Hire Date">
          <input
            value={form.onHireDate}
            onChange={(event) => setForm({ ...form, onHireDate: event.target.value })}
            type="date"
          />
        </FormField>
        <FormField label="Off Hire Date">
          <input
            value={form.offHireDate}
            onChange={(event) => setForm({ ...form, offHireDate: event.target.value })}
            type="date"
          />
        </FormField>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel={isEdit ? 'Update Contract' : 'Create Contract'}
        />
      </form>
    </section>
  );
}

// Function summary: Handles the parse contracts mode workflow for this module.
function parseContractsMode(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (path === '/contracts/new') {
    return { kind: 'create' as const };
  }
  const edit = /^\/contracts\/([^/]+)\/edit$/i.exec(path);
  if (edit) {
    return { kind: 'edit' as const, contractId: edit[1] };
  }
  const detail = /^\/contracts\/([^/]+)$/i.exec(path);
  if (detail) {
    return { kind: 'detail' as const, contractId: detail[1] };
  }
  return { kind: 'list' as const };
}

// Function summary: Builds contracts url data for callers.
function buildContractsUrl(options: { searchText: string; page: number; sort: string; sortDir: SortDirection }) {
  const params = new URLSearchParams();
  if (options.searchText) {
    params.set('q', options.searchText);
  }
  if (options.page > 1) {
    params.set('page', String(options.page));
  }
  if (options.sort !== 'contractNumber') {
    params.set('sort', options.sort);
  }
  if (options.sortDir !== 'Ascending') {
    params.set('sortDir', options.sortDir);
  }
  const query = params.toString();
  return query ? `/contracts?${query}` : '/contracts';
}

// Function summary: Maps date input into the shape required by callers.
function toDateInput(value?: string | null) {
  if (!value) {
    return '';
  }
  return value.slice(0, 10);
}

// File summary: Renders the company administration list, detail, and form panels.
// Major updates:
// - 2026-07-30 pending Split from AdminPanels.tsx so company and user admin panels live in separate modules.

import { Building2, CheckCircle2, Edit3, Eye, Plus, Save, Search, Trash2, UsersRound } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  createCompany,
  deleteCompany,
  getCompany,
  isAbortError,
  queryCompanies,
  searchLookup,
  updateCompany,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { ReadOnlyRow } from '../components/ReadOnlyRow';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { normalizeSortDirection, parsePositiveInt } from '../gridQuery';
import type { AdminPanelProps, LookupExecution, RequestExecution } from './adminShared';
import type { CompanyDetailResponse, CompanyListItem, QueryCompaniesRequest, SortDirection } from '../dtos';

const companyPageSize = 10;

// Function summary: Renders the CompaniesPanel React component and wires its local UI behavior.
export function CompaniesPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const mode = parseCompaniesMode(locationPath);
  if (mode.kind === 'create') {
    return <CompanyFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return (
      <CompanyFormPanel
        companyId={mode.companyId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (mode.kind === 'detail') {
    return (
      <CompanyDetailPanel
        companyId={mode.companyId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  return <CompanyListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the CompanyListPanel React component and wires its local UI behavior.
function CompanyListPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'companyName');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [suggestionResult, setSuggestionResult] = useState<{
    execution: LookupExecution;
    results: string[];
  } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const columns = useMemo<DataGridColumn<CompanyListItem>[]>(
    () => [
      {
        key: 'companyName',
        header: 'Company',
        sortable: true,
        render: (company) => (
          <span className="cell-with-icon">
            <Building2 size={16} aria-hidden="true" />
            {company.companyName}
          </span>
        ),
      },
      { key: 'userCount', header: 'Users', sortable: true, align: 'end', render: (company) => company.userCount },
      { key: 'sites', header: 'Sites', sortable: true, render: (company) => company.sites || 'None' },
      { key: 'contracts', header: 'Contracts', sortable: true, render: (company) => company.contracts || 'None' },
    ],
    [],
  );

  const query = useMemo<QueryCompaniesRequest>(
    () => ({
      searchText,
      page,
      pageSize: companyPageSize,
      sort: sortKey,
      sortDir,
    }),
    [page, searchText, sortDir, sortKey],
  );
  const execution = useMemo<RequestExecution<QueryCompaniesRequest>>(() => ({ query, generation: 0 }), [query]);
  const [completedExecution, setCompletedExecution] = useState<RequestExecution<QueryCompaniesRequest> | null>(null);
  const suggestionExecution = useMemo<LookupExecution | null>(
    () => (searchText.length >= 2 ? { query: searchText } : null),
    [searchText],
  );
  const isLoading = completedExecution !== execution;
  const suggestions = suggestionResult?.execution === suggestionExecution ? suggestionResult.results : [];
  const returnPath = currentRoutePath(locationPath);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildCompaniesUrl(searchText, page, sortKey, sortDir));
    queryCompanies(execution.query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setCompanies(response.results);
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

  useEffect(() => {
    if (!suggestionExecution) {
      return;
    }
    const controller = new AbortController();
    const handle = globalThis.setTimeout(() => {
      searchLookup('companies', suggestionExecution.query, {}, { signal: controller.signal })
        .then((response) => {
          if (!controller.signal.aborted) {
            setSuggestionResult({ execution: suggestionExecution, results: response.results });
          }
        })
        .catch((err: Error) => {
          if (!controller.signal.aborted && !isAbortError(err)) {
            setSuggestionResult({ execution: suggestionExecution, results: [] });
          }
        });
    }, 180);
    return () => {
      controller.abort();
      globalThis.clearTimeout(handle);
    };
  }, [suggestionExecution]);

  // Function summary: Handles the handle search workflow for this module.
  function handleSearch(value: string) {
    setSearchText(value);
    setPage(1);
  }

  // Function summary: Handles the handle sort change workflow for this module.
  function handleSortChange(key: string, direction: GridSortDirection) {
    setSortKey(key);
    setSortDir(direction);
    setPage(1);
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Administration</p>
          <h2>Companies</h2>
        </div>
        <button
          className="secondary-button"
          type="button"
          onClick={() => onNavigate(withReturnTo('/companies/new', returnPath))}
        >
          <Plus size={17} aria-hidden="true" />
          <span>Create Company</span>
        </button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input
          value={searchText}
          onChange={(event) => handleSearch(event.target.value)}
          placeholder="Search companies"
        />
      </label>
      {suggestions.length > 0 && (
        <div className="suggestions" aria-label="Company search suggestions">
          {suggestions.map((item) => (
            <button type="button" key={item} onClick={() => handleSearch(item)}>
              {item}
            </button>
          ))}
        </div>
      )}
      {notice && <Notice tone="info" message={notice} />}
      <DataGrid
        columns={columns}
        rows={companies}
        getRowKey={(company) => company.id}
        emptyMessage="No companies match the current search."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={companyPageSize}
        total={total}
        totalPages={totalPages}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={[
          {
            label: 'View company',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (company) => onNavigate(withReturnTo(`/companies/${company.id}`, returnPath)),
          },
          {
            label: 'Edit company',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (company) => onNavigate(withReturnTo(`/companies/${company.id}/edit`, returnPath)),
          },
          {
            label: 'Company users',
            icon: <UsersRound size={16} aria-hidden="true" />,
            onClick: (company) =>
              onNavigate(
                withReturnTo(
                  `/users?companyId=${encodeURIComponent(company.id)}&companyName=${encodeURIComponent(company.companyName)}`,
                  returnPath,
                ),
              ),
          },
          {
            label: 'Delete company',
            icon: <Trash2 size={16} aria-hidden="true" />,
            onClick: (company) => setNotice(`Open ${company.companyName} to delete with confirmation.`),
          },
        ]}
      />
    </section>
  );
}

// Function summary: Renders the CompanyDetailPanel React component and wires its local UI behavior.
function CompanyDetailPanel({
  companyId,
  locationPath,
  onNavigate,
  onRequestError,
}: AdminPanelProps & Readonly<{ companyId: string }>) {
  const [company, setCompany] = useState<CompanyDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const backPath = returnToOr(locationPath, '/companies');
  const detailPath = currentRoutePath(locationPath);

  useEffect(() => {
    getCompany(companyId)
      .then((response) => setCompany(response.item ?? null))
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [companyId, onRequestError]);

  async function handleDelete() {
    setIsDeleting(true);
    try {
      await deleteCompany(companyId);
      onNavigate(backPath);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsDeleting(false);
      setConfirmDelete(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Company</p>
          <h2>{company?.companyName ?? 'Loading company'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            Back
          </button>
          <button
            className="secondary-button"
            type="button"
            onClick={() => onNavigate(withReturnTo(`/companies/${companyId}/edit`, detailPath))}
            disabled={!company}
          >
            <Edit3 size={17} aria-hidden="true" />
            <span>Edit</span>
          </button>
          <button className="danger-button" type="button" onClick={() => setConfirmDelete(true)} disabled={!company}>
            <Trash2 size={17} aria-hidden="true" />
            <span>Delete</span>
          </button>
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {company && (
        <>
          <div className="detail-grid">
            <ReadOnlyMetric label="Users" value={company.userCount} />
            <ReadOnlyMetric label="Sites" value={company.siteCount} />
            <ReadOnlyMetric label="Contracts" value={company.contractCount} />
          </div>
          <div className="detail-stack">
            <ReadOnlyRow label="Company name" value={company.companyName} />
            <ReadOnlyRow label="Sites" value={company.sites || 'None'} />
            <ReadOnlyRow label="Contracts" value={company.contracts || 'None'} />
          </div>
          <button
            className="secondary-button inline"
            type="button"
            onClick={() =>
              onNavigate(
                withReturnTo(
                  `/users?companyId=${company.id}&companyName=${encodeURIComponent(company.companyName)}`,
                  detailPath,
                ),
              )
            }
          >
            <UsersRound size={17} aria-hidden="true" />
            <span>Manage users</span>
          </button>
        </>
      )}
      <ConfirmDialog
        open={confirmDelete}
        title="Delete company"
        message={`Delete ${company?.companyName ?? 'this company'} and its company users?`}
        confirmLabel="Delete"
        isBusy={isDeleting}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={handleDelete}
      />
    </section>
  );
}

// Function summary: Renders the CompanyFormPanel React component and wires its local UI behavior.
function CompanyFormPanel({
  companyId,
  locationPath,
  onNavigate,
  onRequestError,
}: AdminPanelProps & Readonly<{ companyId?: string }>) {
  const isEdit = Boolean(companyId);
  const [companyName, setCompanyName] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, '/companies');

  useEffect(() => {
    if (!companyId) {
      return;
    }
    getCompany(companyId)
      .then((response) => setCompanyName(response.item?.companyName ?? ''))
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [companyId, onRequestError]);

  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const response =
        isEdit && companyId ? await updateCompany(companyId, { companyName }) : await createCompany({ companyName });
      const saved = response.item;
      setStatus(isEdit ? 'Company updated.' : 'Company created.');
      if (saved?.id) {
        onNavigate(`/companies/${saved.id}`);
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
          <p>Company</p>
          <h2>{isEdit ? 'Edit Company' : 'Add Company'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Name">
          <input value={companyName} onChange={(event) => setCompanyName(event.target.value)} maxLength={50} />
        </FormField>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel={isEdit ? 'Update Company' : 'Create Company'}
        />
      </form>
    </section>
  );
}

// Function summary: Renders the ReadOnlyMetric React component and wires its local UI behavior.
function ReadOnlyMetric({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="metric compact-metric">
      <CheckCircle2 size={18} aria-hidden="true" />
      <div>
        <strong>{value}</strong>
        <span>{label}</span>
      </div>
    </div>
  );
}

// Function summary: Handles the parse companies mode workflow for this module.
function parseCompaniesMode(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (path === '/companies/new') {
    return { kind: 'create' as const };
  }
  const edit = /^\/companies\/([^/]+)\/edit$/i.exec(path);
  if (edit) {
    return { kind: 'edit' as const, companyId: edit[1] };
  }
  const detail = /^\/companies\/([^/]+)$/i.exec(path);
  if (detail) {
    return { kind: 'detail' as const, companyId: detail[1] };
  }
  return { kind: 'list' as const };
}

// Function summary: Builds companies url data for callers.
function buildCompaniesUrl(searchText: string, page: number, sort: string, sortDir: SortDirection) {
  const params = new URLSearchParams();
  if (searchText) {
    params.set('q', searchText);
  }
  if (page > 1) {
    params.set('page', String(page));
  }
  if (sort !== 'companyName') {
    params.set('sort', sort);
  }
  if (sortDir !== 'Ascending') {
    params.set('sortDir', sortDir);
  }
  const query = params.toString();
  return query ? `/companies?${query}` : '/companies';
}

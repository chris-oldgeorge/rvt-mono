// File summary: Renders React administration panels and client-side state for portal admin workflows.
// Major updates:
// - 2026-06-26 pending Added cancellation for admin list and lookup requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for company and user detail/edit forms.
// - 2026-06-26 pending Allowed installer users to carry a company assignment for scoped installer access.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import {
  Building2,
  CheckCircle2,
  Edit3,
  Eye,
  KeyRound,
  Lock,
  Mail,
  Plus,
  Save,
  Search,
  Trash2,
  Unlock,
  UsersRound
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  createCompany,
  createUser,
  deleteCompany,
  deleteUser,
  disableUser,
  enableUser,
  getCompany,
  getUser,
  getUserOptions,
  isAbortError,
  queryCompanies,
  queryUsers,
  resendUserConfirmation,
  searchLookup,
  sendUserResetPasswordLink,
  updateCompany,
  updateUser
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn, GridSortDirection } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import type {
  CompanyDetailResponse,
  CompanyListItem,
  OptionItem,
  QueryCompaniesRequest,
  QueryUsersRequest,
  SortDirection,
  UserDetailResponse,
  UserListItem,
  UserMutationRequest
} from '../dtos';

const companyPageSize = 10;
const userPageSize = 10;
const companyUserRole = 'CompanyUser';
const installerRole = 'RVTInstaller';

type AdminPanelCallbacks = Readonly<{
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type AdminPanelProps = AdminPanelCallbacks & Readonly<{
  locationPath: string;
}>;

// Function summary: Renders the CompaniesPanel React component and wires its local UI behavior.
export function CompaniesPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const mode = parseCompaniesMode(locationPath);
  if (mode.kind === 'create') {
    return <CompanyFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return <CompanyFormPanel companyId={mode.companyId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'detail') {
    return <CompanyDetailPanel companyId={mode.companyId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  return <CompanyListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the UsersPanel React component and wires its local UI behavior.
export function UsersPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const mode = parseUsersMode(locationPath);
  if (mode.kind === 'create') {
    return <UserFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return <UserFormPanel userId={mode.userId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'detail') {
    return <UserDetailPanel userId={mode.userId} locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  return <UserListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
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
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const columns = useMemo<DataGridColumn<CompanyListItem>[]>(() => [
    {
      key: 'companyName',
      header: 'Company',
      sortable: true,
      render: (company) => (
        <span className="cell-with-icon">
          <Building2 size={16} aria-hidden="true" />
          {company.companyName}
        </span>
      )
    },
    { key: 'userCount', header: 'Users', sortable: true, align: 'end', render: (company) => company.userCount },
    { key: 'sites', header: 'Sites', sortable: true, render: (company) => company.sites || 'None' },
    { key: 'contracts', header: 'Contracts', sortable: true, render: (company) => company.contracts || 'None' }
  ], []);

  const query = useMemo<QueryCompaniesRequest>(() => ({
    searchText,
    page,
    pageSize: companyPageSize,
    sort: sortKey,
    sortDir
  }), [page, searchText, sortDir, sortKey]);
  const returnPath = currentRoutePath(locationPath);

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildCompaniesUrl(searchText, page, sortKey, sortDir));
    setIsLoading(true);
    queryCompanies(query, { signal: controller.signal })
      .then((response) => {
        setCompanies(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [onRequestError, page, query, searchText, sortDir, sortKey]);

  useEffect(() => {
    if (searchText.length < 2) {
      setSuggestions([]);
      return;
    }
    const controller = new AbortController();
    const handle = globalThis.setTimeout(() => {
      searchLookup('companies', searchText, {}, { signal: controller.signal })
        .then((response) => setSuggestions(response.results))
        .catch((err: Error) => {
          if (!isAbortError(err)) {
            setSuggestions([]);
          }
        });
    }, 180);
    return () => {
      controller.abort();
      globalThis.clearTimeout(handle);
    };
  }, [searchText]);

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
        <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo('/companies/new', returnPath))}>
          <Plus size={17} aria-hidden="true" />
          <span>Create Company</span>
        </button>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => handleSearch(event.target.value)} placeholder="Search companies" />
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
            onClick: (company) => onNavigate(withReturnTo(`/companies/${company.id}`, returnPath))
          },
          {
            label: 'Edit company',
            icon: <Edit3 size={16} aria-hidden="true" />,
            onClick: (company) => onNavigate(withReturnTo(`/companies/${company.id}/edit`, returnPath))
          },
          {
            label: 'Company users',
            icon: <UsersRound size={16} aria-hidden="true" />,
            onClick: (company) => onNavigate(withReturnTo(`/users?companyId=${encodeURIComponent(company.id)}&companyName=${encodeURIComponent(company.companyName)}`, returnPath))
          },
          {
            label: 'Delete company',
            icon: <Trash2 size={16} aria-hidden="true" />,
            onClick: (company) => setNotice(`Open ${company.companyName} to delete with confirmation.`)
          }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the CompanyDetailPanel React component and wires its local UI behavior.
function CompanyDetailPanel({ companyId, locationPath, onNavigate, onRequestError }: AdminPanelProps & Readonly<{ companyId: string }>) {
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
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/companies/${companyId}/edit`, detailPath))} disabled={!company}>
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
          <button className="secondary-button inline" type="button" onClick={() => onNavigate(withReturnTo(`/users?companyId=${company.id}&companyName=${encodeURIComponent(company.companyName)}`, detailPath))}>
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
function CompanyFormPanel({ companyId, locationPath, onNavigate, onRequestError }: AdminPanelProps & Readonly<{ companyId?: string }>) {
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

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const response = isEdit && companyId
        ? await updateCompany(companyId, { companyName })
        : await createCompany({ companyName });
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
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel={isEdit ? 'Update Company' : 'Create Company'} />
      </form>
    </section>
  );
}

// Function summary: Renders the UserListPanel React component and wires its local UI behavior.
function UserListPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const companyId = initialParams.get('companyId');
  const companyName = initialParams.get('companyName');
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [searchText, setSearchText] = useState(initialParams.get('q') ?? '');
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'email');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const columns = useMemo<DataGridColumn<UserListItem>[]>(() => [
    { key: 'name', header: 'Name', sortable: true, render: (user) => user.name || 'None' },
    { key: 'companyName', header: 'Company', sortable: true, render: (user) => user.companyName || 'None' },
    { key: 'email', header: 'Email', sortable: true, render: (user) => user.email },
    { key: 'phoneNumber', header: 'Mobile', sortable: true, render: (user) => user.phoneNumber || 'None' },
    { key: 'role', header: 'Role', sortable: true, render: (user) => user.role },
    { key: 'siteCount', header: 'Sites', sortable: true, align: 'end', render: (user) => user.role === companyUserRole ? user.siteCount : '' },
    { key: 'status', header: 'Status', sortable: true, render: (user) => userStatusLabel(user) }
  ], []);

  const query = useMemo<QueryUsersRequest>(() => ({
    companyId,
    searchText,
    page,
    pageSize: userPageSize,
    sort: sortKey,
    sortDir
  }), [companyId, page, searchText, sortDir, sortKey]);
  const returnPath = currentRoutePath(locationPath);
  const companiesBackPath = returnToOr(locationPath, '/companies');

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(null, '', buildUsersUrl({ companyId, companyName, searchText, page, sort: sortKey, sortDir }));
    setIsLoading(true);
    queryUsers(query, { signal: controller.signal })
      .then((response) => {
        setUsers(response.results);
        setTotal(response.total);
        setTotalPages(response.totalPages);
        setError(null);
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [companyId, companyName, onRequestError, page, query, searchText, sortDir, sortKey]);

  useEffect(() => {
    if (searchText.length < 2) {
      setSuggestions([]);
      return;
    }
    const controller = new AbortController();
    const handle = globalThis.setTimeout(() => {
      searchLookup('users', searchText, { companyId: companyId ?? undefined }, { signal: controller.signal })
        .then((response) => setSuggestions(response.results))
        .catch((err: Error) => {
          if (!isAbortError(err)) {
            setSuggestions([]);
          }
        });
    }, 180);
    return () => {
      controller.abort();
      globalThis.clearTimeout(handle);
    };
  }, [companyId, searchText]);

  async function handleAction(user: UserListItem, action: 'resend' | 'reset' | 'disable' | 'enable' | 'delete') {
    setNotice(null);
    setError(null);
    try {
      if (action === 'resend') {
        await resendUserConfirmation(user.id);
        setNotice('Confirmation link sent.');
      }
      if (action === 'reset') {
        await sendUserResetPasswordLink(user.id);
        setNotice('Password reset link sent.');
      }
      if (action === 'disable') {
        await disableUser(user.id);
        setNotice(`${user.email} disabled.`);
      }
      if (action === 'enable') {
        await enableUser(user.id);
        setNotice(`${user.email} enabled.`);
      }
      if (action === 'delete') {
        await deleteUser(user.id);
        setNotice(`${user.email} deleted.`);
      }
      const refreshed = await queryUsers(query);
      setUsers(refreshed.results);
      setTotal(refreshed.total);
      setTotalPages(refreshed.totalPages);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>{companyName ? `Company users / ${companyName}` : 'Administration'}</p>
          <h2>Users</h2>
        </div>
        <div className="button-row">
          {companyId && (
            <button className="secondary-button" type="button" onClick={() => onNavigate(companiesBackPath)}>
              Companies
            </button>
          )}
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(companyId ? `/users/new?companyId=${companyId}&companyName=${encodeURIComponent(companyName ?? '')}` : '/users/new', returnPath))}>
            <Plus size={17} aria-hidden="true" />
            <span>Create User</span>
          </button>
        </div>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input value={searchText} onChange={(event) => { setSearchText(event.target.value); setPage(1); }} placeholder="Search users" />
      </label>
      {suggestions.length > 0 && (
        <div className="suggestions" aria-label="User search suggestions">
          {suggestions.map((item) => (
            <button type="button" key={item} onClick={() => { setSearchText(item); setPage(1); }}>
              {item}
            </button>
          ))}
        </div>
      )}
      {notice && <Notice tone="success" message={notice} />}
      <DataGrid
        columns={columns}
        rows={users}
        getRowKey={(user) => user.id}
        emptyMessage="No users match the current search."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={userPageSize}
        total={total}
        totalPages={totalPages}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={(key, direction) => { setSortKey(key); setSortDir(direction); setPage(1); }}
        rowActions={[
          { label: 'View user', icon: <Eye size={16} aria-hidden="true" />, onClick: (user) => onNavigate(withReturnTo(`/users/${user.id}`, returnPath)) },
          { label: 'Edit user', icon: <Edit3 size={16} aria-hidden="true" />, disabled: (user) => !user.canEdit, onClick: (user) => onNavigate(withReturnTo(`/users/${user.id}/edit`, returnPath)) },
          { label: 'Resend confirmation', icon: <Mail size={16} aria-hidden="true" />, disabled: (user) => !user.canSendConfirmation, onClick: (user) => void handleAction(user, 'resend') },
          { label: 'Send password reset', icon: <KeyRound size={16} aria-hidden="true" />, disabled: (user) => !user.canSendPasswordReset, onClick: (user) => void handleAction(user, 'reset') },
          { label: 'Disable user', icon: <Lock size={16} aria-hidden="true" />, disabled: (user) => !user.canDisable, onClick: (user) => void handleAction(user, 'disable') },
          { label: 'Enable user', icon: <Unlock size={16} aria-hidden="true" />, disabled: (user) => !user.canEnable, onClick: (user) => void handleAction(user, 'enable') },
          { label: 'Delete user', icon: <Trash2 size={16} aria-hidden="true" />, disabled: (user) => !user.canDelete, onClick: (user) => void handleAction(user, 'delete') }
        ]}
      />
    </section>
  );
}

// Function summary: Renders the UserDetailPanel React component and wires its local UI behavior.
function UserDetailPanel({ userId, locationPath, onNavigate, onRequestError }: AdminPanelProps & Readonly<{ userId: string }>) {
  const [user, setUser] = useState<UserDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const backPath = returnToOr(locationPath, '/users');
  const detailPath = currentRoutePath(locationPath);

  useEffect(() => {
    getUser(userId)
      .then((response) => setUser(response.item ?? null))
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError, userId]);

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>User</p>
          <h2>{user?.email ?? 'Loading user'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            Back
          </button>
          <button className="secondary-button" type="button" onClick={() => onNavigate(withReturnTo(`/users/${userId}/edit`, detailPath))} disabled={!user?.canEdit}>
            <Edit3 size={17} aria-hidden="true" />
            <span>Edit</span>
          </button>
        </div>
      </div>
      {error && <Notice tone="error" message={error} />}
      {user && (
        <div className="detail-stack">
          <ReadOnlyRow label="Name" value={user.name || 'None'} />
          <ReadOnlyRow label="Email" value={user.email} />
          <ReadOnlyRow label="Mobile" value={user.phoneNumber || 'None'} />
          <ReadOnlyRow label="Role" value={user.role || 'None'} />
          <ReadOnlyRow label="Company" value={user.companyName || 'None'} />
          <ReadOnlyRow label="Company job title" value={user.companyRole || 'None'} />
          <ReadOnlyRow label="Status" value={userStatusLabel(user, 'Pending confirmation')} />
          <ReadOnlyRow label="Assigned sites" value={user.siteCount} />
        </div>
      )}
    </section>
  );
}

// Function summary: Renders the UserFormPanel React component and wires its local UI behavior.
function UserFormPanel({ userId, locationPath, onNavigate, onRequestError }: AdminPanelProps & Readonly<{ userId?: string }>) {
  const params = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const presetCompanyId = params.get('companyId') ?? '';
  const presetCompanyName = params.get('companyName') ?? '';
  const isEdit = Boolean(userId);
  const fallbackBackPath = presetCompanyId ? `/users?companyId=${presetCompanyId}&companyName=${encodeURIComponent(presetCompanyName)}` : '/users';
  const backPath = returnToOr(locationPath, fallbackBackPath);
  const [availableRoles, setAvailableRoles] = useState<OptionItem[]>([]);
  const [companies, setCompanies] = useState<OptionItem[]>([]);
  const [form, setForm] = useState<UserMutationRequest>({
    email: '',
    name: '',
    mobilePhone: '',
    role: presetCompanyId ? companyUserRole : '',
    companyId: presetCompanyId,
    companyRole: ''
  });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const optionsRequest = getUserOptions().then((options) => {
      setAvailableRoles(options.availableRoles);
      setCompanies(options.companies);
    });
    const detailRequest = userId
      ? getUser(userId).then((response) => {
        const item = response.item;
        if (item) {
          setForm({
            email: item.email,
            name: item.name ?? '',
            mobilePhone: item.phoneNumber ?? '',
            role: item.role,
            companyId: item.companyId ?? '',
            companyRole: item.companyRole ?? ''
          });
        }
      })
      : Promise.resolve();
    Promise.all([optionsRequest, detailRequest]).catch((err: Error) => {
      setError(err.message);
      onRequestError(err);
    });
  }, [onRequestError, userId]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setStatus(null);
    setError(null);
    try {
      const payload = {
        ...form,
        companyId: roleRequiresCompany(form.role) ? form.companyId || null : null,
        companyRole: form.role === companyUserRole ? form.companyRole : null
      };
      const response = isEdit && userId ? await updateUser(userId, payload) : await createUser(payload);
      const saved = response.item;
      setStatus(isEdit ? 'User updated.' : 'User created and invitation link sent.');
      if (saved?.id) {
        onNavigate(`/users/${saved.id}`);
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
          <p>{presetCompanyName ? `Company user / ${presetCompanyName}` : 'User'}</p>
          <h2>{isEdit ? 'Edit User' : 'Add User'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Name">
          <input value={form.name ?? ''} onChange={(event) => setForm({ ...form, name: event.target.value })} />
        </FormField>
        <FormField label="Email">
          <input value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} type="email" />
        </FormField>
        <FormField label="Mobile Phone Nr">
          <input value={form.mobilePhone ?? ''} onChange={(event) => setForm({ ...form, mobilePhone: event.target.value })} />
        </FormField>
        <FormField label="Role">
          <select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })} disabled={Boolean(presetCompanyId)}>
            <option value="">Select a Role</option>
            {availableRoles.map((role) => (
              <option value={role.value} key={role.value}>{role.label}</option>
            ))}
          </select>
        </FormField>
        {roleRequiresCompany(form.role) && (
          <>
            <FormField label="Company">
              <select value={form.companyId ?? ''} onChange={(event) => setForm({ ...form, companyId: event.target.value })} disabled={Boolean(presetCompanyId)}>
                <option value="">Select a Company</option>
                {companies.map((company) => (
                  <option value={company.value} key={company.value}>{company.label}</option>
                ))}
              </select>
            </FormField>
            {form.role === companyUserRole && (
              <FormField label="Company Job Title">
                <input value={form.companyRole ?? ''} onChange={(event) => setForm({ ...form, companyRole: event.target.value })} />
              </FormField>
            )}
          </>
        )}
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel={isEdit ? 'Update User' : 'Create User'} />
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

// Function summary: Renders the ReadOnlyRow React component and wires its local UI behavior.
function ReadOnlyRow({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="readonly-row">
      <span>{label}</span>
      <strong>{value}</strong>
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

// Function summary: Handles the parse users mode workflow for this module.
function parseUsersMode(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (path === '/users/new') {
    return { kind: 'create' as const };
  }
  const edit = /^\/users\/([^/]+)\/edit$/i.exec(path);
  if (edit) {
    return { kind: 'edit' as const, userId: edit[1] };
  }
  const detail = /^\/users\/([^/]+)$/i.exec(path);
  if (detail) {
    return { kind: 'detail' as const, userId: detail[1] };
  }
  return { kind: 'list' as const };
}

// Function summary: Handles the parse positive int workflow for this module.
function parsePositiveInt(value: string | null, fallback: number) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

// Function summary: Evaluates whether a user role requires a company assignment.
function roleRequiresCompany(role?: string | null) {
  return role === companyUserRole || role === installerRole;
}

// Function summary: Applies r status label to the current configuration.
function userStatusLabel(user: Pick<UserListItem | UserDetailResponse, 'isDisabled' | 'emailConfirmed'>, pendingLabel = 'Pending') {
  if (user.isDisabled) {
    return 'Disabled';
  }
  if (user.emailConfirmed) {
    return 'Active';
  }
  return pendingLabel;
}

// Function summary: Handles the normalize sort direction workflow for this module.
function normalizeSortDirection(value: string | null): SortDirection {
  return value?.toLowerCase() === 'descending' || value?.toLowerCase() === 'desc' ? 'Descending' : 'Ascending';
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

// Function summary: Builds users url data for callers.
function buildUsersUrl(options: {
  companyId?: string | null;
  companyName?: string | null;
  searchText: string;
  page: number;
  sort: string;
  sortDir: SortDirection;
}) {
  const params = new URLSearchParams();
  if (options.companyId) {
    params.set('companyId', options.companyId);
  }
  if (options.companyName) {
    params.set('companyName', options.companyName);
  }
  if (options.searchText) {
    params.set('q', options.searchText);
  }
  if (options.page > 1) {
    params.set('page', String(options.page));
  }
  if (options.sort !== 'email') {
    params.set('sort', options.sort);
  }
  if (options.sortDir !== 'Ascending') {
    params.set('sortDir', options.sortDir);
  }
  const query = params.toString();
  return query ? `/users?${query}` : '/users';
}

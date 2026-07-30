// File summary: Renders the user administration list, detail, and form panels.
// Major updates:
// - 2026-07-30 pending Split from AdminPanels.tsx so company and user admin panels live in separate modules.
// - 2026-06-26 pending Added cancellation for admin list and lookup requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for company and user detail/edit forms.
// - 2026-06-26 pending Allowed installer users to carry a company assignment for scoped installer access.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { Edit3, Eye, KeyRound, Lock, Mail, Plus, Save, Search, Trash2, Unlock } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  createUser,
  deleteUser,
  disableUser,
  enableUser,
  getUser,
  getUserOptions,
  isAbortError,
  queryUsers,
  resendUserConfirmation,
  searchLookup,
  sendUserResetPasswordLink,
  updateUser,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import type { DataGridColumn } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { ReadOnlyRow } from '../components/ReadOnlyRow';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { normalizeSortDirection, parsePositiveInt } from '../gridQuery';
import type { AdminPanelProps, LookupExecution, RequestExecution } from './adminShared';
import type {
  OptionItem,
  QueryUsersRequest,
  SortDirection,
  UserDetailResponse,
  UserListItem,
  UserMutationRequest,
} from '../dtos';

const userPageSize = 10;
const companyUserRole = 'CompanyUser';
const installerRole = 'RVTInstaller';

// Function summary: Renders the UsersPanel React component and wires its local UI behavior.
export function UsersPanel({ locationPath, onNavigate, onRequestError }: AdminPanelProps) {
  const mode = parseUsersMode(locationPath);
  if (mode.kind === 'create') {
    return <UserFormPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
  }
  if (mode.kind === 'edit') {
    return (
      <UserFormPanel
        userId={mode.userId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (mode.kind === 'detail') {
    return (
      <UserDetailPanel
        userId={mode.userId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  return <UserListPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={onRequestError} />;
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
  const [suggestionResult, setSuggestionResult] = useState<{
    execution: LookupExecution;
    results: string[];
  } | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);

  const columns = useMemo<DataGridColumn<UserListItem>[]>(
    () => [
      { key: 'name', header: 'Name', sortable: true, render: (user) => user.name || 'None' },
      { key: 'companyName', header: 'Company', sortable: true, render: (user) => user.companyName || 'None' },
      { key: 'email', header: 'Email', sortable: true, render: (user) => user.email },
      { key: 'phoneNumber', header: 'Mobile', sortable: true, render: (user) => user.phoneNumber || 'None' },
      { key: 'role', header: 'Role', sortable: true, render: (user) => user.role },
      {
        key: 'siteCount',
        header: 'Sites',
        sortable: true,
        align: 'end',
        render: (user) => (user.role === companyUserRole ? user.siteCount : ''),
      },
      { key: 'status', header: 'Status', sortable: true, render: (user) => userStatusLabel(user) },
    ],
    [],
  );

  const query = useMemo<QueryUsersRequest>(
    () => ({
      companyId,
      searchText,
      page,
      pageSize: userPageSize,
      sort: sortKey,
      sortDir,
    }),
    [companyId, page, searchText, sortDir, sortKey],
  );
  const execution = useMemo<RequestExecution<QueryUsersRequest>>(
    () => ({ query, generation: refreshVersion }),
    [query, refreshVersion],
  );
  const [completedExecution, setCompletedExecution] = useState<RequestExecution<QueryUsersRequest> | null>(null);
  const suggestionExecution = useMemo<LookupExecution | null>(
    () => (searchText.length >= 2 ? { query: searchText, companyId: companyId ?? undefined } : null),
    [companyId, searchText],
  );
  const isLoading = completedExecution !== execution;
  const suggestions = suggestionResult?.execution === suggestionExecution ? suggestionResult.results : [];
  const returnPath = currentRoutePath(locationPath);
  const companiesBackPath = returnToOr(locationPath, '/companies');

  useEffect(() => {
    const controller = new AbortController();
    globalThis.history.replaceState(
      null,
      '',
      buildUsersUrl({ companyId, companyName, searchText, page, sort: sortKey, sortDir }),
    );
    queryUsers(execution.query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setUsers(response.results);
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
  }, [companyId, companyName, execution, onRequestError, page, searchText, sortDir, sortKey]);

  useEffect(() => {
    if (!suggestionExecution) {
      return;
    }
    const controller = new AbortController();
    const handle = globalThis.setTimeout(() => {
      searchLookup(
        'users',
        suggestionExecution.query,
        { companyId: suggestionExecution.companyId },
        { signal: controller.signal },
      )
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

  const [confirmDeleteUser, setConfirmDeleteUser] = useState<UserListItem | null>(null);

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
      setRefreshVersion((current) => current + 1);
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
          <button
            className="secondary-button"
            type="button"
            onClick={() =>
              onNavigate(
                withReturnTo(
                  companyId
                    ? `/users/new?companyId=${companyId}&companyName=${encodeURIComponent(companyName ?? '')}`
                    : '/users/new',
                  returnPath,
                ),
              )
            }
          >
            <Plus size={17} aria-hidden="true" />
            <span>Create User</span>
          </button>
        </div>
      </div>
      <label className="search-box">
        <Search size={18} aria-hidden="true" />
        <input
          value={searchText}
          onChange={(event) => {
            setSearchText(event.target.value);
            setPage(1);
          }}
          placeholder="Search users"
        />
      </label>
      {suggestions.length > 0 && (
        <div className="suggestions" aria-label="User search suggestions">
          {suggestions.map((item) => (
            <button
              type="button"
              key={item}
              onClick={() => {
                setSearchText(item);
                setPage(1);
              }}
            >
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
        onSortChange={(key, direction) => {
          setSortKey(key);
          setSortDir(direction);
          setPage(1);
        }}
        rowActions={[
          {
            label: 'View user',
            icon: <Eye size={16} aria-hidden="true" />,
            onClick: (user) => onNavigate(withReturnTo(`/users/${user.id}`, returnPath)),
          },
          {
            label: 'Edit user',
            icon: <Edit3 size={16} aria-hidden="true" />,
            disabled: (user) => !user.canEdit,
            onClick: (user) => onNavigate(withReturnTo(`/users/${user.id}/edit`, returnPath)),
          },
          {
            label: 'Resend confirmation',
            icon: <Mail size={16} aria-hidden="true" />,
            disabled: (user) => !user.canSendConfirmation,
            onClick: (user) => void handleAction(user, 'resend'),
          },
          {
            label: 'Send password reset',
            icon: <KeyRound size={16} aria-hidden="true" />,
            disabled: (user) => !user.canSendPasswordReset,
            onClick: (user) => void handleAction(user, 'reset'),
          },
          {
            label: 'Disable user',
            icon: <Lock size={16} aria-hidden="true" />,
            disabled: (user) => !user.canDisable,
            onClick: (user) => void handleAction(user, 'disable'),
          },
          {
            label: 'Enable user',
            icon: <Unlock size={16} aria-hidden="true" />,
            disabled: (user) => !user.canEnable,
            onClick: (user) => void handleAction(user, 'enable'),
          },
          {
            label: 'Delete user',
            icon: <Trash2 size={16} aria-hidden="true" />,
            disabled: (user) => !user.canDelete,
            onClick: (user) => setConfirmDeleteUser(user),
          },
        ]}
      />
      <ConfirmDialog
        open={confirmDeleteUser !== null}
        title="Delete user"
        message={`Delete ${confirmDeleteUser?.email ?? 'this user'}?`}
        confirmLabel="Delete"
        onCancel={() => setConfirmDeleteUser(null)}
        onConfirm={() => {
          const user = confirmDeleteUser;
          setConfirmDeleteUser(null);
          if (user) {
            void handleAction(user, 'delete');
          }
        }}
      />
    </section>
  );
}

// Function summary: Renders the UserDetailPanel React component and wires its local UI behavior.
function UserDetailPanel({
  userId,
  locationPath,
  onNavigate,
  onRequestError,
}: AdminPanelProps & Readonly<{ userId: string }>) {
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
          <button
            className="secondary-button"
            type="button"
            onClick={() => onNavigate(withReturnTo(`/users/${userId}/edit`, detailPath))}
            disabled={!user?.canEdit}
          >
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
function UserFormPanel({
  userId,
  locationPath,
  onNavigate,
  onRequestError,
}: AdminPanelProps & Readonly<{ userId?: string }>) {
  const params = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const presetCompanyId = params.get('companyId') ?? '';
  const presetCompanyName = params.get('companyName') ?? '';
  const isEdit = Boolean(userId);
  const fallbackBackPath = presetCompanyId
    ? `/users?companyId=${presetCompanyId}&companyName=${encodeURIComponent(presetCompanyName)}`
    : '/users';
  const backPath = returnToOr(locationPath, fallbackBackPath);
  const [availableRoles, setAvailableRoles] = useState<OptionItem[]>([]);
  const [companies, setCompanies] = useState<OptionItem[]>([]);
  const [form, setForm] = useState<UserMutationRequest>({
    email: '',
    name: '',
    mobilePhone: '',
    role: presetCompanyId ? companyUserRole : '',
    companyId: presetCompanyId,
    companyRole: '',
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
              companyRole: item.companyRole ?? '',
            });
          }
        })
      : Promise.resolve();
    Promise.all([optionsRequest, detailRequest]).catch((err: Error) => {
      setError(err.message);
      onRequestError(err);
    });
  }, [onRequestError, userId]);

  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setStatus(null);
    setError(null);
    try {
      const payload = {
        ...form,
        companyId: roleRequiresCompany(form.role) ? form.companyId || null : null,
        companyRole: form.role === companyUserRole ? form.companyRole : null,
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
          <input
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
            type="email"
          />
        </FormField>
        <FormField label="Mobile Phone Nr">
          <input
            value={form.mobilePhone ?? ''}
            onChange={(event) => setForm({ ...form, mobilePhone: event.target.value })}
          />
        </FormField>
        <FormField label="Role">
          <select
            value={form.role}
            onChange={(event) => setForm({ ...form, role: event.target.value })}
            disabled={Boolean(presetCompanyId)}
          >
            <option value="">Select a Role</option>
            {availableRoles.map((role) => (
              <option value={role.value} key={role.value}>
                {role.label}
              </option>
            ))}
          </select>
        </FormField>
        {roleRequiresCompany(form.role) && (
          <>
            <FormField label="Company">
              <select
                value={form.companyId ?? ''}
                onChange={(event) => setForm({ ...form, companyId: event.target.value })}
                disabled={Boolean(presetCompanyId)}
              >
                <option value="">Select a Company</option>
                {companies.map((company) => (
                  <option value={company.value} key={company.value}>
                    {company.label}
                  </option>
                ))}
              </select>
            </FormField>
            {form.role === companyUserRole && (
              <FormField label="Company Job Title">
                <input
                  value={form.companyRole ?? ''}
                  onChange={(event) => setForm({ ...form, companyRole: event.target.value })}
                />
              </FormField>
            )}
          </>
        )}
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel={isEdit ? 'Update User' : 'Create User'}
        />
      </form>
    </section>
  );
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

// Function summary: Evaluates whether a user role requires a company assignment.
function roleRequiresCompany(role?: string | null) {
  return role === companyUserRole || role === installerRole;
}

// Function summary: Applies r status label to the current configuration.
function userStatusLabel(
  user: Pick<UserListItem | UserDetailResponse, 'isDisabled' | 'emailConfirmed'>,
  pendingLabel = 'Pending',
) {
  if (user.isDisabled) {
    return 'Disabled';
  }
  if (user.emailConfirmed) {
    return 'Active';
  }
  return pendingLabel;
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

// File summary: Renders the authenticated portal shell with navigation, shared error handling, and routed panels.
// Major updates:
// - 2026-07-30 pending Extracted from App.tsx during the shell/page split; replaced the "SPA migration" brand placeholder.

import {
  Activity,
  AlertCircle,
  LockKeyhole,
  LogOut,
  Mail,
  RefreshCcw,
  Save,
  ShieldCheck,
  UserRound,
} from 'lucide-react';
import { Suspense, lazy, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { SubmitEvent } from 'react';
import {
  changePassword,
  getHealth,
  getProfile,
  isAbortError,
  isForbidden,
  isUnauthorized,
  logout,
  updateProfile,
} from './api/client';
import { CompaniesPanel } from './admin/CompanyPanels';
import { UsersPanel } from './admin/UserPanels';
import { HelpAdminPanel } from './admin/HelpAdminPanel';
import { ContractsPanel } from './operations/ContractPanels';
import { SitesPanel } from './operations/SitePanels';
import { NotificationsPanel } from './operations/NotificationPanels';
import { DashboardPanel } from './operations/DashboardPanels';
import { HelpPanel } from './operations/HelpPanel';
import { FormField, Notice, SubmitButton } from './components/FormControls';
import { PasswordFields } from './auth/PasswordFields';
import {
  adminRoles,
  canAccessRoute,
  getUserRoles,
  hasAnyRole,
  navigationGroup,
  roleNames,
  routePath,
  visibleNavigation,
} from './routes';
import type { NavigationItem, ProtectedRoute } from './routes';
import type { AuthStateResponse, AuthUser, GetHealthResponse, ProfileResponse } from './dtos';

const LazyMapPanel = lazy(() =>
  import('./operations/MapCalendarPanels').then((module) => ({ default: module.MapPanel })),
);
const LazyCalendarPanel = lazy(() =>
  import('./operations/MapCalendarPanels').then((module) => ({ default: module.CalendarPanel })),
);
const LazyDataViewsPanel = lazy(() =>
  import('./operations/DataViewPanels').then((module) => ({ default: module.DataViewsPanel })),
);
const LazyMonitorsPanel = lazy(() =>
  import('./operations/MonitorPanels').then((module) => ({ default: module.MonitorsPanel })),
);
const LazyReportsPanel = lazy(() =>
  import('./operations/ReportPanels').then((module) => ({ default: module.ReportsPanel })),
);

type PortalShellProps = Readonly<{
  auth: AuthStateResponse;
  locationPath: string;
  route: ProtectedRoute;
  onAuthChanged: (auth: AuthStateResponse) => void;
  onNavigate: (path: string) => void;
}>;

// Function summary: Renders the PortalShell React component and wires its local UI behavior.
export function PortalShell({ auth, locationPath, route, onAuthChanged, onNavigate }: PortalShellProps) {
  const [health, setHealth] = useState<GetHealthResponse | null>(null);
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [shellError, setShellError] = useState<{
    route: ProtectedRoute;
    message: string;
  } | null>(null);
  const [adminExpanded, setAdminExpanded] = useState(true);
  const user = auth.user ?? null;
  const visibleItems = useMemo(() => visibleNavigation(user), [user]);
  const primaryItems = useMemo(() => navigationGroup(visibleItems, 'primary'), [visibleItems]);
  const adminItems = useMemo(() => navigationGroup(visibleItems, 'admin'), [visibleItems]);
  const secondaryItems = useMemo(() => navigationGroup(visibleItems, 'secondary'), [visibleItems]);
  const accountItems = useMemo(() => navigationGroup(visibleItems, 'account'), [visibleItems]);
  const contentRoute = canAccessRoute(route, user) ? route : 'access-denied';
  const isAdminRouteActive = adminItems.some((item) => item.route === contentRoute);
  const visibleShellError = shellError?.route === route ? shellError.message : null;

  // The current route is read through a ref so the error handlers stay
  // reference-stable across navigation; otherwise the health/profile fetch
  // effect below would refire on every route change.
  const routeRef = useRef(route);
  useEffect(() => {
    routeRef.current = route;
  }, [route]);

  const handleAccessRequestError = useCallback(
    (error: unknown) => {
      if (isUnauthorized(error)) {
        onAuthChanged({ isAuthenticated: false, user: null });
        onNavigate('/login');
        return true;
      }
      if (isForbidden(error)) {
        setShellError({
          route: routeRef.current,
          message: 'You do not have permission to use that part of the portal.',
        });
        if (routeRef.current !== 'access-denied') {
          onNavigate('/access-denied');
        }
        return true;
      }
      return false;
    },
    [onAuthChanged, onNavigate],
  );

  const handleRequestError = useCallback(
    (error: unknown) => {
      handleAccessRequestError(error);
    },
    [handleAccessRequestError],
  );

  const handleShellRequestError = useCallback(
    (error: unknown) => {
      if (handleAccessRequestError(error)) {
        return;
      }
      setShellError({ route: routeRef.current, message: (error as Error).message });
    },
    [handleAccessRequestError],
  );

  useEffect(() => {
    const controller = new AbortController();
    const reportError = (error: unknown) => {
      if (!isAbortError(error)) {
        handleShellRequestError(error);
      }
    };
    getHealth({ signal: controller.signal }).then(setHealth).catch(reportError);
    getProfile({ signal: controller.signal }).then(setProfile).catch(reportError);
    return () => controller.abort();
  }, [handleShellRequestError]);

  async function handleLogout() {
    const nextAuth = await logout().catch((error: unknown) => {
      if (isUnauthorized(error)) {
        return { isAuthenticated: false, user: null };
      }
      throw error;
    });
    onAuthChanged(nextAuth);
    onNavigate('/login');
  }

  return (
    <main className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <aside className="sidebar" aria-label="Primary">
        <div className="brand">
          <img src="/rvt-mark.svg" alt="" />
          <div>
            <strong>RVT Monitoring</strong>
            <span>RVT Group</span>
          </div>
        </div>
        <nav>
          <NavigationButtonList items={primaryItems} contentRoute={contentRoute} onNavigate={onNavigate} />
          {adminItems.length > 0 && (
            <div className="nav-group">
              <button
                className={isAdminRouteActive ? 'active nav-group-trigger' : 'nav-group-trigger'}
                type="button"
                aria-expanded={adminExpanded}
                onClick={() => setAdminExpanded((expanded) => !expanded)}
              >
                <ShieldCheck size={18} aria-hidden="true" />
                <span>Admin</span>
              </button>
              {adminExpanded && (
                <div className="nav-submenu">
                  <NavigationButtonList items={adminItems} contentRoute={contentRoute} onNavigate={onNavigate} />
                </div>
              )}
            </div>
          )}
          {secondaryItems.length > 0 && (
            <div className="nav-secondary" aria-label="Migrated tools">
              <span>Tools</span>
              <NavigationButtonList items={secondaryItems} contentRoute={contentRoute} onNavigate={onNavigate} />
            </div>
          )}
          <NavigationButtonList items={accountItems} contentRoute={contentRoute} onNavigate={onNavigate} />
        </nav>
      </aside>
      <section className="workspace" id="main-content" tabIndex={-1}>
        <header className="topbar">
          <div>
            <p>{roleSummary(user)}</p>
            <h1>{pageTitle(contentRoute)}</h1>
          </div>
          <div className="topbar-actions">
            <a className="icon-text-button" href="mailto:monitoring@rvtgroup.co.uk">
              <Mail size={18} aria-hidden="true" />
              <span>Contact</span>
            </a>
            <div className="status-pill">
              <ShieldCheck size={18} aria-hidden="true" />
              <span>{health ? `${health.status} / ${health.framework}` : 'checking API'}</span>
            </div>
            <button className="icon-text-button" type="button" onClick={handleLogout}>
              <LogOut size={18} aria-hidden="true" />
              <span>Sign out</span>
            </button>
          </div>
        </header>
        <section className="identity-strip" aria-label="Signed-in user">
          <UserRound size={20} aria-hidden="true" />
          <div>
            <strong>{user?.name || user?.email}</strong>
            <span>{getUserRoles(user).join(', ') || 'No role'}</span>
          </div>
        </section>
        {visibleShellError && <Notice tone="error" message={visibleShellError} />}
        {contentRoute === 'dashboard' && (
          <DashboardPanel auth={auth} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'maps' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading maps" />}>
            <LazyMapPanel locationPath={locationPath} onRequestError={handleRequestError} />
          </Suspense>
        )}
        {contentRoute === 'calendar' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading calendar" />}>
            <LazyCalendarPanel locationPath={locationPath} onRequestError={handleRequestError} />
          </Suspense>
        )}
        {contentRoute === 'data' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading data views" />}>
            <LazyDataViewsPanel locationPath={locationPath} onRequestError={handleRequestError} />
          </Suspense>
        )}
        {contentRoute === 'sites' && (
          <SitesPanel
            locationPath={locationPath}
            onNavigate={onNavigate}
            onRequestError={handleRequestError}
            canManage={hasAnyRole(user, adminRoles)}
            currentUserId={user?.id ?? null}
          />
        )}
        {contentRoute === 'contracts' && (
          <ContractsPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'monitors' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading monitors" />}>
            <LazyMonitorsPanel
              locationPath={locationPath}
              onNavigate={onNavigate}
              onRequestError={handleRequestError}
              canManage={hasAnyRole(user, adminRoles)}
              canUseInstallerTools={hasAnyRole(user, [...adminRoles, roleNames.installer])}
              installerOnly={hasAnyRole(user, [roleNames.installer]) && !hasAnyRole(user, adminRoles)}
            />
          </Suspense>
        )}
        {contentRoute === 'notifications' && (
          <NotificationsPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'reports' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading reports" />}>
            <LazyReportsPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
          </Suspense>
        )}
        {contentRoute === 'admin-help' && (
          <HelpAdminPanel onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'help' && (
          <HelpPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'companies' && (
          <CompaniesPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'users' && (
          <UsersPanel locationPath={locationPath} onNavigate={onNavigate} onRequestError={handleRequestError} />
        )}
        {contentRoute === 'profile' && <AccountPanel profile={profile} onProfileChanged={setProfile} />}
        {contentRoute === 'access-denied' && (
          <AccessDeniedPanel onNavigateHome={() => onNavigate(routePath('dashboard'))} />
        )}
        {contentRoute === 'not-found' && <NotFoundPanel onNavigateHome={() => onNavigate(routePath('dashboard'))} />}
      </section>
    </main>
  );
}

// Function summary: Renders a lightweight loading state for route chunks loaded after the core shell.
function RouteLoadingPanel({ label }: Readonly<{ label: string }>) {
  return (
    <section className="panel placeholder-panel" aria-live="polite">
      <RefreshCcw size={22} aria-hidden="true" />
      <p>{label}</p>
    </section>
  );
}

type NavigationButtonListProps = Readonly<{
  items: NavigationItem[];
  contentRoute: ProtectedRoute;
  onNavigate: (path: string) => void;
}>;

// Function summary: Renders a list of route buttons for the portal shell navigation.
function NavigationButtonList({ items, contentRoute, onNavigate }: NavigationButtonListProps) {
  return (
    <>
      {items.map((item) => {
        const Icon = item.icon;
        return (
          <button
            className={contentRoute === item.route ? 'active' : ''}
            type="button"
            key={item.name}
            aria-current={contentRoute === item.route ? 'page' : undefined}
            onClick={() => onNavigate(item.path)}
          >
            <Icon size={18} aria-hidden="true" />
            <span>{item.name}</span>
          </button>
        );
      })}
    </>
  );
}

// Function summary: Handles the role summary workflow for this module.
function roleSummary(user: AuthUser | null) {
  if (!user) {
    return 'Signed in';
  }
  if (hasAnyRole(user, adminRoles)) {
    return 'RVT administration';
  }
  if (hasAnyRole(user, [roleNames.installer])) {
    return 'Installer access';
  }
  if (hasAnyRole(user, [roleNames.companyUser])) {
    return 'Company access';
  }
  return 'Signed in';
}

// Function summary: Handles the page title workflow for this module.
function pageTitle(route: ProtectedRoute) {
  switch (route) {
    case 'maps':
      return 'Maps';
    case 'calendar':
      return 'Calendar';
    case 'data':
      return 'Data Views';
    case 'sites':
      return 'Sites';
    case 'contracts':
      return 'Contracts';
    case 'monitors':
      return 'Monitors';
    case 'notifications':
      return 'Notifications';
    case 'reports':
      return 'Reports';
    case 'admin-help':
      return 'Help/FAQ Management';
    case 'help':
      return 'Help';
    case 'companies':
      return 'Companies';
    case 'users':
      return 'Users';
    case 'profile':
      return 'Account Settings';
    case 'access-denied':
      return 'Access Denied';
    case 'not-found':
      return 'Page Not Found';
    default:
      return 'Operations Dashboard';
  }
}

type AccountPanelProps = Readonly<{
  profile: ProfileResponse | null;
  onProfileChanged: (profile: ProfileResponse) => void;
}>;

// Function summary: Renders the AccountPanel React component and wires its local UI behavior.
function AccountPanel({ profile, onProfileChanged }: AccountPanelProps) {
  const [profileStatus, setProfileStatus] = useState<string | null>(null);
  const profileFormKey = profile
    ? [profile.id, profile.email, profile.name, profile.mobilePhone, profile.companyRole].join('|')
    : 'profile-loading';

  function handleProfileChanged(updated: ProfileResponse) {
    onProfileChanged(updated);
    setProfileStatus('Your details have been updated.');
  }

  return (
    <section className="account-grid" aria-label="Account management">
      <ProfileForm
        key={profileFormKey}
        profile={profile}
        status={profileStatus}
        onProfileChanged={handleProfileChanged}
        onProfileFeedbackDismiss={() => setProfileStatus(null)}
      />
      <PasswordForm />
    </section>
  );
}

type AccessDeniedPanelProps = Readonly<{
  onNavigateHome?: () => void;
}>;

// Function summary: Renders the AccessDeniedPanel React component and wires its local UI behavior.
function AccessDeniedPanel({ onNavigateHome }: AccessDeniedPanelProps) {
  return (
    <section className="panel placeholder-panel" aria-label="Access denied">
      <AlertCircle size={24} aria-hidden="true" />
      <div>
        <h2>Permission required</h2>
        <p>Your role does not have permission to use this part of the portal.</p>
        {onNavigateHome && (
          <button className="secondary-button inline" type="button" onClick={onNavigateHome}>
            <Activity size={17} aria-hidden="true" />
            <span>Go to home</span>
          </button>
        )}
      </div>
    </section>
  );
}

type NotFoundPanelProps = Readonly<{
  onNavigateHome: () => void;
}>;

// Function summary: Renders the NotFoundPanel React component and wires its local UI behavior.
function NotFoundPanel({ onNavigateHome }: NotFoundPanelProps) {
  return (
    <section className="panel placeholder-panel" aria-label="Page not found">
      <AlertCircle size={24} aria-hidden="true" />
      <div>
        <h2>That portal route is not available.</h2>
        <p>The old MVC route has either been retired or folded into one of the migrated SPA sections.</p>
        <button className="secondary-button inline" type="button" onClick={onNavigateHome}>
          <Activity size={17} aria-hidden="true" />
          <span>Go to home</span>
        </button>
      </div>
    </section>
  );
}

type ProfileFormProps = Readonly<{
  profile: ProfileResponse | null;
  status: string | null;
  onProfileChanged: (profile: ProfileResponse) => void;
  onProfileFeedbackDismiss: () => void;
}>;

// Function summary: Renders the ProfileForm React component and wires its local UI behavior.
function ProfileForm({ profile, status, onProfileChanged, onProfileFeedbackDismiss }: ProfileFormProps) {
  const [email, setEmail] = useState(profile?.email ?? '');
  const [name, setName] = useState(profile?.name ?? '');
  const [mobilePhone, setMobilePhone] = useState(profile?.mobilePhone ?? '');
  const [companyRole, setCompanyRole] = useState(profile?.companyRole ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    onProfileFeedbackDismiss();
    setError(null);
    try {
      const updated = await updateProfile({ email, name, mobilePhone, companyRole });
      onProfileChanged(updated);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Account</p>
          <h2>Profile</h2>
        </div>
        <UserRound size={20} aria-hidden="true" />
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Email">
          <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
        </FormField>
        <FormField label="Name">
          <input value={name} onChange={(event) => setName(event.target.value)} />
        </FormField>
        <FormField label="Mobile">
          <input value={mobilePhone} onChange={(event) => setMobilePhone(event.target.value)} />
        </FormField>
        <FormField label="Company role">
          <input value={companyRole} onChange={(event) => setCompanyRole(event.target.value)} />
        </FormField>
        <div className="readonly-row">
          <span>Role</span>
          <strong>{profile?.role || 'None'}</strong>
        </div>
        <div className="readonly-row">
          <span>Company</span>
          <strong>{profile?.companyName || 'None'}</strong>
        </div>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          disabled={!profile}
          idleLabel="Save profile"
        />
      </form>
    </section>
  );
}

// Function summary: Renders the PasswordForm React component and wires its local UI behavior.
function PasswordForm() {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setStatus(null);
    setError(null);
    try {
      const response = await changePassword({ oldPassword, newPassword, confirmPassword });
      setStatus(response.message);
      setOldPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Security</p>
          <h2>Password</h2>
        </div>
        <LockKeyhole size={20} aria-hidden="true" />
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Current password">
          <input
            value={oldPassword}
            onChange={(event) => setOldPassword(event.target.value)}
            type="password"
            autoComplete="current-password"
          />
        </FormField>
        <PasswordFields
          password={newPassword}
          confirmPassword={confirmPassword}
          onPasswordChange={setNewPassword}
          onConfirmPasswordChange={setConfirmPassword}
        />
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel="Change password"
        />
      </form>
    </section>
  );
}

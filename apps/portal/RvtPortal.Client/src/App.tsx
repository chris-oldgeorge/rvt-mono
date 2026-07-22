// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-07-08 pending Lazy-loaded heavy route panels while keeping login, dashboard, and the shell in the initial bundle.
// - 2026-06-10 pending Added admin Help/FAQ management navigation and route.
// - 2026-06-10 pending Kept panel-scoped API errors out of the persistent shell banner.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-08 pending Grouped admin navigation for legacy menu parity.

import {
  Activity,
  AlertCircle,
  BarChart3,
  Bell,
  Building2,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  FileText,
  Gauge,
  HelpCircle,
  LockKeyhole,
  LogOut,
  Mail,
  Map as MapIcon,
  MapPinned,
  RefreshCcw,
  Save,
  ShieldCheck,
  UserRound,
  UsersRound,
  type LucideIcon
} from 'lucide-react';
import { Component, Suspense, lazy, useCallback, useEffect, useMemo, useState } from 'react';
import type { ErrorInfo, FormEvent, ReactNode } from 'react';
import {
  changePassword,
  confirmEmail,
  forgotPassword,
  getCurrentAuth,
  getHealth,
  getProfile,
  isForbidden,
  isUnauthorized,
  login,
  logout,
  resetPassword,
  setInitialPassword,
  updateProfile
} from './api/client';
import { CompaniesPanel, UsersPanel } from './admin/AdminPanels';
import { HelpAdminPanel } from './admin/HelpAdminPanel';
import { ContractsPanel, SitesPanel } from './operations/ContractSitePanels';
import { NotificationsPanel } from './operations/NotificationAlertPanels';
import { DashboardPanel } from './operations/DashboardPanels';
import { HelpPanel } from './operations/HelpPanel';
import { FormField, Notice, SubmitButton } from './components/FormControls';
import type {
  AuthStateResponse,
  AuthUser,
  ConfirmEmailResponse,
  GetHealthResponse,
  ProfileResponse
} from './dtos';

const LazyMapPanel = lazy(() => import('./operations/DashboardRoutePanels').then((module) => ({ default: module.MapPanel })));
const LazyCalendarPanel = lazy(() => import('./operations/DashboardRoutePanels').then((module) => ({ default: module.CalendarPanel })));
const LazyDataViewsPanel = lazy(() => import('./operations/DataViewPanels').then((module) => ({ default: module.DataViewsPanel })));
const LazyMonitorsPanel = lazy(() => import('./operations/MonitorPanels').then((module) => ({ default: module.MonitorsPanel })));
const LazyReportsPanel = lazy(() => import('./operations/ReportPanels').then((module) => ({ default: module.ReportsPanel })));

const roleNames = {
  masterAdmin: 'RVTMasterAdmin',
  admin: 'RVTAdmin',
  installer: 'RVTInstaller',
  companyUser: 'CompanyUser'
} as const;

const adminRoles = [roleNames.masterAdmin, roleNames.admin];

type PublicRoute = 'login' | 'forgot-password' | 'reset-password' | 'confirm-email' | 'privacy';
type ProtectedRoute =
  | 'dashboard'
  | 'maps'
  | 'calendar'
  | 'data'
  | 'sites'
  | 'contracts'
  | 'monitors'
  | 'notifications'
  | 'reports'
  | 'admin-help'
  | 'help'
  | 'companies'
  | 'users'
  | 'profile'
  | 'access-denied'
  | 'not-found';
type AppRoute = PublicRoute | ProtectedRoute;

type NavigationItem = {
  name: string;
  path: string;
  route: ProtectedRoute;
  icon: LucideIcon;
  state: string;
  roles?: string[];
  group?: 'primary' | 'admin' | 'secondary' | 'account';
};

const navigationItems: NavigationItem[] = [
  { name: 'Home', path: '/', route: 'dashboard', icon: Activity, state: 'Protected', group: 'primary' },
  {
    name: 'Maps',
    path: '/maps',
    route: 'maps',
    icon: MapIcon,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Calendar',
    path: '/calendar',
    route: 'calendar',
    icon: CalendarDays,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Data',
    path: '/data',
    route: 'data',
    icon: BarChart3,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Sites',
    path: '/sites',
    route: 'sites',
    icon: MapPinned,
    state: 'Migrated',
    group: 'primary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Contracts',
    path: '/contracts',
    route: 'contracts',
    icon: FileText,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles
  },
  {
    name: 'Monitors',
    path: '/monitors',
    route: 'monitors',
    icon: Gauge,
    state: 'Migrated',
    group: 'primary',
    roles: [...adminRoles, roleNames.companyUser, roleNames.installer]
  },
  {
    name: 'Notifications',
    path: '/notifications',
    route: 'notifications',
    icon: Bell,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Reports',
    path: '/reports',
    route: 'reports',
    icon: FileText,
    state: 'Migrated',
    group: 'admin',
    roles: adminRoles
  },
  {
    name: 'Help/FAQ',
    path: '/admin/help',
    route: 'admin-help',
    icon: HelpCircle,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles
  },
  {
    name: 'Help',
    path: '/help',
    route: 'help',
    icon: HelpCircle,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser]
  },
  {
    name: 'Companies',
    path: '/companies',
    route: 'companies',
    icon: Building2,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles
  },
  {
    name: 'Users',
    path: '/users',
    route: 'users',
    icon: UsersRound,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles
  },
  { name: 'Account', path: '/profile', route: 'profile', icon: UserRound, state: 'Self service', group: 'account' }
];

const exactRoutes: Readonly<Record<string, AppRoute>> = {
  '/': 'dashboard',
  '/forgot-password': 'forgot-password',
  '/reset-password': 'reset-password',
  '/confirm-email': 'confirm-email',
  '/privacy': 'privacy',
  '/login': 'login',
  '/profile': 'profile',
  '/access-denied': 'access-denied'
};
const prefixRoutes: ReadonlyArray<readonly [string, ProtectedRoute]> = [
  ['/admin/help', 'admin-help'],
  ['/maps', 'maps'],
  ['/calendar', 'calendar'],
  ['/data', 'data'],
  ['/sites', 'sites'],
  ['/contracts', 'contracts'],
  ['/monitors', 'monitors'],
  ['/notifications', 'notifications'],
  ['/reports', 'reports'],
  ['/help', 'help'],
  ['/companies', 'companies'],
  ['/users', 'users']
];

// Function summary: Retrieves route from location data for callers.
function getRouteFromLocation(): AppRoute {
  const path = globalThis.location.pathname.toLowerCase();
  return exactRoutes[path] ?? prefixRoutes.find(([prefix]) => path.startsWith(prefix))?.[1] ?? 'not-found';
}

// Function summary: Navigates the SPA to the requested route.
function navigate(path: string) {
  globalThis.history.pushState(null, '', path);
  globalThis.dispatchEvent(new globalThis.PopStateEvent('popstate'));
}

// Function summary: Retrieves user roles data for callers.
function getUserRoles(user?: AuthUser | null) {
  return user?.roles ?? [];
}

// Function summary: Evaluates any role for the current decision point.
function hasAnyRole(user: AuthUser | null | undefined, roles?: string[]) {
  if (!roles || roles.length === 0) {
    return true;
  }
  const userRoles = getUserRoles(user);
  return roles.some((role) => userRoles.includes(role));
}

// Function summary: Handles the visible navigation workflow for this module.
function visibleNavigation(user: AuthUser | null | undefined) {
  return navigationItems.filter((item) => hasAnyRole(user, item.roles));
}

// Function summary: Retrieves navigation group data for the legacy-compatible shell menu.
function navigationGroup(items: NavigationItem[], group: NonNullable<NavigationItem['group']>) {
  return items.filter((item) => item.group === group);
}

// Function summary: Evaluates access route for the current decision point.
function canAccessRoute(route: ProtectedRoute, user: AuthUser | null | undefined) {
  if (route === 'access-denied' || route === 'not-found') {
    return true;
  }
  const navigationItem = navigationItems.find((item) => item.route === route);
  return navigationItem ? hasAnyRole(user, navigationItem.roles) : false;
}

// Function summary: Handles the route path workflow for this module.
function routePath(route: ProtectedRoute) {
  if (route === 'dashboard') {
    return '/';
  }
  return `/${route}`;
}

// Function summary: Handles the current location path workflow for this module.
function currentLocationPath() {
  const pathname = globalThis.location.pathname.startsWith('/') ? globalThis.location.pathname : '/';
  const search = globalThis.location.search.startsWith('?') ? globalThis.location.search : '';
  return `${pathname}${search}`;
}

// Function summary: Renders the App React component and wires its local UI behavior.
export function App() {
  const [route, setRoute] = useState<AppRoute>(() => getRouteFromLocation());
  const [locationPath, setLocationPath] = useState(currentLocationPath);
  const [auth, setAuth] = useState<AuthStateResponse | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);

  useEffect(() => {
    // Function summary: Handles the on pop state workflow for this module.
    const onPopState = () => {
      setRoute(getRouteFromLocation());
      setLocationPath(currentLocationPath());
    };
    globalThis.addEventListener('popstate', onPopState);
    return () => globalThis.removeEventListener('popstate', onPopState);
  }, []);

  useEffect(() => {
    getCurrentAuth()
      .then((nextAuth) => {
        setAuth(nextAuth);
        setAuthError(null);
      })
      .catch(() => {
        setAuth({ isAuthenticated: false, user: null });
        setAuthError(null);
      });
  }, []);

  if (auth === null) {
    return <LoadingScreen />;
  }

  if (route === 'privacy') {
    return <PrivacyPage isAuthenticated={auth.isAuthenticated} onNavigate={navigate} />;
  }

  if (!auth.isAuthenticated) {
    if (route === 'not-found') {
      return <PublicNotFoundPage onNavigate={navigate} />;
    }
    if (route === 'forgot-password') {
      return <ForgotPasswordPage onNavigate={navigate} />;
    }
    if (route === 'reset-password') {
      return <ResetPasswordPage onNavigate={navigate} />;
    }
    if (route === 'confirm-email') {
      return <ConfirmEmailPage onAuthenticated={setAuth} onNavigate={navigate} />;
    }
    return <LoginPage authError={authError} onAuthenticated={setAuth} onNavigate={navigate} />;
  }

  const protectedRoute: ProtectedRoute =
    route === 'login' || route === 'forgot-password' || route === 'reset-password' || route === 'confirm-email'
      ? 'dashboard'
      : route;

  return (
    <AppErrorBoundary>
      <PortalShell auth={auth} locationPath={locationPath} route={protectedRoute} onAuthChanged={setAuth} onNavigate={navigate} />
    </AppErrorBoundary>
  );
}

type AppErrorBoundaryProps = Readonly<{
  children: ReactNode;
}>;

type AppErrorBoundaryState = Readonly<{
  hasError: boolean;
}>;

export class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  state: AppErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): AppErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('RVT Portal render failure', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return <ErrorBoundaryPanel />;
    }

    return this.props.children;
  }
}

// Function summary: Renders the ErrorBoundaryPanel React component and wires its local UI behavior.
function ErrorBoundaryPanel() {
  return (
    <main className="auth-shell">
      <section className="auth-panel compact">
        <AlertCircle size={24} aria-hidden="true" />
        <h1>Something went wrong</h1>
        <p>Refresh the page or return to the dashboard.</p>
        <button className="secondary-button" type="button" onClick={() => navigate('/')}>
          <Activity size={17} aria-hidden="true" />
          <span>Go to dashboard</span>
        </button>
      </section>
    </main>
  );
}

// Function summary: Renders the LoadingScreen React component and wires its local UI behavior.
function LoadingScreen() {
  return (
    <main className="auth-shell">
      <section className="auth-panel compact">
        <RefreshCcw size={22} aria-hidden="true" />
        <h1>RVT Monitoring</h1>
        <p>Checking session</p>
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

type PublicPageProps = Readonly<{
  onNavigate: (path: string) => void;
}>;

type PrivacyPageProps = PublicPageProps & Readonly<{
  isAuthenticated: boolean;
}>;

// Function summary: Renders the PrivacyPage React component and wires its local UI behavior.
function PrivacyPage({ isAuthenticated, onNavigate }: PrivacyPageProps) {
  return (
    <main className="auth-shell document-shell">
      <article className="document-panel" aria-label="Privacy policy">
        <div className="document-heading">
          <ShieldCheck size={28} aria-hidden="true" />
          <div>
            <p>RVT Group</p>
            <h1>Privacy Policy</h1>
          </div>
        </div>
        <p>
          Your privacy is important to RVT Group, and this privacy policy describes how we collect, use,
          disclose, transfer, and store your information. We will take all reasonable steps to ensure that
          your data is treated securely and in accordance with this privacy policy.
        </p>
        <p>RVT Group complies with its obligations under the General Data Protection Regulation by:</p>
        <ul>
          <li>keeping the data it holds up to date,</li>
          <li>storing and destroying it securely,</li>
          <li>not collecting or retaining excessive amounts of data,</li>
          <li>protecting personal data from loss, misuse, unauthorised access and disclosure, and</li>
          <li>ensuring that appropriate technical measures are in place to protect personal data.</li>
        </ul>
        <h2>The reason we collect and process information</h2>
        <p>
          We process personal information to enable us to promote our goods and services, to maintain our
          accounts and records, and to support and manage our staff. If you are a customer (or potential
          customer), information about you helps us to:
        </p>
        <ul>
          <li>provide you with information, products or services that you request from us or which we feel may interest you, and</li>
          <li>carry out our obligations arising from any contracts entered into between you and us.</li>
        </ul>
        <h2>The data we collect</h2>
        <p>
          We process information relevant to the above reasons/purposes. This may include:
        </p>
        <ul>
          <li>
            personal details such as name, work email address, mobile phone number, landline phone number,
            job title (but not information categorised as sensitive under data protection laws and regulations)
          </li>
          <li>employment details such as such as name, work email address, mobile phone number, landline phone number, job title</li>
          <li>goods or services provided (by us to you, or by you to us)</li>
        </ul>
        <p>
          We may collect certain information or data about you in the course of business, such as when you
          visit our website, contact us directly, or engage with our email bulletins (e.g. tracking whether
          you open these emails and what links you may click on). Such data could include your name, address,
          telephone number, email address and social media identifiers.
        </p>
        <p>
          If you telephone us, your call will not be recorded, but may be monitored by RVT personnel for the
          purposes of training to ensure that the highest possible quality of service is provided.
        </p>
        <p>
          Your information can be viewed by authorised people within RVT Group and relevant trustworthy
          external agencies supporting normal business operation, and may be used to:
        </p>
        <ul>
          <li>improve our website by monitoring how you use it</li>
          <li>gather feedback to improve our services and our email bulletins</li>
          <li>despatch goods to you</li>
          <li>respond to any feedback you send us</li>
        </ul>
        <p>
          RVT Group is dedicated to protecting people's health on and near construction and demolition sites
          against hazards such as dust, fumes and noise with temporary-environment control. Our lawful basis
          for collecting and processing your information is that it is of legitimate interest:
        </p>
        <ol>
          <li>to provide you with information that could help to protect the health of people affected by site activity,</li>
          <li>to provide you with information that will help to protect the environment at large from dust, fumes and noise, and</li>
          <li>to help us to grow our business.</li>
        </ol>
        <p>The above interests were identified as a result of a legitimate interests assessment that we have conducted.</p>
        <h2>Requests for additional information</h2>
        <p>
          Sometimes we will require you to provide further personal information. This may be if you are hiring
          equipment from us. Whenever we do this, we will tell you why we are collecting this information and
          how we will use it.
        </p>
        <h2>IP addresses</h2>
        <p>
          If you contact us online, we may monitor the type of device used by you. This may include specific
          identification, such as your IP address.
        </p>
        <h2>How we use this information</h2>
        <p>
          We do not sell customer's personal data to third parties and will only use your personal information
          to provide you with details of our own products, or services which we believe will be of interest
          to you. RVT Group use email addresses to personalise and improve digital marketing campaigns.
        </p>
        <h2>Where your information is stored</h2>
        <p>
          We store your information on secure servers within the UK (and so within the European Economic Area
          or EEA). The email platform we use, Mailchimp, is based outside the EEA and their servers hold your
          information in the United States. MailChimp participates in, and has certified its compliance with,
          the EU-U.S. Privacy Shield Framework.
        </p>
        <h2>Keeping your information secure</h2>
        <p>
          We have procedures and security features in place to keep your information secure once we receive it.
          For example:
        </p>
        <ul>
          <li>All log in users to our server are password protected and passwords have to be changed by default every 90 days.</li>
          <li>All employees and anyone accessing data have to sign a confidentiality agreement.</li>
          <li>Employee records can only be accessed by directors and approved senior personnel within the company</li>
          <li>Users have varying degrees of access depending on their position within the company</li>
          <li>Only directors and approved senior personnel can access the memory stick port on the computers</li>
        </ul>
        <p>
          Our company website uses HyperText Transfer Protocol Secure (HTPPS) coding on all its pages to help
          keep your information safe from hackers and, like most websites, uses cookies to enhance visitor
          experience (by, for example, enabling pages to load faster) and provide information about the
          aggregated statistics on how our website is used. The cookies we use do not obtain data that
          identifies individuals.
        </p>
        <h2>Disclosing your information</h2>
        <p>
          We may pass on your personal information if we have a legal obligation to do so, or if we have to
          enforce or apply our terms of use and other agreements. This may include disclosing your information
          to other companies and organisations in connection with fraud protection and credit risk reduction.
          We may also share your information with relevant external third parties for the following reasons:
        </p>
        <ul>
          <li>Marketing Agencies: To ensure our database is kept current and up to date, if additional resource is required.</li>
          <li>Marketing Platforms and Apps: To ensure that we are able to communicate seamlessly with you across multiple channels.</li>
          <li>Consultancy: To ensure the long-term sustainability of RVT Group by continually providing a relevant, high quality service offering.</li>
          <li>Logistics Agencies: To ensure our equipment is delivered on time.</li>
        </ul>
        <p>They will not pass on your information to other parties.</p>
        <h2>Third parties</h2>
        <p>We do not allow the information we hold about you to be used for advertising purposes or contact from third parties.</p>
        <h2>Cookies</h2>
        <p>
          By using our website you signify your agreement to our use of cookies. Our website uses cookies to
          store information on your computer. Some cookies on our site are essential, and the site won't work
          as expected without them. These cookies are set when you interact with the site by doing something
          that goes beyond clicking on simple links.
        </p>
        <p>
          We also use some non-essential 'performance' cookies, such as Google Analytics and Add This sharing
          feature, to anonymously track visitors or enhance your experience of the site. If you wish to
          restrict or block web browser cookies which are set on your device then you can do this through your
          browser settings. Click on the Help function within your browser to find out more.
        </p>
        <p>Performance cookies:</p>
        <ul>
          <li>These cookies are used to measure the performance of websites and see how websites are used.</li>
          <li>We use 'Performance' cookies to improve how the website works and measure our marketing activity.</li>
          <li>
            Information that is collected using these cookies is aggregated and anonymous and we are not able
            to identify individual users with these cookies.
          </li>
        </ul>
        <p>On www.rvtgroup.co.uk we may use 'Performance' cookies to:</p>
        <ul>
          <li>Provide us with aggregated statistics on how our website is used.</li>
          <li>
            Provide feedback to partners that one of our visitors also visited their website. This lets our
            partners improve their websites. We don't allow our partners to reuse this information for further
            advertising.
          </li>
          <li>Help us improve the website by measuring any errors that occur and also to improve the performance of the site.</li>
          <li>Test different designs of pages on our website.</li>
        </ul>
        <p>Cookies we have defined as 'Performance' cookies will NOT be used to remember any preferences you have set beyond the current visit.</p>
        <h2>Changes to our privacy and cookies policy</h2>
        <p>
          We may make changes and update our privacy and cookies policy from time to time and in accordance
          with updated legislation. Any such changes will be shown here as part of our privacy and cookies
          policy and will apply from the date that they are published. We are unable to contact you directly
          to inform you of these changes, other than in response to a specific request made to us as referred
          to above.
        </p>
        <h2>Your rights</h2>
        <p>
          You can find out what information we hold about you and ask us not to use any of the information we
          collect. If you wish to exercise this right, please send your request to our Marketing Department
          in writing by email to <a href="mailto:dataprotection@rvtgroup.co.uk">dataprotection@rvtgroup.co.uk</a>
        </p>
        <p>or by post to:</p>
        <p>
          RVT Group<br />
          Prospect House,<br />
          Riverside Way,<br />
          Dartford,<br />
          Kent,<br />
          DA1 5BS
        </p>
        <p>
          If you wish to unsubscribe from our email bulletins you can also do this by clicking on the
          unsubscribe link each one contains.
        </p>
        <h2>About Us</h2>
        <p>
          RVT Group is a limited company, registered company number 07907482, and registered office address
          Prospect House Riverside Industrial Estate, Riverside Way, Dartford, Kent, DA1 5BS.
        </p>
        <p>
          Please note that if you click on, or follow, any links from our site to external websites, our
          privacy policy will no longer apply. Please check the privacy policies of any such external site
          before submitting any personal data, as we cannot accept any responsibility or liability in relation
          to them.
        </p>
        <button className="secondary-button" type="button" onClick={() => onNavigate(isAuthenticated ? '/' : '/login')}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>{isAuthenticated ? 'Back to dashboard' : 'Back to sign in'}</span>
        </button>
      </article>
    </main>
  );
}

// Function summary: Renders the PublicNotFoundPage React component and wires its local UI behavior.
function PublicNotFoundPage({ onNavigate }: PublicPageProps) {
  return (
    <main className="auth-shell">
      <section className="auth-panel compact">
        <AlertCircle size={24} aria-hidden="true" />
        <h1>Page Not Found</h1>
        <p>That portal route is not available.</p>
        <button className="secondary-button" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
      </section>
    </main>
  );
}

type LoginPageProps = PublicPageProps & Readonly<{
  authError: string | null;
  onAuthenticated: (auth: AuthStateResponse) => void;
}>;

// Function summary: Renders the LoginPage React component and wires its local UI behavior.
function LoginPage({ authError, onAuthenticated, onNavigate }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(authError);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      const nextAuth = await login({ email, password, rememberMe: true });
      onAuthenticated(nextAuth);
      onNavigate('/');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="legacy-login-shell">
      <section className="legacy-login" aria-label="RVT portal sign in">
        <div className="legacy-promo">
          <img className="legacy-logo" src="/images/rvt.png" alt="RVT Group logo" />
          <img className="legacy-promo-image" src="/images/loginPromotion.png" alt="" />
        </div>
        <div className="legacy-form-column">
          <img className="legacy-logo narrow-logo" src="/images/rvt.png" alt="RVT Group logo" />
          <form className="legacy-login-form" onSubmit={handleSubmit}>
            <h1>Please sign in</h1>
            <label className="floating-field first">
              <input
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                type="email"
                placeholder="name@example.com"
                autoComplete="email"
              />
              <span>Email address</span>
            </label>
            <label className="floating-field last">
              <input
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                type="password"
                placeholder="Password"
                autoComplete="current-password"
              />
              <span>Password</span>
            </label>
            <div className="legacy-reset-row">
              <button
                className="legacy-text-link"
                type="button"
                onClick={() => onNavigate(email ? `/forgot-password?email=${encodeURIComponent(email)}` : '/forgot-password')}
              >
                Reset your password?
              </button>
            </div>
            {error && <div className="legacy-validation">{error}</div>}
            <button className="legacy-sign-in-button" disabled={isSubmitting} type="submit">
              {isSubmitting ? 'Signing in' : 'Sign in'}
            </button>
            <div className="legacy-contact">
              <h2>No account?</h2>
              <a href="mailto:monitoring@rvtgroup.co.uk" target="_blank" rel="noreferrer">
                Contact us
              </a>
              <span> to set you up on the platform.</span>
            </div>
            <p className="legacy-copyright">&copy; {new Date().getFullYear()} RVT Group Ltd.</p>
          </form>
        </div>
      </section>
    </main>
  );
}

// Function summary: Renders the ForgotPasswordPage React component and wires its local UI behavior.
function ForgotPasswordPage({ onNavigate }: PublicPageProps) {
  const [email, setEmail] = useState(() => new URLSearchParams(globalThis.location.search).get('email') ?? '');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setMessage(null);
    setError(null);
    try {
      const response = await forgotPassword({ email });
      setMessage(response.message);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <Mail size={24} aria-hidden="true" />
          <h1>Reset Password</h1>
        </div>
        <form className="form-grid" onSubmit={handleSubmit}>
          <label className="form-field">
            <span>Email</span>
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" />
          </label>
          {message && <Notice tone="success" message={message} />}
          {error && <Notice tone="error" message={error} />}
          <button className="primary-button" disabled={isSubmitting} type="submit">
            <Mail size={18} aria-hidden="true" />
            <span>{isSubmitting ? 'Sending' : 'Send reset link'}</span>
          </button>
        </form>
      </section>
    </main>
  );
}

// Function summary: Renders the ResetPasswordPage React component and wires its local UI behavior.
function ResetPasswordPage({ onNavigate }: PublicPageProps) {
  const code = new URLSearchParams(globalThis.location.search).get('code') ?? '';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(code ? null : 'A code must be supplied for password reset.');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setMessage(null);
    setError(null);
    try {
      const response = await resetPassword({ email, password, confirmPassword, code });
      setMessage(response.message);
      setPassword('');
      setConfirmPassword('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <LockKeyhole size={24} aria-hidden="true" />
          <h1>Choose Password</h1>
        </div>
        <form className="form-grid" onSubmit={handleSubmit}>
          <label className="form-field">
            <span>Email</span>
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" />
          </label>
          <PasswordFields
            password={password}
            confirmPassword={confirmPassword}
            onPasswordChange={setPassword}
            onConfirmPasswordChange={setConfirmPassword}
          />
          {message && <Notice tone="success" message={message} />}
          {error && <Notice tone="error" message={error} />}
          <button className="primary-button" disabled={isSubmitting || !code} type="submit">
            <Save size={18} aria-hidden="true" />
            <span>{isSubmitting ? 'Saving' : 'Save password'}</span>
          </button>
        </form>
      </section>
    </main>
  );
}

type ConfirmEmailPageProps = PublicPageProps & Readonly<{
  onAuthenticated: (auth: AuthStateResponse) => void;
}>;

// Function summary: Renders the ConfirmEmailPage React component and wires its local UI behavior.
function ConfirmEmailPage({ onAuthenticated, onNavigate }: ConfirmEmailPageProps) {
  const [confirmation, setConfirmation] = useState<ConfirmEmailResponse | null>(null);
  const [confirmationCode, setConfirmationCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState('Confirming email');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const params = new URLSearchParams(globalThis.location.search);
    const userId = params.get('userId') ?? '';
    const code = params.get('code') ?? '';
    if (!userId || !code) {
      setError('A user and confirmation code must be supplied.');
      setMessage('');
      return;
    }
    setConfirmationCode(code);
    confirmEmail(userId, code)
      .then((response) => {
        setConfirmation(response);
        setMessage('Email confirmed');
      })
      .catch((err: Error) => {
        setError(err.message);
        setMessage('');
      });
  }, []);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!confirmation) {
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const nextAuth = await setInitialPassword({
        userId: confirmation.userId,
        code: confirmationCode,
        newPassword: password,
        confirmPassword
      });
      onAuthenticated(nextAuth);
      onNavigate('/');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <CheckCircle2 size={24} aria-hidden="true" />
          <h1>Confirm Email</h1>
        </div>
        {message && <Notice tone="success" message={message} />}
        {error && <Notice tone="error" message={error} />}
        {confirmation && (
          <form className="form-grid" onSubmit={handleSubmit}>
            <label className="form-field">
              <span>Email</span>
              <input value={confirmation.email} readOnly />
            </label>
            <PasswordFields
              password={password}
              confirmPassword={confirmPassword}
              onPasswordChange={setPassword}
              onConfirmPasswordChange={setConfirmPassword}
            />
            <button className="primary-button" disabled={isSubmitting} type="submit">
              <Save size={18} aria-hidden="true" />
              <span>{isSubmitting ? 'Saving' : 'Set password'}</span>
            </button>
          </form>
        )}
      </section>
    </main>
  );
}

type PasswordFieldsProps = Readonly<{
  password: string;
  confirmPassword: string;
  onPasswordChange: (value: string) => void;
  onConfirmPasswordChange: (value: string) => void;
}>;

// Function summary: Renders the PasswordFields React component and wires its local UI behavior.
function PasswordFields({
  password,
  confirmPassword,
  onPasswordChange,
  onConfirmPasswordChange
}: PasswordFieldsProps) {
  return (
    <>
      <label className="form-field">
        <span>Password</span>
        <input
          value={password}
          onChange={(event) => onPasswordChange(event.target.value)}
          type="password"
          autoComplete="new-password"
        />
      </label>
      <label className="form-field">
        <span>Confirm password</span>
        <input
          value={confirmPassword}
          onChange={(event) => onConfirmPasswordChange(event.target.value)}
          type="password"
          autoComplete="new-password"
        />
      </label>
    </>
  );
}

type PortalShellProps = Readonly<{
  auth: AuthStateResponse;
  locationPath: string;
  route: ProtectedRoute;
  onAuthChanged: (auth: AuthStateResponse) => void;
  onNavigate: (path: string) => void;
}>;

// Function summary: Renders the PortalShell React component and wires its local UI behavior.
function PortalShell({ auth, locationPath, route, onAuthChanged, onNavigate }: PortalShellProps) {
  const [health, setHealth] = useState<GetHealthResponse | null>(null);
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [shellError, setShellError] = useState<string | null>(null);
  const [adminExpanded, setAdminExpanded] = useState(true);
  const user = auth.user ?? null;
  const visibleItems = useMemo(() => visibleNavigation(user), [user]);
  const primaryItems = useMemo(() => navigationGroup(visibleItems, 'primary'), [visibleItems]);
  const adminItems = useMemo(() => navigationGroup(visibleItems, 'admin'), [visibleItems]);
  const secondaryItems = useMemo(() => navigationGroup(visibleItems, 'secondary'), [visibleItems]);
  const accountItems = useMemo(() => navigationGroup(visibleItems, 'account'), [visibleItems]);
  const contentRoute = canAccessRoute(route, user) ? route : 'access-denied';
  const isAdminRouteActive = adminItems.some((item) => item.route === contentRoute);

  const handleAccessRequestError = useCallback((error: unknown) => {
    if (isUnauthorized(error)) {
      onAuthChanged({ isAuthenticated: false, user: null });
      onNavigate('/login');
      return true;
    }
    if (isForbidden(error)) {
      setShellError('You do not have permission to use that part of the portal.');
      if (route !== 'access-denied') {
        onNavigate('/access-denied');
      }
      return true;
    }
    return false;
  }, [onAuthChanged, onNavigate, route]);

  const handleRequestError = useCallback((error: unknown) => {
    handleAccessRequestError(error);
  }, [handleAccessRequestError]);

  const handleShellRequestError = useCallback((error: unknown) => {
    if (handleAccessRequestError(error)) {
      return;
    }
    setShellError((error as Error).message);
  }, [handleAccessRequestError]);

  useEffect(() => {
    setShellError(null);
  }, [route]);

  useEffect(() => {
    getHealth().then(setHealth).catch(handleShellRequestError);
    getProfile().then(setProfile).catch(handleShellRequestError);
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
            <span>SPA migration</span>
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
        {shellError && <Notice tone="error" message={shellError} />}
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
          <NotificationsPanel
            locationPath={locationPath}
            onNavigate={onNavigate}
            onRequestError={handleRequestError}
          />
        )}
        {contentRoute === 'reports' && (
          <Suspense fallback={<RouteLoadingPanel label="Loading reports" />}>
            <LazyReportsPanel
              locationPath={locationPath}
              onNavigate={onNavigate}
              onRequestError={handleRequestError}
            />
          </Suspense>
        )}
        {contentRoute === 'admin-help' && (
          <HelpAdminPanel
            onNavigate={onNavigate}
            onRequestError={handleRequestError}
          />
        )}
        {contentRoute === 'help' && (
          <HelpPanel
            locationPath={locationPath}
            onNavigate={onNavigate}
            onRequestError={handleRequestError}
          />
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
        {contentRoute === 'not-found' && (
          <NotFoundPanel onNavigateHome={() => onNavigate(routePath('dashboard'))} />
        )}
      </section>
    </main>
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
  return (
    <section className="account-grid" aria-label="Account management">
      <ProfileForm profile={profile} onProfileChanged={onProfileChanged} />
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
  onProfileChanged: (profile: ProfileResponse) => void;
}>;

// Function summary: Renders the ProfileForm React component and wires its local UI behavior.
function ProfileForm({ profile, onProfileChanged }: ProfileFormProps) {
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [mobilePhone, setMobilePhone] = useState('');
  const [companyRole, setCompanyRole] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!profile) {
      return;
    }
    setEmail(profile.email);
    setName(profile.name ?? '');
    setMobilePhone(profile.mobilePhone ?? '');
    setCompanyRole(profile.companyRole ?? '');
  }, [profile]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setStatus(null);
    setError(null);
    try {
      const updated = await updateProfile({ email, name, mobilePhone, companyRole });
      onProfileChanged(updated);
      setStatus('Your details have been updated.');
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

  async function handleSubmit(event: FormEvent) {
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
        <SubmitButton icon={<Save size={17} aria-hidden="true" />} isSubmitting={isSubmitting} idleLabel="Change password" />
      </form>
    </section>
  );
}

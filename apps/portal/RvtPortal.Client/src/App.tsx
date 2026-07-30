// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-07-30 pending Split PrivacyPage, the auth pages, PortalShell, and the route tables into their own modules.
// - 2026-07-08 pending Lazy-loaded heavy route panels while keeping login, dashboard, and the shell in the initial bundle.
// - 2026-06-10 pending Added admin Help/FAQ management navigation and route.
// - 2026-06-10 pending Kept panel-scoped API errors out of the persistent shell banner.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-08 pending Grouped admin navigation for legacy menu parity.

import { Activity, AlertCircle, ChevronLeft, RefreshCcw } from 'lucide-react';
import { Component, useEffect, useState } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import { getCurrentAuth } from './api/client';
import { ConfirmEmailPage, ForgotPasswordPage, LoginPage, ResetPasswordPage } from './auth/AuthPages';
import type { PublicPageProps } from './auth/AuthPages';
import { PortalShell } from './PortalShell';
import { PrivacyPage } from './PrivacyPage';
import { currentLocationPath, getRouteFromLocation, navigate } from './routes';
import type { AppRoute, ProtectedRoute } from './routes';
import type { AuthStateResponse } from './dtos';

// Function summary: Renders the App React component and wires its local UI behavior.
export function App() {
  const [route, setRoute] = useState<AppRoute>(() => getRouteFromLocation());
  const [locationPath, setLocationPath] = useState(currentLocationPath);
  const [auth, setAuth] = useState<AuthStateResponse | null>(null);
  // Carried in memory rather than a query string so the address never lands
  // in browser history, server logs, or referrer headers.
  const [forgotPasswordEmail, setForgotPasswordEmail] = useState('');

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
      })
      .catch(() => {
        setAuth({ isAuthenticated: false, user: null });
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
      return <ForgotPasswordPage initialEmail={forgotPasswordEmail} onNavigate={navigate} />;
    }
    if (route === 'reset-password') {
      return <ResetPasswordPage onNavigate={navigate} />;
    }
    if (route === 'confirm-email') {
      return <ConfirmEmailPage onAuthenticated={setAuth} onNavigate={navigate} />;
    }
    return (
      <LoginPage
        onAuthenticated={setAuth}
        onNavigate={navigate}
        onForgotPassword={(email) => {
          setForgotPasswordEmail(email);
          navigate('/forgot-password');
        }}
      />
    );
  }

  const protectedRoute: ProtectedRoute =
    route === 'login' || route === 'forgot-password' || route === 'reset-password' || route === 'confirm-email'
      ? 'dashboard'
      : route;

  return (
    <AppErrorBoundary>
      <PortalShell
        auth={auth}
        locationPath={locationPath}
        route={protectedRoute}
        onAuthChanged={setAuth}
        onNavigate={navigate}
      />
    </AppErrorBoundary>
  );
}

type AppErrorBoundaryProps = Readonly<{
  children: ReactNode;
}>;

type AppErrorBoundaryState = Readonly<{
  hasError: boolean;
}>;

class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
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

// Function summary: Renders the PublicNotFoundPage React component and wires its local UI behavior.
function PublicNotFoundPage({ onNavigate }: PublicPageProps) {
  return (
    <main className="auth-shell">
      <section className="auth-panel compact">
        <AlertCircle size={24} aria-hidden="true" />
        <h1>Page Not Found</h1>
        <p>That portal route is not available.</p>
        <button className="secondary-button" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
      </section>
    </main>
  );
}

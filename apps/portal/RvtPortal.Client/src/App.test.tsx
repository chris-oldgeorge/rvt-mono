// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-06-26 pending Covered stale monitor search response suppression after request cancellation.
// - 2026-06-26 pending Covered origin-aware Back navigation from list-driven edit forms.
// - 2026-06-26 pending Covered archived report-rule site warnings and save blocking.
// - 2026-06-25 pending Covered locale-aware Help admin content-type filter ordering.
// - 2026-06-25 pending Covered notification closed-note display parity.
// - 2026-06-26 pending Added RC-grade admin/report interaction scenarios for coverage quality.
// - 2026-06-10 pending Covered Admin Help/FAQ management route and publish workflow.
// - 2026-06-24 pending Covered reporting wizard, manual generation, and recipient grid affordances.
// - 2026-06-10 pending Covered clearing stale panel and contract-form errors after successful retries.
// - 2026-06-08 pending Covered admin unattached monitor removal workflow.
// - 2026-06-08 pending Added per-day site operating-hours and Help page coverage.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-08 pending Added legacy dashboard, navigation, and Create Site parity coverage.
// - 2026-06-08 pending Covered live dashboard Site Search and site row-action navigation.
// - 2026-06-09 pending Added monitor/site legacy-detail parity coverage.
// - 2026-06-09 pending Covered protected monitor picture URLs in SPA detail tests.
// - 2026-06-09 pending Covered latest average and battery monitor detail cards.
// - 2026-06-09 pending Covered embedded monitor/site detail map parity and metric source details.
// - 2026-06-24 pending Covered site customer-logo upload and delete controls.
// - 2026-06-25 pending Covered vibration alert-level peak-only display without averaging period.
// - 2026-07-08 pending Waited for routed panels to settle in navigation tests to remove React act warnings.

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App, AppErrorBoundary } from './App';

const adminUser = {
  id: 'admin-id',
  email: 'admin@rvt.test',
  name: 'Admin User',
  roles: ['RVTAdmin']
};

const installerUser = {
  id: 'installer-id',
  email: 'installer@rvt.test',
  name: 'Installer User',
  roles: ['RVTInstaller']
};

describe('App', () => {
  beforeEach(() => {
    globalThis.history.replaceState(null, '', '/login');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the login form for anonymous users', async () => {
    stubFetch({ auth: { isAuthenticated: false, user: null } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /please sign in/i })).toBeInTheDocument());
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
  });

  it('renders the privacy route for anonymous users', async () => {
    globalThis.history.replaceState(null, '', '/privacy');
    stubFetch({ auth: { isAuthenticated: false, user: null } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /privacy policy/i })).toBeInTheDocument());
    expect(screen.getByText(/your privacy is important to rvt group/i)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /please sign in/i })).not.toBeInTheDocument();
  });

  it('renders admin navigation for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /operations dashboard/i })).toBeInTheDocument());
    const navigation = within(screen.getByRole('navigation'));
    expect(navigation.getByRole('button', { name: /companies/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /users/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /maps/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /calendar/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /sites/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /contracts/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /monitors/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /data/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /reports/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /help\/faq/i })).toBeInTheDocument();
  });

  it('groups legacy admin navigation items under an Admin menu for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /operations dashboard/i })).toBeInTheDocument());
    const navigation = within(screen.getByRole('navigation'));
    expect(navigation.getByRole('button', { name: /^home$/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /^sites$/i })).toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /^monitors$/i })).toBeInTheDocument();
    const adminButton = navigation.getByRole('button', { name: /^admin$/i });
    const adminGroup = adminButton.closest('.nav-group');
    expect(adminGroup).not.toBeNull();
    const adminNavigation = within(adminGroup as HTMLElement);
    expect(adminNavigation.getByRole('button', { name: /^admin$/i })).toBeInTheDocument();
    expect(adminNavigation.getByRole('button', { name: /^companies$/i })).toBeInTheDocument();
    expect(adminNavigation.getByRole('button', { name: /^contracts$/i })).toBeInTheDocument();
    expect(adminNavigation.getByRole('button', { name: /^users$/i })).toBeInTheDocument();
    expect(adminNavigation.getByRole('button', { name: /^reports$/i })).toBeInTheDocument();
    expect(adminNavigation.getByRole('button', { name: /^help\/faq$/i })).toBeInTheDocument();
  });

  it('exposes keyboard skip navigation and marks the active route', async () => {
    globalThis.history.replaceState(null, '', '/reports');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^reports$/i })).toBeInTheDocument());
    expect(screen.getByRole('link', { name: /skip to content/i })).toHaveAttribute('href', '#main-content');
    expect(document.querySelector('#main-content')).toHaveAttribute('tabindex', '-1');
    expect(within(screen.getByRole('navigation')).getByRole('button', { name: /reports/i })).toHaveAttribute('aria-current', 'page');
  });

  it('shows only installer monitor navigation for installers', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: installerUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /operations dashboard/i })).toBeInTheDocument());
    const navigation = within(screen.getByRole('navigation'));
    expect(navigation.queryByRole('button', { name: /companies/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /users/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /sites/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /contracts/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /notifications/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /reports/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /maps/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /calendar/i })).not.toBeInTheDocument();
    expect(navigation.queryByRole('button', { name: /data/i })).not.toBeInTheDocument();
    expect(navigation.getByRole('button', { name: /monitors/i })).toBeInTheDocument();
  });

  it('renders the migrated dashboard summary for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /live monitor overview/i })).toBeInTheDocument());
    expect(screen.getByText('Open Alerts')).toBeInTheDocument();
    expect(screen.getByText('P8-DUST')).toBeInTheDocument();
  });

  it('renders the legacy dashboard site search section for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /site search/i })).toBeInTheDocument());
    expect(screen.getByPlaceholderText(/search sites/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^search$/i })).not.toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /contracts/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /site name/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /address/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /company name/i })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('RVT Test Site')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /view site/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /edit site/i })).toBeInTheDocument();
  });

  it('updates dashboard site search results live and opens the selected site', async () => {
    globalThis.history.replaceState(null, '', '/');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    const searchInput = await screen.findByPlaceholderText(/search sites/i);
    fireEvent.change(searchInput, { target: { value: 'acme' } });

    await waitFor(() => {
      expect(fetchedUrls().some((url) => url.pathname === '/api/sites' && url.searchParams.get('searchText') === 'acme')).toBe(true);
    });
    fireEvent.click(screen.getByRole('button', { name: /view site/i }));
    expect(globalThis.location.pathname).toBe('/sites/site-id');
    await waitFor(() => expect(screen.getByRole('heading', { name: /^RVT Test Site$/i })).toBeInTheDocument());
  });

  it('renders the maps route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/maps');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^maps$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getAllByText('P8-DUST (Dust)')[0]).toBeInTheDocument());
  });

  it('renders the calendar route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/calendar');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^calendar$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText(/P8-DUST \/ Dust/i)).toBeInTheDocument());
  });

  it('renders the contracts operations route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/contracts');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^contracts$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('RVT-C-001')).toBeInTheDocument());
  });

  it('lets admins narrow company search from live suggestions', async () => {
    globalThis.history.replaceState(null, '', '/companies');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname === '/api/lookups/companies') {
          return jsonResponse({ kind: 'companies', query: 'acme', take: 8, results: ['Acme Environmental'] });
        }

        if (url.pathname !== '/api/companies') {
          return undefined;
        }

        return jsonResponse({
          results: [
            {
              id: 'acme-company-id',
              companyName: url.searchParams.get('searchText') === 'Acme Environmental' ? 'Acme Environmental' : 'RVT Group',
              userCount: 3,
              sites: '2',
              contracts: '4'
            }
          ],
          total: 1,
          page: 1,
          pageSize: 10,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
          searchText: url.searchParams.get('searchText') ?? '',
          sort: 'companyName',
          sortDir: 'Ascending'
        });
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^companies$/i })).toBeInTheDocument());
    fireEvent.change(screen.getByPlaceholderText(/search companies/i), { target: { value: 'acme' } });
    await waitFor(() => expect(screen.getByRole('button', { name: /^Acme Environmental$/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /^Acme Environmental$/i }));

    await waitFor(() => expect(screen.getByText('Acme Environmental')).toBeInTheDocument());
    expect(fetchedUrls().some((url) => url.pathname === '/api/companies' && url.searchParams.get('searchText') === 'Acme Environmental')).toBe(true);
  });

  it('returns admin edit forms to the filtered list that opened them', async () => {
    globalThis.history.replaceState(null, '', '/companies?q=RVT&page=2&sort=userCount&sortDir=Descending');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname !== '/api/companies/company-id') {
          return undefined;
        }

        return jsonResponse({
          item: {
            id: 'company-id',
            companyName: 'RVT Group',
            userCount: 2,
            siteCount: 1,
            contractCount: 1,
            sites: '1',
            contracts: '1'
          }
        });
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^companies$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('button', { name: /edit company/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /edit company/i }));

    await waitFor(() => expect(screen.getByRole('heading', { name: /edit company/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /^back$/i }));

    await waitFor(() => {
      expect(globalThis.location.pathname).toBe('/companies');
      expect(screen.getByRole('button', { name: /edit company/i })).toBeInTheDocument();
    });
    expect(new URLSearchParams(globalThis.location.search).get('q')).toBe('RVT');
    expect(new URLSearchParams(globalThis.location.search).get('page')).toBe('2');
    expect(new URLSearchParams(globalThis.location.search).get('sort')).toBe('userCount');
    expect(new URLSearchParams(globalThis.location.search).get('sortDir')).toBe('Descending');
  });

  it('clears a contract option error after company options reload successfully', async () => {
    globalThis.history.replaceState(null, '', '/contracts/contract-id/edit');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname === '/api/contracts/contract-id') {
          return jsonResponse({
            item: {
              id: 'contract-id',
              contractNumber: 'RVT-C-001',
              onHireDate: '2026-01-01T00:00:00Z',
              offHireDate: null,
              companyId: 'company-id',
              companyName: 'RVT Group',
              siteId: 'site-id',
              siteName: 'RVT Test Site',
              companies: contractTestCompanies(),
              sites: [{ value: 'site-id', label: 'RVT Test Site' }]
            }
          });
        }

        if (url.pathname === '/api/contracts/options') {
          if (url.searchParams.get('companyId') === 'other-company-id') {
            return jsonResponse({ detail: 'Contract options unavailable' }, 500);
          }

          return jsonResponse({
            companies: contractTestCompanies(),
            sites: [{ value: 'site-id', label: 'RVT Test Site' }]
          });
        }

        return undefined;
      }
    });

    render(<App />);

    const companySelect = await screen.findByLabelText(/^company$/i);
    await waitFor(() => expect(screen.getByLabelText(/contract number/i)).toHaveValue('RVT-C-001'));
    fireEvent.change(companySelect, { target: { value: 'other-company-id' } });
    await waitFor(() => expect(screen.getByText(/contract options unavailable/i)).toBeInTheDocument());

    fireEvent.change(companySelect, { target: { value: 'company-id' } });

    await waitFor(() => expect(screen.queryByText(/contract options unavailable/i)).not.toBeInTheDocument());
  });

  it('renders the sites operations route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/sites');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^sites$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('RVT Test Site')).toBeInTheDocument());
  });

  it('renders per-day site hours on Site Detail and Edit Site', async () => {
    globalThis.history.replaceState(null, '', '/sites/site-id');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /^RVT Test Site$/i })).toBeInTheDocument());
    expect(screen.getByText('Monday Hours')).toBeInTheDocument();
    expect(screen.getByText('07:00 - 17:00')).toBeInTheDocument();
    expect(screen.getByText('Thursday Hours')).toBeInTheDocument();
    expect(screen.getAllByText('Closed')[0]).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /edit site/i })).toBeInTheDocument());
    expect(screen.getByLabelText(/monday start/i)).toHaveValue('07:00');
    expect(screen.getByLabelText(/wednesday start/i)).toHaveValue('09:00');
    expect(screen.getByLabelText(/thursday closed/i)).toBeChecked();
  });

  it('lets RVT admins replace and delete a site customer logo', async () => {
    globalThis.history.replaceState(null, '', '/sites/site-id/edit');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url, init) => {
        if (url.pathname !== '/api/sites/site-id/customer-logo') {
          return undefined;
        }

        if (init?.method === 'DELETE') {
          return jsonResponse({ item: { customerLogoUrl: null } });
        }

        return jsonResponse({ item: { customerLogoUrl: '/api/sites/site-id/customer-logo' } });
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /edit site/i })).toBeInTheDocument());
    expect(await screen.findByRole('img', { name: /customer logo/i })).toHaveAttribute('src', '/api/sites/site-id/customer-logo');

    const logoFile = new File(['logo'], 'customer-logo.png', { type: 'image/png' });
    fireEvent.change(screen.getByLabelText(/customer logo image/i), { target: { files: [logoFile] } });
    fireEvent.click(screen.getByRole('button', { name: /upload logo/i }));

    await waitFor(() => expect(screen.getByText(/customer logo updated/i)).toBeInTheDocument());
    expect(fetchedUrls().some((url) => url.pathname === '/api/sites/site-id/customer-logo')).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /delete logo/i }));

    await waitFor(() => expect(screen.getByText(/customer logo removed/i)).toBeInTheDocument());
    expect(screen.queryByRole('img', { name: /customer logo/i })).not.toBeInTheDocument();
  });

  it('renders site detail shortcuts for map data calendar and notifications', async () => {
    globalThis.history.replaceState(null, '', '/sites/site-id');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /^RVT Test Site$/i })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /open map/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /open calendar/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /open notifications/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/site detail map/i)).toBeInTheDocument();
    expect(screen.getAllByText(/MON-ONLINE \(Dust\)/i).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /open data/i }));
    await waitFor(() => {
      expect(globalThis.location.pathname).toBe('/data');
      expect(globalThis.location.search).toContain('deploymentId=deployment-id');
    });
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^data views$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Dust Monitor DATA-DUST')).toBeInTheDocument());
  });

  it('renders the Help page with FAQ articles and asset links', async () => {
    globalThis.history.replaceState(null, '', '/help');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^help$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('heading', { name: /data readings/i })).toBeInTheDocument());
    expect(screen.getByText('Dust reading definitions')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /open article/i })).toHaveAttribute('href', '/help/dust-reading-definitions');
    fireEvent.click(screen.getByRole('link', { name: /open article/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /dust reading definitions/i })).toBeInTheDocument());
    expect(screen.getByRole('link', { name: /dust monitoring guide/i })).toHaveAttribute('href', '/help-assets/data-readings/dust-guide.pdf');
  });

  it('lets RVT admins manage Help FAQ content from the Admin menu', async () => {
    globalThis.history.replaceState(null, '', '/admin/help');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /help\/faq management/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Draft FAQ')).toBeInTheDocument());
    const typeFilter = screen.getAllByLabelText(/^type$/i)[0] as HTMLSelectElement;
    expect(Array.from(typeFilter.options).map((option) => option.value)).toEqual([
      'All',
      'article',
      'Article',
      'Definition',
      'Document',
      'FAQ',
      'Video'
    ]);
    fireEvent.click(screen.getByRole('button', { name: /edit draft faq/i }));
    fireEvent.change(screen.getByLabelText(/^title$/i), { target: { value: 'Updated FAQ' } });
    fireEvent.click(screen.getByLabelText(/publish this content/i));
    fireEvent.click(screen.getByRole('button', { name: /save faq/i }));

    await waitFor(() => expect(screen.getByText(/help article updated/i)).toBeInTheDocument());
    expect(fetchedUrls().some((url) => url.pathname === '/api/help/admin')).toBe(true);
    expect(fetchedUrls().some((url) => url.pathname === '/api/help/admin/articles/help-article-id')).toBe(true);
  });

  it('shows legacy Create Site quick actions and company-dependent contracts', async () => {
    globalThis.history.replaceState(null, '', '/sites/new');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 2, name: /add site/i })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /add company/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add contract/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^contract$/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /add contract/i })).toBeDisabled();
  });

  it('renders the monitors operations route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/monitors');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^monitors$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('MON-ONLINE')).toBeInTheDocument());
  });

  it('keeps newer monitor search results when an older request resolves later', async () => {
    globalThis.history.replaceState(null, '', '/monitors');
    const staleRequest = deferredResponse();
    let heldInitialRequest = false;
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname !== '/api/monitors') {
          return undefined;
        }

        const searchText = url.searchParams.get('searchText') ?? '';
        if (!searchText && !heldInitialRequest) {
          heldInitialRequest = true;
          return staleRequest.promise;
        }

        if (searchText === 'fresh') {
          return jsonResponse(monitorPage(url, 'MON-FRESH'));
        }

        return undefined;
      }
    });

    render(<App />);

    await waitFor(() => expect(heldInitialRequest).toBe(true));
    fireEvent.change(await screen.findByPlaceholderText(/search monitors/i), { target: { value: 'fresh' } });

    await waitFor(() => expect(screen.getByText('MON-FRESH')).toBeInTheDocument());
    staleRequest.resolve(jsonResponse(monitorPage(new URL('/api/monitors', 'http://localhost'), 'MON-STALE')));

    await waitFor(() => expect(screen.queryByText('MON-STALE')).not.toBeInTheDocument());
    expect(screen.getByText('MON-FRESH')).toBeInTheDocument();
  });

  it('returns monitor edit forms to the filtered list that opened them', async () => {
    globalThis.history.replaceState(null, '', '/monitors?q=MON&page=2&sort=siteName&sortDir=Descending&state=online');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^monitors$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('button', { name: /edit monitor/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /edit monitor/i }));

    await waitFor(() => expect(screen.getByRole('heading', { name: /edit monitor/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /^back$/i }));

    await waitFor(() => {
      expect(globalThis.location.pathname).toBe('/monitors');
      expect(screen.getByRole('button', { name: /edit monitor/i })).toBeInTheDocument();
    });
    expect(new URLSearchParams(globalThis.location.search).get('q')).toBe('MON');
    expect(new URLSearchParams(globalThis.location.search).get('page')).toBe('2');
    expect(new URLSearchParams(globalThis.location.search).get('sort')).toBe('siteName');
    expect(new URLSearchParams(globalThis.location.search).get('sortDir')).toBe('Descending');
    expect(new URLSearchParams(globalThis.location.search).get('state')).toBe('online');
  });

  it('renders legacy monitor detail summaries with image and notification drill-through', async () => {
    globalThis.history.replaceState(null, '', '/monitors/monitor-id');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /^MON-ONLINE$/i })).toBeInTheDocument());
    expect(screen.getByText('Latest Reading')).toBeInTheDocument();
    expect(screen.getByText('Latest Breach')).toBeInTheDocument();
    expect(screen.getByText('Latest 15 Min Average')).toBeInTheDocument();
    expect(screen.getByText('Battery Charge')).toBeInTheDocument();
    expect(screen.getByText('Dust PM10 live reading')).toBeInTheDocument();
    expect(screen.getByLabelText(/monitor detail map/i)).toBeInTheDocument();
    expect(screen.getByText('48')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /monitor location/i })).toHaveAttribute('src', '/api/monitors/monitor-id/picture');
    expect(screen.getByRole('heading', { name: /deployment details/i })).toBeInTheDocument();
    expect(screen.getAllByText('MON-CON-001').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /view notification/i }));
    await waitFor(() => expect(globalThis.location.pathname).toBe('/notifications/notification-id'));
    await waitFor(() => expect(screen.getByRole('heading', { name: /PM10 > 50/i })).toBeInTheDocument());
  });

  it('hides the Average column for vibration alert levels', async () => {
    globalThis.history.replaceState(null, '', '/monitors/monitor-id/alert-levels');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /^alert levels$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Peak')).toBeInTheDocument());
    expect(screen.queryByText(/^Average$/)).not.toBeInTheDocument();
  });

  it('lets RVT admins open unattached monitors and archive a monitor with related data', async () => {
    globalThis.history.replaceState(null, '', '/monitors');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^monitors$/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /unattached/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /unattached monitors/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('SER-OLD-001')).toBeInTheDocument());
    expect(screen.getByText('Archive')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /remove monitor/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /archive monitor/i })).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText(/removal reason/i), { target: { value: 'Retired from fleet' } });
    fireEvent.click(screen.getByRole('button', { name: /^archive$/i }));

    await waitFor(() => {
      expect(fetchedUrls().some((url) => url.pathname === '/api/monitors/unattached')).toBe(true);
      expect(fetchedUrls().some((url) => url.pathname === '/api/monitors/11111111-1111-1111-1111-111111111111/unattached')).toBe(true);
    });
    await waitFor(() => expect(screen.getByText(/has been archived/i)).toBeInTheDocument());
  });

  it('renders the notifications operations route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/notifications');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^notifications$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('PM10 > 50')).toBeInTheDocument());
  });

  it('shows closed notification notes when the current notification rows include one', async () => {
    globalThis.history.replaceState(null, '', '/notifications?state=all');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname !== '/api/notifications') {
          return undefined;
        }

        return jsonResponse({
          results: [
            {
              id: 'notification-id',
              monitorId: 'monitor-id',
              deploymentId: 'deployment-id',
              fleetNumber: 'MON-ONLINE',
              serialId: 'SER-P5',
              typeOfMonitor: 'Dust',
              alertType: 'Alert',
              alertField: 'PM10',
              limitOn: 50,
              level: 61,
              averagingPeriod: 900,
              notificationTime: '2026-01-02T10:00:00Z',
              closedTime: '2026-01-02T11:00:00Z',
              closedByUser: 'Admin User',
              closedNote: 'Investigated from SPA',
              contractId: 'contract-id',
              contractNumber: 'RVT-C-001',
              siteId: 'site-id',
              siteName: 'RVT Test Site',
              companyId: 'company-id',
              companyName: 'RVT Group',
              limitName: 'PM10 > 50',
              alertStatus: 'Closed',
              canClose: false
            }
          ],
          total: 1,
          page: 1,
          pageSize: 10,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
          searchText: '',
          sort: 'notificationTime',
          sortDir: 'Descending',
          state: 'all',
          isScopedToCurrentUser: false,
          canClose: true
        });
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^notifications$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('columnheader', { name: /closed note/i })).toBeInTheDocument());
    expect(screen.getByText('Investigated from SPA')).toBeInTheDocument();
  });

  it('renders the reports operations route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/reports');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^reports$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Weekly Compliance')).toBeInTheDocument());
  });

  it('returns report-rule editing to the reports list that opened it', async () => {
    globalThis.history.replaceState(null, '', '/reports?q=weekly&page=2&sort=siteName&sortDir=Descending');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^reports$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('button', { name: /edit report rule/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /edit report rule/i }));

    await waitFor(() => expect(screen.getByRole('heading', { name: /edit rule/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /^back$/i }));

    await waitFor(() => {
      expect(globalThis.location.pathname).toBe('/reports');
      expect(screen.getByRole('button', { name: /edit report rule/i })).toBeInTheDocument();
    });
    expect(new URLSearchParams(globalThis.location.search).get('q')).toBe('weekly');
    expect(new URLSearchParams(globalThis.location.search).get('page')).toBe('2');
    expect(new URLSearchParams(globalThis.location.search).get('sort')).toBe('siteName');
    expect(new URLSearchParams(globalThis.location.search).get('sortDir')).toBe('Descending');
  });

  it('clears a report load error after the report grid retries successfully', async () => {
    globalThis.history.replaceState(null, '', '/reports');
    let reportRequestCount = 0;
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname !== '/api/reports') {
          return undefined;
        }

        reportRequestCount += 1;
        if (reportRequestCount === 1) {
          return jsonResponse({ detail: 'Reports unavailable' }, 500);
        }

        return jsonResponse({
          results: [
            {
              id: 'retried-report-id',
              siteId: 'site-id',
              siteName: 'RVT Test Site',
              reportDate: '2026-01-14T08:00:00Z',
              reportFrom: '2026-01-08T00:00:00Z',
              reportTo: '2026-01-14T00:00:00Z',
              reportLink: 'https://reports.rvt.test/retried-weekly.pdf',
              reportRuleId: 'report-rule-id',
              frequency: 2,
              frequencyLabel: 'Weekly',
              reportName: 'Retried Compliance',
              contracts: 'RVT-C-001'
            }
          ],
          total: 1,
          page: 1,
          pageSize: 10,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
          searchText: url.searchParams.get('searchText') ?? '',
          sort: 'reportDate',
          sortDir: 'Descending'
        });
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getAllByText(/reports unavailable/i).length).toBeGreaterThan(0));
    fireEvent.change(screen.getByPlaceholderText(/search reports/i), { target: { value: 'retried' } });

    await waitFor(() => expect(screen.getByText('Retried Compliance')).toBeInTheDocument());
    expect(screen.queryByText(/reports unavailable/i)).not.toBeInTheDocument();
  });

  it('supports report-rule wizard setup and manual generation from the edit view', async () => {
    globalThis.history.replaceState(null, '', '/reports/rules/new');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /add rule/i })).toBeInTheDocument());
    expect(screen.getByText(/step 1/i)).toBeInTheDocument();
    expect(screen.getByText(/schedule/i)).toBeInTheDocument();
    expect(screen.getByText(/recipients/i)).toBeInTheDocument();

    globalThis.history.pushState(null, '', '/reports/rules/report-rule-id');
    fireEvent(window, new PopStateEvent('popstate'));

    await waitFor(() => expect(screen.getByRole('heading', { name: /edit rule/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /generate now/i }));

    await waitFor(() => expect(screen.getByText(/manual generation queued/i)).toBeInTheDocument());
    expect(fetchedUrls().some((url) => url.pathname === '/api/report-rules/report-rule-id/generation-requests')).toBe(true);
  });

  it('offers daily report rules without weekly or monthly schedule fields', async () => {
    globalThis.history.replaceState(null, '', '/reports/rules/new');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /add rule/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('option', { name: /daily/i })).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/frequency/i), { target: { value: '1' } });

    expect(screen.queryByLabelText(/day of week/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/day of month/i)).not.toBeInTheDocument();
  });

  it('shows archived report-rule sites as disabled and blocks saving until an active site is selected', async () => {
    globalThis.history.replaceState(null, '', '/reports/rules/report-rule-id');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname !== '/api/report-rules/report-rule-id') {
          return undefined;
        }

        return jsonResponse({
          item: {
            id: 'report-rule-id',
            siteId: 'archived-site-id',
            siteName: 'Archived Report Site',
            frequency: 2,
            frequencyLabel: 'Weekly',
            dayOfWeek: 1,
            dayOfMonth: null,
            reportName: 'Archived Site Compliance',
            lastGenerated: null,
            canManage: true,
            assignedUserCount: 1,
            sites: [
              { value: 'active-site-id', label: 'Active Report Site' },
              { value: 'archived-site-id', label: 'Archived Report Site', disabled: true }
            ],
            frequencies: [
              { value: '1', label: 'Daily' },
              { value: '2', label: 'Weekly' },
              { value: '3', label: 'Monthly' }
            ],
            daysOfWeek: [{ value: '1', label: 'Monday' }],
            alertRuleGuidelines: []
          }
        });
      }
    });

    render(<App />);

    const siteSelect = await screen.findByLabelText(/^site$/i);
    await waitFor(() => expect(within(siteSelect).getByRole('option', { name: /archived report site/i })).toBeInTheDocument());
    const archivedOption = within(siteSelect).getByRole('option', { name: /archived report site/i });

    expect(siteSelect).toHaveValue('archived-site-id');
    expect(archivedOption).toBeDisabled();
    expect(screen.getByText(/this site is archived/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save report rule/i })).toBeDisabled();

    fireEvent.change(siteSelect, { target: { value: 'active-site-id' } });

    expect(screen.queryByText(/this site is archived/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save report rule/i })).not.toBeDisabled();
  });

  it('renders report recipient assignment as searchable paged grids', async () => {
    globalThis.history.replaceState(null, '', '/reports/rules/report-rule-id/users');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /rvt test site/i })).toBeInTheDocument());
    expect(screen.getByRole('heading', { name: /available users/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /assigned users/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/search available users/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/search assigned users/i)).toBeInTheDocument();
    expect(screen.getByText('available@rvt.test')).toBeInTheDocument();
    expect(screen.getByText('assigned@rvt.test')).toBeInTheDocument();
  });

  it('lets admins search report recipients without losing assigned recipients', async () => {
    globalThis.history.replaceState(null, '', '/reports/rules/report-rule-id/users');
    stubFetch({
      auth: { isAuthenticated: true, user: adminUser },
      routeOverride: (url) => {
        if (url.pathname === '/api/report-rules/report-rule-id/available-users') {
          const search = url.searchParams.get('searchText') ?? '';
          return jsonResponse(reportUserPage(url, [
            reportUser('filtered-available-user-id', 'filtered.available@rvt.test', search ? 'Filtered Available' : 'Available User')
          ]));
        }

        return undefined;
      }
    });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /rvt test site/i })).toBeInTheDocument());
    fireEvent.change(screen.getByPlaceholderText(/search available users/i), { target: { value: 'filtered' } });

    await waitFor(() => expect(screen.getByText('filtered.available@rvt.test')).toBeInTheDocument());
    expect(screen.getByText('assigned@rvt.test')).toBeInTheDocument();
    expect(fetchedUrls().some((url) =>
      url.pathname === '/api/report-rules/report-rule-id/available-users' &&
      url.searchParams.get('searchText') === 'filtered')).toBe(true);
  });

  it('renders the data views route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/data?deploymentId=deployment-id');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^data views$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Dust Monitor DATA-DUST')).toBeInTheDocument());
    expect(screen.getByRole('columnheader', { name: /pm10/i })).toBeInTheDocument();
    expect(screen.getByText('42.3')).toBeInTheDocument();
  });

  it('renders the users admin route for RVT admin users', async () => {
    globalThis.history.replaceState(null, '', '/users');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser } });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: /^users$/i })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('company.user@rvt.test')).toBeInTheDocument());
  });

  it('blocks direct admin route access for installers without loading company data', async () => {
    globalThis.history.replaceState(null, '', '/companies');
    const companyRequestCount = { value: 0 };
    stubFetch({ auth: { isAuthenticated: true, user: installerUser }, companyRequestCount });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /access denied/i })).toBeInTheDocument());
    expect(companyRequestCount.value).toBe(0);
  });

  it('renders not found for authenticated unknown routes without loading dashboard data', async () => {
    globalThis.history.replaceState(null, '', '/legacy/demo-route');
    const dashboardRequestCount = { value: 0 };
    stubFetch({ auth: { isAuthenticated: true, user: adminUser }, dashboardRequestCount });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /page not found/i })).toBeInTheDocument());
    expect(screen.getByText(/that portal route is not available/i)).toBeInTheDocument();
    expect(dashboardRequestCount.value).toBe(0);
  });

  it('renders a stable error boundary without exposing exception details', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <AppErrorBoundary>
        <ThrowingPanel />
      </AppErrorBoundary>
    );

    expect(screen.getByRole('heading', { name: /something went wrong/i })).toBeInTheDocument();
    expect(screen.getByText(/refresh the page or return to the dashboard/i)).toBeInTheDocument();
    expect(screen.queryByText(/phase 10 render failure/i)).not.toBeInTheDocument();

    consoleError.mockRestore();
  });

  it('redirects to login when an authenticated API call returns 401', async () => {
    globalThis.history.replaceState(null, '', '/profile');
    stubFetch({ auth: { isAuthenticated: true, user: adminUser }, profileStatus: 401 });

    render(<App />);

    await waitFor(() => expect(screen.getByRole('heading', { name: /please sign in/i })).toBeInTheDocument());
  });
});

type StubFetchOptions = {
  auth: unknown;
  profileStatus?: number;
  companyRequestCount?: { value: number };
  dashboardRequestCount?: { value: number };
  routeOverride?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined;
};

// Function summary: Retrieves fetch call URLs issued by tests for request-behavior assertions.
function fetchedUrls() {
  const fetchMock = globalThis.fetch as unknown as { mock: { calls: Array<[RequestInfo | URL]> } };

  return fetchMock.mock.calls.map(([input]) => new URL(input.toString(), 'http://localhost'));
}

// Function summary: Handles the stub fetch workflow for this module.
function stubFetch({ auth, profileStatus = 200, companyRequestCount, dashboardRequestCount, routeOverride }: StubFetchOptions) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input.toString(), 'http://localhost');

    if (url.pathname === '/api/auth/me') {
      return jsonResponse(auth);
    }

    if (url.pathname === '/api/health') {
      return jsonResponse({ status: 'Healthy', framework: 'Testing', serverTimeUtc: new Date(0).toISOString() });
    }

    if (url.pathname === '/api/auth/profile') {
      if (profileStatus !== 200) {
        return jsonResponse({ title: 'Unauthorized', detail: 'Session expired' }, profileStatus);
      }
      return jsonResponse({
        id: 'profile-id',
        email: 'admin@rvt.test',
        name: 'Admin User',
        role: 'RVTAdmin',
        companyRole: 'Operations',
        companyName: null
      });
    }

    const overriddenResponse = routeOverride?.(url, init);
    if (overriddenResponse) {
      return overriddenResponse;
    }

    if (url.pathname === '/api/dashboard/summary') {
      if (dashboardRequestCount) {
        dashboardRequestCount.value += 1;
      }
      return jsonResponse({
        role: 'RVTAdmin',
        monitorCounts: {
          new: 1,
          notUsed: 1,
          online: 2,
          offline: 1,
          assigned: 3
        },
        openAlerts: 1,
        openCautions: 1,
        sites: [{ value: 'site-id', label: 'RVT Test Site' }],
        calendarDeployments: [{ value: 'deployment-id', label: 'P8-DUST - RVT Test Site' }],
        recentNotifications: [
          {
            id: 'dashboard-notification-id',
            monitorId: 'monitor-id',
            fleetNumber: 'P8-DUST',
            serialId: 'SER-P8',
            alertType: 'Alert',
            alertField: 'pm10',
            level: 61,
            notificationTime: '2026-05-24T10:00:00Z',
            siteName: 'RVT Test Site'
          }
        ]
      });
    }

    if (url.pathname === '/api/data/deployments/deployment-id/grid') {
      return jsonResponse({
        deploymentId: 'deployment-id',
        monitorId: 'monitor-id',
        monitorName: 'Dust Monitor DATA-DUST',
        monitorType: 'Dust',
        minDate: '2026-05-24T09:00:00Z',
        maxDate: '2026-05-24T10:00:00Z',
        fromDate: '2026-05-24T09:00:00Z',
        toDate: '2026-05-24T10:00:00Z',
        fromDateChanged: false,
        toDateChanged: false,
        maxDuration: null,
        filterOption: 'raw',
        filterOptions: [{ value: 'raw', label: 'Raw' }],
        columns: [
          { key: 'sampleTime', label: 'Sample Time' },
          { key: 'pm10', label: 'PM10' }
        ],
        rows: [
          {
            sampleTime: '2026-05-24T10:00:00Z',
            values: { pm10: 42.3 }
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        sort: 'sampleTime',
        sortDir: 'Descending'
      });
    }

    if (url.pathname === '/api/data/deployments/deployment-id/graph') {
      return jsonResponse({
        deploymentId: 'deployment-id',
        monitorId: 'monitor-id',
        monitorName: 'Dust Monitor DATA-DUST',
        monitorType: 'Dust',
        graphName: 'Dust levels',
        minDate: '2026-05-24T09:00:00Z',
        maxDate: '2026-05-24T10:00:00Z',
        fromDate: '2026-05-24T09:00:00Z',
        toDate: '2026-05-24T10:00:00Z',
        fromDateChanged: false,
        toDateChanged: false,
        maxDuration: null,
        filterOption: 'raw',
        filterOptions: [{ value: 'raw', label: 'Raw' }],
        xAxisLabel: 'Sample Time',
        xAxisField: 'sampleTime',
        xAxisUnit: '',
        xAxisNumeric: false,
        yAxisLabel: 'ug/m3',
        decimalPlaces: 1,
        datasets: [
          {
            key: 'pm10',
            label: 'PM10',
            points: [{ time: '2026-05-24T10:00:00Z', y: 42.3 }]
          }
        ],
        thresholds: []
      });
    }

    if (url.pathname === '/api/data/deployments/deployment-id/traces') {
      return jsonResponse({
        deploymentId: 'deployment-id',
        monitorId: 'monitor-id',
        monitorName: 'Dust Monitor DATA-DUST',
        monitorType: 'Vibration',
        traces: [
          {
            id: 'trace-id',
            startTime: '2026-05-24T10:00:00Z',
            endTime: '2026-05-24T10:00:30Z',
            durationSeconds: 30
          }
        ]
      });
    }

    if (url.pathname === '/api/data/deployments/deployment-id/traces/trace-id') {
      return jsonResponse({
        deploymentId: 'deployment-id',
        monitorId: 'monitor-id',
        traceId: 'trace-id',
        monitorName: 'Dust Monitor DATA-DUST',
        fromDate: '2026-05-24T10:00:00Z',
        toDate: '2026-05-24T10:00:30Z',
        samples: [{ index: 1, x: 0.1, y: 0.2, z: 0.3 }]
      });
    }

    if (url.pathname === '/api/dashboard/map-markers') {
      return jsonResponse({
        siteId: url.searchParams.get('siteId'),
        siteName: 'RVT Test Site',
        isScopedToCurrentUser: false,
        markers: [
          {
            monitorId: 'monitor-id',
            deploymentId: 'deployment-id',
            latitude: 51.501,
            longitude: -0.141,
            typeOfMonitor: 'Dust',
            offline: false,
            alert: true,
            caution: false,
            siteName: 'RVT Test Site',
            fleetNumber: 'P8-DUST',
            serialId: 'SER-P8',
            lastDataTime: '2026-05-24T10:00:00Z',
            what3words: 'filled.count.soap'
          }
        ]
      });
    }

    if (url.pathname === '/api/dashboard/calendar/month') {
      return jsonResponse({
        monitorId: 'monitor-id',
        deploymentId: 'deployment-id',
        fleetNumber: 'P8-DUST',
        serialId: 'SER-P8',
        typeOfMonitor: 'Dust',
        year: 2026,
        month: 5,
        startDate: '2026-05-01T00:00:00Z',
        endDate: '2026-05-31T00:00:00Z',
        unit: 'Βµg/mΒ³',
        deployments: [{ value: 'deployment-id', label: 'P8-DUST - RVT Test Site' }],
        days: [
          {
            date: '2026-05-24T00:00:00Z',
            isCurrentMonth: true,
            status: 'Alert',
            average: 61,
            notificationCount: 1
          }
        ]
      });
    }

    if (url.pathname === '/api/dashboard/calendar/day') {
      return jsonResponse({
        monitorId: 'monitor-id',
        displayDay: '2026-05-24T00:00:00Z',
        fleetNumber: 'P8-DUST',
        typeOfMonitor: 'Dust',
        unit: 'Βµg/mΒ³',
        values: [{ label: 'pm10', value: 61 }],
        alertLevels: [
          {
            id: 'alert-level-id',
            monitorId: 'monitor-id',
            serialId: 'SER-P8',
            alertField: 'pm10',
            limitOn: 50,
            limitOff: 45,
            alertType: 'Alert',
            isActive: true,
            averagingPeriod: 3600,
            averagingPeriodLabel: '1 hour',
            weekdays: true,
            saturdays: true,
            sundays: true,
            startTime: null,
            endTime: null,
            isDeleted: false
          }
        ],
        notifications: [
          {
            id: 'dashboard-notification-id',
            monitorId: 'monitor-id',
            fleetNumber: 'P8-DUST',
            serialId: 'SER-P8',
            alertType: 'Alert',
            alertField: 'pm10',
            level: 61,
            notificationTime: '2026-05-24T10:00:00Z'
          }
        ]
      });
    }

    if (url.pathname === '/api/companies') {
      if (companyRequestCount) {
        companyRequestCount.value += 1;
      }
      return jsonResponse({
        results: [
          {
            id: 'company-id',
            companyName: 'RVT Group',
            userCount: 2,
            sites: '1',
            contracts: '1'
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'companyName',
        sortDir: 'Ascending'
      });
    }

    if (url.pathname === '/api/users') {
      return jsonResponse({
        results: [
          {
            id: 'company-user-id',
            companyId: 'company-id',
            companyName: 'RVT Group',
            isDisabled: false,
            name: 'Company User',
            email: 'company.user@rvt.test',
            phoneNumber: '07123456789',
            companyRole: 'Site contact',
            role: 'CompanyUser',
            siteCount: 1,
            emailConfirmed: true,
            canView: true,
            canEdit: true,
            canDisable: true,
            canEnable: false,
            canDelete: true,
            canSendConfirmation: false,
            canSendPasswordReset: true,
            canManageNotificationSettings: true
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'email',
        sortDir: 'Ascending',
        companyId: null,
        companyName: null
      });
    }

    if (url.pathname === '/api/contracts') {
      return jsonResponse({
        results: [
          {
            id: 'contract-id',
            contractNumber: 'RVT-C-001',
            onHireDate: '2026-01-01T00:00:00Z',
            offHireDate: null,
            companyId: 'company-id',
            companyName: 'RVT Group',
            siteId: 'site-id',
            siteName: 'RVT Test Site'
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'contractNumber',
        sortDir: 'Ascending'
      });
    }

    if (url.pathname === '/api/sites') {
      return jsonResponse({
        results: [
          {
            id: 'site-id',
            siteName: 'RVT Test Site',
            archived: false,
            createDate: '2026-01-01T00:00:00Z',
            siteAddress: '1 Test Street',
            contracts: 'RVT-C-001',
            companyId: 'company-id',
            companyName: 'RVT Group',
            siteContact: 'Company User',
            monitorCount: 2,
            openNotificationCount: 1
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'siteName',
        sortDir: 'Ascending',
        isScopedToCurrentUser: false
      });
    }

    if (url.pathname === '/api/sites/site-id') {
      return jsonResponse({
        item: {
          id: 'site-id',
          siteName: 'RVT Test Site',
          archived: false,
          createDate: '2026-01-01T00:00:00Z',
          siteAddress: '1 Test Street',
          contracts: 'RVT-C-001',
          companyId: 'company-id',
          companyName: 'RVT Group',
          siteContact: 'Company User',
          customerLogoUrl: '/api/sites/site-id/customer-logo',
          monitorCount: 2,
          openNotificationCount: 1,
          startTime: '08:00',
          endTime: '18:00',
          satStartTime: null,
          satEndTime: null,
          sunStartTime: null,
          sunEndTime: null,
          operatingHours: [
            { dayOfWeek: 1, dayName: 'Monday', startTime: '07:00', endTime: '17:00', isClosed: false },
            { dayOfWeek: 2, dayName: 'Tuesday', startTime: '08:00', endTime: '18:00', isClosed: false },
            { dayOfWeek: 3, dayName: 'Wednesday', startTime: '09:00', endTime: '19:00', isClosed: false },
            { dayOfWeek: 4, dayName: 'Thursday', startTime: null, endTime: null, isClosed: true },
            { dayOfWeek: 5, dayName: 'Friday', startTime: '08:30', endTime: '16:30', isClosed: false },
            { dayOfWeek: 6, dayName: 'Saturday', startTime: '10:00', endTime: '14:00', isClosed: false },
            { dayOfWeek: 7, dayName: 'Sunday', startTime: null, endTime: null, isClosed: true }
          ],
          contractList: [],
          monitors: [
            {
              id: 'monitor-id',
              deploymentId: 'deployment-id',
              fleetNumber: 'MON-ONLINE',
              serialId: 'SER-P5',
              monitorName: 'Dust Monitor',
              typeOfMonitor: 'Dust',
              contractId: 'contract-id',
              contractNumber: 'RVT-C-001',
              lastDataTime: '2026-01-02T00:00:00Z',
              offLine: false,
              lat: 51.501,
              lng: -0.141,
              what3words: 'filled.count.soap'
            }
          ],
          openNotifications: [
            {
              id: 'notification-id',
              monitorId: 'monitor-id',
              fleetNumber: 'MON-ONLINE',
              serialId: 'SER-P5',
              alertType: 'Alert',
              alertField: 'pm10',
              limitOn: 45,
              level: 48,
              notificationTime: '2026-01-02T10:00:00Z'
            }
          ],
          archive: null,
          companies: [{ value: 'company-id', label: 'RVT Group' }],
          availableContracts: [{ value: 'contract-id', label: 'RVT-C-001' }],
          canManage: true
        }
      });
    }

    if (url.pathname === '/api/help/admin') {
      return jsonResponse({
        searchText: url.searchParams.get('searchText') ?? '',
        status: url.searchParams.get('status') ?? 'All',
        contentType: url.searchParams.get('contentType') ?? 'All',
        sections: [
          {
            id: 'help-section-id',
            title: 'Data Readings',
            slug: 'data-readings',
            sortOrder: 0,
            articles: [
              {
                id: 'help-article-id',
                title: 'Draft FAQ',
                slug: 'draft-faq',
                summary: 'Draft Help CMS content.',
                contentType: 'FAQ',
                sectionTitle: 'Data Readings',
                sectionSlug: 'data-readings',
                sectionSortOrder: 0,
                sortOrder: 0
              }
            ]
          }
        ],
        articles: [
          {
            id: 'help-article-id',
            title: 'Draft FAQ',
            slug: 'draft-faq',
            summary: 'Draft Help CMS content.',
            body: 'Draft FAQ body.',
            contentType: 'FAQ',
            sectionTitle: 'Data Readings',
            sectionSlug: 'data-readings',
            sectionSortOrder: 0,
            sortOrder: 0,
            isPublished: false,
            createdAtUtc: '2026-06-08T00:00:00Z',
            updatedAtUtc: '2026-06-08T00:00:00Z',
            assets: []
          },
          {
            id: 'help-article-lowercase-type-id',
            title: 'Lowercase type article',
            slug: 'lowercase-type-article',
            summary: 'Custom Help CMS content.',
            body: 'Custom body.',
            contentType: 'article',
            sectionTitle: 'Data Readings',
            sectionSlug: 'data-readings',
            sectionSortOrder: 0,
            sortOrder: 1,
            isPublished: true,
            createdAtUtc: '2026-06-08T00:00:00Z',
            updatedAtUtc: '2026-06-08T00:00:00Z',
            assets: []
          }
        ]
      });
    }

    if (url.pathname === '/api/help/admin/articles/help-article-id') {
      return jsonResponse({
        item: {
          id: 'help-article-id',
          title: 'Updated FAQ',
          slug: 'updated-faq',
          summary: 'Draft Help CMS content.',
          body: 'Draft FAQ body.',
          contentType: 'FAQ',
          sectionTitle: 'Data Readings',
          sectionSlug: 'data-readings',
          sectionSortOrder: 0,
          sortOrder: 0,
          isPublished: true,
          createdAtUtc: '2026-06-08T00:00:00Z',
          updatedAtUtc: '2026-06-10T00:00:00Z',
          assets: []
        }
      });
    }

    if (url.pathname === '/api/help') {
      return jsonResponse({
        searchText: url.searchParams.get('searchText') ?? '',
        sections: [
          {
            id: 'help-section-id',
            title: 'Data Readings',
            slug: 'data-readings',
            sortOrder: 0,
            articles: [
              {
                id: 'help-article-id',
                title: 'Dust reading definitions',
                slug: 'dust-reading-definitions',
                summary: 'Common dust-reading terms used in RVT Cloud.',
                contentType: 'FAQ',
                sectionTitle: 'Data Readings',
                sectionSlug: 'data-readings',
                sectionSortOrder: 0,
                sortOrder: 0
              }
            ]
          }
        ]
      });
    }

    if (url.pathname === '/api/help/articles/dust-reading-definitions') {
      return jsonResponse({
        item: {
          id: 'help-article-id',
          title: 'Dust reading definitions',
          slug: 'dust-reading-definitions',
          summary: 'Common dust-reading terms used in RVT Cloud.',
          body: 'PM10 and PM2.5 readings represent particulate matter levels captured from site monitors.',
          contentType: 'FAQ',
          sectionTitle: 'Data Readings',
          sectionSlug: 'data-readings',
          sectionSortOrder: 0,
          sortOrder: 0,
          isPublished: true,
          createdAtUtc: '2026-06-08T00:00:00Z',
          updatedAtUtc: '2026-06-08T00:00:00Z',
          assets: [
            {
              id: 'help-asset-id',
              title: 'Dust monitoring guide',
              assetType: 'Document',
              url: '/help-assets/data-readings/dust-guide.pdf',
              internalPath: 'help-assets/data-readings/dust-guide.pdf',
              sortOrder: 0
            }
          ]
        }
      });
    }

    if (url.pathname === '/api/sites/site-id/notification-settings') {
      return jsonResponse({
        siteId: 'site-id',
        siteName: 'RVT Test Site',
        settings: []
      });
    }

    if (url.pathname === '/api/monitors/unattached') {
      return jsonResponse({
        results: [
          {
            id: '11111111-1111-1111-1111-111111111111',
            fleetNumber: 'RVT-OLD-001',
            serialId: 'SER-OLD-001',
            manufacturer: 'RVT',
            model: 'Dust',
            firmwareVersion: '1.0',
            typeOfMonitor: 'Dust',
            contractId: null,
            contractNumber: null,
            siteId: null,
            siteName: null,
            companyId: null,
            companyName: null,
            startDate: null,
            endDate: null,
            lastDataTime: null,
            isAssigned: false,
            isOffline: false,
            hasAlerts: false,
            hasCautions: false,
            canEdit: true,
            canAssign: false,
            canInstallerEdit: false,
            hasRelatedData: true,
            willArchiveOnRemoval: true,
            impact: {
              deploymentCount: 1,
              notificationCount: 2,
              alertRuleCount: 3,
              measurementTableCount: 1,
              measurementRowCount: 20,
              hasRelatedData: true
            }
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'fleetNumber',
        sortDir: 'Ascending',
        canRemove: true
      });
    }

    if (url.pathname === '/api/monitors/11111111-1111-1111-1111-111111111111/unattached') {
      return jsonResponse({
        id: '11111111-1111-1111-1111-111111111111',
        action: 'archived',
        message: "Monitor 'RVT-OLD-001' has been archived because related data exists.",
        impact: {
          deploymentCount: 1,
          notificationCount: 2,
          alertRuleCount: 3,
          measurementTableCount: 1,
          measurementRowCount: 20,
          hasRelatedData: true
        }
      });
    }

    if (url.pathname === '/api/monitors') {
      return jsonResponse({
        results: [
          {
            id: 'monitor-id',
            deploymentId: 'deployment-id',
            fleetNumber: 'MON-ONLINE',
            serialId: 'SER-P5',
            manufacturer: 'RVT',
            model: 'Dust',
            firmwareVersion: '1.0',
            typeOfMonitor: 'Dust',
            contractId: 'contract-id',
            contractNumber: 'RVT-C-001',
            siteId: 'site-id',
            siteName: 'RVT Test Site',
            companyId: 'company-id',
            companyName: 'RVT Group',
            startDate: '2026-01-01T00:00:00Z',
            lastDataTime: '2026-01-02T00:00:00Z',
            isAssigned: true,
            isOffline: false,
            hasAlerts: true,
            hasCautions: false,
            canEdit: true,
            canAssign: false,
            canInstallerEdit: true
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'fleetNumber',
        sortDir: 'Ascending',
        state: 'all',
        isScopedToCurrentUser: false,
        canManage: true,
        canUseInstallerTools: true
      });
    }

    if (url.pathname === '/api/monitors/monitor-id') {
      return jsonResponse({
        item: {
          id: 'monitor-id',
          deploymentId: 'deployment-id',
          fleetNumber: 'MON-ONLINE',
          serialId: 'SER-P5',
          manufacturer: 'RVT',
          model: 'Dust',
          firmwareVersion: '1.0',
          typeOfMonitor: 'Dust',
          contractId: 'contract-id',
          contractNumber: 'MON-CON-001',
          siteId: 'site-id',
          siteName: 'RVT Test Site',
          companyId: 'company-id',
          companyName: 'RVT Group',
          startDate: '2026-01-01T00:00:00Z',
          endDate: null,
          lastDataTime: '2026-01-02T00:00:00Z',
          isAssigned: true,
          isOffline: false,
          hasAlerts: true,
          hasCautions: false,
          canEdit: true,
          canAssign: false,
          canInstallerEdit: true,
          listedAtTime: '2025-12-01T00:00:00Z',
          calibrationDate: null,
          calibrationDue: null,
          lat: 51.5,
          lng: -0.12,
          location: 'North boundary',
          what3words: 'filled.count.soap',
          pictureLink: '/api/monitors/monitor-id/picture',
          statusLabel: 'Online',
          monitorNotes: 'No notes for this monitor',
          latestReading: {
            label: 'Latest Breach',
            field: 'pm10',
            value: 48,
            unit: 'ug/m3',
            sampleTime: '2026-01-02T10:00:00Z',
            detail: 'Dust PM10 live reading'
          },
          latestAverage: {
            label: 'Latest 15 Min Average',
            field: 'pm10',
            value: 24.5,
            unit: 'ug/m3',
            sampleTime: '2026-01-02T10:15:00Z',
            detail: 'Dust PM10 15 minute average'
          },
          latestBattery: {
            label: 'Battery Charge',
            field: 'batteryCharge',
            value: 87,
            unit: '%',
            sampleTime: '2026-01-02T09:58:00Z',
            detail: 'Omnidots sensor status'
          },
          deploymentSummary: {
            contractNumber: 'MON-CON-001',
            siteName: 'RVT Test Site',
            companyName: 'RVT Group',
            onHireDate: '2026-01-01T00:00:00Z',
            offHireDate: null,
            addedDate: '2026-01-01T00:00:00Z'
          },
          alertLevels: [],
          recentNotifications: [
            {
              id: 'notification-id',
              monitorId: 'monitor-id',
              notificationTime: '2026-01-02T10:00:00Z',
              alertType: 'Alert',
              alertField: 'pm10',
              limitOn: 45,
              level: 48,
              closedTime: null
            }
          ]
        }
      });
    }

    if (url.pathname === '/api/alert-levels') {
      return jsonResponse({
        monitorId: 'monitor-id',
        serialId: 'SER-V1',
        fleetNumber: 'VIB-PEAK',
        typeOfMonitor: 'Vibration',
        canManage: true,
        options: {
          monitorId: 'monitor-id',
          serialId: 'SER-V1',
          typeOfMonitor: 'Vibration',
          alertFields: [{ value: 'Peak', label: 'Peak' }],
          alertTypes: [
            { value: 'Alert', label: 'Alert' },
            { value: 'Caution', label: 'Caution' }
          ],
          averagingPeriods: []
        },
        results: [
          {
            id: 'vibration-alert-id',
            monitorId: 'monitor-id',
            serialId: 'SER-V1',
            alertField: 'Peak',
            limitOn: 8,
            limitOff: 5,
            alertType: 'Alert',
            isActive: false,
            averagingPeriod: 60,
            averagingPeriodLabel: '',
            weekdays: true,
            saturdays: false,
            sundays: false,
            startTime: null,
            endTime: null,
            isDeleted: false
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'alertField',
        sortDir: 'Ascending'
      });
    }

    if (url.pathname === '/api/notifications') {
      return jsonResponse({
        results: [
          {
            id: 'notification-id',
            monitorId: 'monitor-id',
            deploymentId: 'deployment-id',
            fleetNumber: 'MON-ONLINE',
            serialId: 'SER-P5',
            typeOfMonitor: 'Dust',
            alertType: 'Alert',
            alertField: 'PM10',
            limitOn: 50,
            level: 61,
            averagingPeriod: 900,
            notificationTime: '2026-01-02T10:00:00Z',
            closedTime: null,
            closedByUser: null,
            closedNote: null,
            contractId: 'contract-id',
            contractNumber: 'RVT-C-001',
            siteId: 'site-id',
            siteName: 'RVT Test Site',
            companyId: 'company-id',
            companyName: 'RVT Group',
            limitName: 'PM10 > 50',
            alertStatus: 'Open',
            canClose: true
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'notificationTime',
        sortDir: 'Descending',
        state: 'open',
        isScopedToCurrentUser: false,
        canClose: true
      });
    }

    if (url.pathname === '/api/notifications/notification-id') {
      return jsonResponse({
        item: {
          id: 'notification-id',
          monitorId: 'monitor-id',
          deploymentId: 'deployment-id',
          fleetNumber: 'MON-ONLINE',
          serialId: 'SER-P5',
          typeOfMonitor: 'Dust',
          alertType: 'Alert',
          alertField: 'PM10',
          limitOn: 50,
          level: 61,
          notificationTime: '2026-01-02T10:00:00Z',
          closedTime: null,
          closedByUser: null,
          closedNote: null,
          contractId: 'contract-id',
          contractNumber: 'RVT-C-001',
          siteId: 'site-id',
          siteName: 'RVT Test Site',
          companyId: 'company-id',
          companyName: 'RVT Group',
          limitName: 'PM10 > 50',
          alertStatus: 'Open',
          canClose: true,
          location: 'North boundary',
          what3words: 'filled.count.soap',
          graphFromUtc: '2026-01-02T09:00:00Z',
          graphToUtc: '2026-01-02T11:00:00Z',
          relatedNotifications: [],
          alertLevels: []
        }
      });
    }

    if (url.pathname === '/api/reports') {
      return jsonResponse({
        results: [
          {
            id: 'report-id',
            siteId: 'site-id',
            siteName: 'RVT Test Site',
            reportDate: '2026-01-07T08:00:00Z',
            reportFrom: '2026-01-01T00:00:00Z',
            reportTo: '2026-01-07T00:00:00Z',
            reportLink: 'https://reports.rvt.test/weekly.pdf',
            reportRuleId: 'report-rule-id',
            frequency: 2,
            frequencyLabel: 'Weekly',
            reportName: 'Weekly Compliance',
            contracts: 'RVT-C-001'
          }
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
        searchText: '',
        sort: 'reportDate',
        sortDir: 'Descending'
      });
    }

    if (url.pathname === '/api/report-rules/options') {
      return jsonResponse({
        sites: [{ value: 'site-id', label: 'RVT Test Site' }],
        frequencies: [
          { value: '1', label: 'Daily' },
          { value: '2', label: 'Weekly' },
          { value: '3', label: 'Monthly' }
        ],
        daysOfWeek: [{ value: '1', label: 'Monday' }],
        alertRuleGuidelines: [
          {
            monitorType: 'Dust',
            title: 'Dust alert rules',
            summary: 'Use PM10 and PM2.5 thresholds for dust monitors.'
          }
        ]
      });
    }

    if (url.pathname === '/api/report-rules/report-rule-id') {
      return jsonResponse({
        item: {
          id: 'report-rule-id',
          siteId: 'site-id',
          siteName: 'RVT Test Site',
          frequency: 2,
          frequencyLabel: 'Weekly',
          dayOfWeek: 1,
          dayOfMonth: null,
          reportName: 'Weekly Compliance',
          lastGenerated: null,
          canManage: true,
          assignedUserCount: 1,
          sites: [{ value: 'site-id', label: 'RVT Test Site' }],
          frequencies: [
            { value: '1', label: 'Daily' },
            { value: '2', label: 'Weekly' },
            { value: '3', label: 'Monthly' }
          ],
          daysOfWeek: [{ value: '1', label: 'Monday' }],
          alertRuleGuidelines: [
            {
              monitorType: 'Dust',
              title: 'Dust alert rules',
              summary: 'Use PM10 and PM2.5 thresholds for dust monitors.'
            }
          ]
        }
      });
    }

    if (url.pathname === '/api/report-rules/report-rule-id/generation-requests') {
      return jsonResponse({
        id: 'generation-request-id',
        reportRuleId: 'report-rule-id',
        status: 'Queued',
        message: 'Manual generation queued.',
        requestedAtUtc: '2026-06-24T10:00:00Z'
      });
    }

    if (url.pathname === '/api/report-rules/report-rule-id/users') {
      return jsonResponse({
        item: {
          reportRuleId: 'report-rule-id',
          siteId: 'site-id',
          siteName: 'RVT Test Site',
          companyId: 'company-id',
          companyName: 'RVT Group',
          availableUsers: [reportUser('available-user-id', 'available@rvt.test', 'Available User')],
          assignedUsers: [reportUser('assigned-user-id', 'assigned@rvt.test', 'Assigned User')]
        }
      });
    }

    if (url.pathname === '/api/report-rules/report-rule-id/available-users') {
      return jsonResponse(reportUserPage(url, [reportUser('available-user-id', 'available@rvt.test', 'Available User')]));
    }

    if (url.pathname === '/api/report-rules/report-rule-id/assigned-users') {
      return jsonResponse(reportUserPage(url, [reportUser('assigned-user-id', 'assigned@rvt.test', 'Assigned User')]));
    }

    if (url.pathname.startsWith('/api/lookups')) {
      return jsonResponse({ kind: 'companies', query: 'rvt', take: 8, results: ['RVT Group'] });
    }

    return new Response(null, { status: 404 });
  }));
}

// Function summary: Builds shared company options for contract form retry tests.
function contractTestCompanies() {
  return [
    { value: 'company-id', label: 'RVT Group' },
    { value: 'other-company-id', label: 'Other Company' }
  ];
}

// Function summary: Builds a report-recipient user fixture for reporting tests.
function reportUser(id: string, email: string, name: string) {
  return {
    id,
    companyId: 'company-id',
    companyName: 'RVT Group',
    isDisabled: false,
    name,
    email,
    phoneNumber: null,
    companyRole: 'Operations',
    role: 'CompanyUser',
    siteCount: 1,
    emailConfirmed: true,
    canView: true,
    canEdit: true,
    canDisable: true,
    canEnable: false,
    canDelete: true,
    canSendConfirmation: false,
    canSendPasswordReset: true,
    canManageNotificationSettings: true
  };
}

// Function summary: Builds a paged report-recipient response fixture for reporting tests.
function reportUserPage(url: URL, users: ReturnType<typeof reportUser>[]) {
  return {
    reportRuleId: 'report-rule-id',
    siteId: 'site-id',
    siteName: 'RVT Test Site',
    companyId: 'company-id',
    companyName: 'RVT Group',
    assignedUserCount: 1,
    results: users,
    total: users.length,
    page: Number(url.searchParams.get('page') ?? 1),
    pageSize: Number(url.searchParams.get('pageSize') ?? 10),
    totalPages: users.length > 0 ? 1 : 0,
    hasPreviousPage: false,
    hasNextPage: false,
    searchText: url.searchParams.get('searchText') ?? '',
    sort: url.searchParams.get('sort') ?? 'email',
    sortDir: url.searchParams.get('sortDir') ?? 'Ascending'
  };
}

// Function summary: Builds a deferred response fixture so tests can resolve stale requests out of order.
function deferredResponse() {
  let resolve!: (response: Response) => void;
  const promise = new Promise<Response>((resolvePromise) => {
    resolve = resolvePromise;
  });

  return { promise, resolve };
}

// Function summary: Builds a monitor list page fixture for stale-response tests.
function monitorPage(url: URL, fleetNumber: string) {
  return {
    results: [
      {
        id: `${fleetNumber.toLowerCase()}-id`,
        deploymentId: `${fleetNumber.toLowerCase()}-deployment-id`,
        fleetNumber,
        serialId: `${fleetNumber}-SER`,
        manufacturer: 'RVT',
        model: 'Dust',
        firmwareVersion: '1.0',
        typeOfMonitor: 'Dust',
        contractId: 'contract-id',
        contractNumber: 'RVT-C-001',
        siteId: 'site-id',
        siteName: 'RVT Test Site',
        companyId: 'company-id',
        companyName: 'RVT Group',
        startDate: '2026-01-01T00:00:00Z',
        lastDataTime: '2026-01-02T00:00:00Z',
        isAssigned: true,
        isOffline: false,
        hasAlerts: false,
        hasCautions: false,
        canEdit: true,
        canAssign: false,
        canInstallerEdit: true
      }
    ],
    total: 1,
    page: Number(url.searchParams.get('page') ?? 1),
    pageSize: Number(url.searchParams.get('pageSize') ?? 10),
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    searchText: url.searchParams.get('searchText') ?? '',
    sort: url.searchParams.get('sort') ?? 'fleetNumber',
    sortDir: url.searchParams.get('sortDir') ?? 'Ascending',
    state: url.searchParams.get('state') ?? 'all',
    isScopedToCurrentUser: false,
    canManage: true,
    canUseInstallerTools: true
  };
}

// Function summary: Handles the json response workflow for this module.
function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
    status
  });
}

// Function summary: Renders the ThrowingPanel React component and wires its local UI behavior.
function ThrowingPanel() {
  throw new Error('Render boundary failure');
}

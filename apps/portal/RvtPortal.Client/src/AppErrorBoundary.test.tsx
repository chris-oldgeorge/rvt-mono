// File summary: Covers the app-level error boundary through the public App surface.
// Major updates:
// - 2026-07-30 pending Replaced the test-only AppErrorBoundary export with a shell-render failure.

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { App } from './App';

vi.mock('./api/client', () => ({
  getCurrentAuth: () =>
    Promise.resolve({
      isAuthenticated: true,
      user: { id: 'admin', email: 'admin@rvt.test', name: 'Admin', roles: ['RVTAdmin'] },
    }),
}));

vi.mock('./PortalShell', () => ({
  PortalShell: () => {
    throw new Error('portal shell render failure');
  },
}));

describe('App error boundary', () => {
  it('replaces a failed shell render with a stable panel that leaks no exception detail', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    globalThis.history.replaceState(null, '', '/dashboard');

    render(<App />);

    expect(await screen.findByRole('heading', { name: /something went wrong/i })).toBeInTheDocument();
    expect(screen.getByText(/refresh the page or return to the dashboard/i)).toBeInTheDocument();
    expect(screen.queryByText(/portal shell render failure/i)).not.toBeInTheDocument();

    consoleError.mockRestore();
  });
});

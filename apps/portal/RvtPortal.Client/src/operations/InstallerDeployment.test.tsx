// File summary: Covers installer deployment coordinate validation and what3words conversion feedback.
// Major updates:
// - 2026-07-30 pending Covered blank and non-numeric coordinates being rejected instead of saved as 0, 0.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MonitorsPanel } from './MonitorPanels';

const api = vi.hoisted(() => ({
  addDefaultMonitorAlertLevels: vi.fn(),
  convertWhat3Words: vi.fn(),
  getInstallerMonitor: vi.fn(),
  getMonitor: vi.fn(),
  queryInstallerMonitors: vi.fn(),
  queryMonitors: vi.fn(),
  updateInstallerDeployment: vi.fn(),
  updateMonitor: vi.fn(),
  uploadMonitorPicture: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  ApiError: class ApiError extends Error {},
  isAbortError: () => false,
}));

const installerPath = '/monitors/monitor-1/installer';

// Function summary: Builds the installer monitor response backing the deployment form.
function installerMonitor(lat: number | null = 51.5072, lng: number | null = -0.1276) {
  return {
    item: {
      id: 'monitor-1',
      deploymentId: 'deployment-1',
      fleetNumber: 'RVT-001',
      serialId: 'SER-001',
      typeOfMonitor: 'Noise',
      location: 'North gate',
      what3words: 'filled.count.soap',
      lat,
      lng,
      alertLevels: [],
      recentNotifications: [],
    },
  };
}

// Function summary: Renders the installer deployment route for coordinate assertions.
function renderInstallerPanel() {
  return render(
    <MonitorsPanel locationPath={installerPath} onNavigate={vi.fn()} onRequestError={vi.fn()} canUseInstallerTools />,
  );
}

describe('Installer deployment coordinates', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.getInstallerMonitor.mockResolvedValue(installerMonitor());
    api.updateInstallerDeployment.mockResolvedValue({ item: { id: 'monitor-1' } });
  });

  it('rejects a cleared coordinate with a field error instead of saving 0, 0', async () => {
    const user = userEvent.setup();
    renderInstallerPanel();

    await user.clear(await screen.findByDisplayValue('51.5072'));
    await user.click(screen.getByRole('button', { name: /save deployment/i }));

    expect(await screen.findByText(/Latitude is required/)).toBeInTheDocument();
    expect(api.updateInstallerDeployment).not.toHaveBeenCalled();
  });

  it('rejects a mistyped coordinate with a field error instead of an opaque 400', async () => {
    const user = userEvent.setup();
    renderInstallerPanel();

    const longitude = await screen.findByDisplayValue('-0.1276');
    await user.clear(longitude);
    await user.type(longitude, '-0.12.76');
    await user.click(screen.getByRole('button', { name: /save deployment/i }));

    expect(await screen.findByText(/Longitude must be a number/)).toBeInTheDocument();
    expect(api.updateInstallerDeployment).not.toHaveBeenCalled();
  });

  it('saves the typed coordinates once both fields parse', async () => {
    const user = userEvent.setup();
    renderInstallerPanel();

    const latitude = await screen.findByDisplayValue('51.5072');
    await user.clear(latitude);
    await user.type(latitude, '51.5');
    await user.click(screen.getByRole('button', { name: /save deployment/i }));

    await waitFor(() =>
      expect(api.updateInstallerDeployment).toHaveBeenCalledWith(
        'deployment-1',
        expect.objectContaining({ lat: 51.5, lng: -0.1276 }),
      ),
    );
  });
});

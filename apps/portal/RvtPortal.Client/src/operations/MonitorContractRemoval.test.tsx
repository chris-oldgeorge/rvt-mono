// File summary: Covers the confirmation gate on both contract-removal entry points.
// Major updates:
// - 2026-07-30 pending Covered confirmation before removing a monitor from its contract.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MonitorAssignmentPanel } from './MonitorAssignmentPanel';
import { MonitorDetailPanel } from './MonitorDetailPanel';

const api = vi.hoisted(() => ({
  addMonitorToContract: vi.fn(),
  getInstallerMonitor: vi.fn(),
  getInstallerMonitorStatus: vi.fn(),
  getMonitor: vi.fn(),
  getMonitorAssignment: vi.fn(),
  removeMonitorFromContract: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  ApiError: class ApiError extends Error {},
  isAbortError: () => false,
}));

// Function summary: Builds the monitor detail response used by the detail panel tests.
function monitorDetail() {
  return {
    item: {
      id: 'monitor-1',
      fleetNumber: 'RVT-001',
      serialId: 'SER-001',
      typeOfMonitor: 'Noise',
      contractNumber: 'C-100',
      isAssigned: true,
      isOffline: false,
      hasAlerts: false,
      hasCautions: false,
      alertLevels: [],
      recentNotifications: [],
    },
  };
}

// Function summary: Builds the assignment context used by the assignment panel tests.
function assignmentContext() {
  return {
    siteId: 'site-1',
    siteName: 'RVT Test Site',
    contractId: 'contract-1',
    contracts: [{ value: 'contract-1', label: 'C-100' }],
    availableMonitors: [],
    assignedMonitors: [
      {
        id: 'monitor-1',
        fleetNumber: 'RVT-001',
        serialId: 'SER-001',
        typeOfMonitor: 'Noise',
        siteName: 'RVT Test Site',
      },
    ],
  };
}

describe('Monitor contract removal confirmations', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.getMonitor.mockResolvedValue(monitorDetail());
    api.getMonitorAssignment.mockResolvedValue(assignmentContext());
    api.removeMonitorFromContract.mockResolvedValue({});
  });

  it('confirms before removing a monitor from its contract on the detail panel', async () => {
    const user = userEvent.setup();
    render(
      <MonitorDetailPanel
        monitorId="monitor-1"
        locationPath="/monitors/monitor-1"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
        canManage
      />,
    );

    await user.click(await screen.findByRole('button', { name: /^remove$/i }));

    expect(api.removeMonitorFromContract).not.toHaveBeenCalled();
    expect(await screen.findByText(/Remove RVT-001 from C-100/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^cancel$/i }));
    expect(api.removeMonitorFromContract).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /^remove$/i }));
    await user.click(await screen.findByRole('button', { name: 'Remove from contract' }));

    await waitFor(() => expect(api.removeMonitorFromContract).toHaveBeenCalledWith('monitor-1'));
  });

  it('confirms before removing an assigned monitor on the assignment panel', async () => {
    const user = userEvent.setup();
    render(
      <MonitorAssignmentPanel
        siteId="site-1"
        contractId="contract-1"
        locationPath="/monitors/assign?siteId=site-1"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    await user.click(await screen.findByRole('button', { name: 'Remove monitor' }));

    expect(api.removeMonitorFromContract).not.toHaveBeenCalled();
    expect(await screen.findByText(/Remove RVT-001 from RVT Test Site/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Remove from contract' }));

    await waitFor(() => expect(api.removeMonitorFromContract).toHaveBeenCalledWith('monitor-1'));
  });
});

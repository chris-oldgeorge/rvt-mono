// File summary: Covers the unattached-monitor removal panel's audit reason capture and confirmation flow.
// Major updates:
// - 2026-07-30 pending Covered the removal reason reaching the request body from inside the confirmation.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnattachedMonitorRemovalPanel } from './MonitorRemovalPanel';
import type { UnattachedMonitorListItem } from '../dtos';

const api = vi.hoisted(() => ({
  queryUnattachedMonitors: vi.fn(),
  removeUnattachedMonitor: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  isAbortError: () => false,
}));

// Function summary: Builds one unattached monitor row for the removal grid.
function unattachedMonitor(): UnattachedMonitorListItem {
  return {
    id: 'monitor-1',
    fleetNumber: 'RVT-001',
    serialId: 'SER-001',
    typeOfMonitor: 'Noise',
    model: 'Model A',
    siteName: null,
    hasRelatedData: true,
    willArchiveOnRemoval: true,
    impact: {
      deploymentCount: 1,
      notificationCount: 0,
      alertRuleCount: 0,
      measurementTableCount: 0,
      measurementRowCount: 12,
      hasRelatedData: true,
    },
  };
}

// Function summary: Builds the paged unattached-monitor response for the removal grid.
function unattachedResponse() {
  return {
    results: [unattachedMonitor()],
    total: 1,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    canRemove: true,
  };
}

describe('UnattachedMonitorRemovalPanel', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.queryUnattachedMonitors.mockResolvedValue(unattachedResponse());
    api.removeUnattachedMonitor.mockResolvedValue({ message: 'Monitor archived.' });
  });

  it('sends the reason typed inside the confirmation to the removal request', async () => {
    const user = userEvent.setup();
    render(
      <UnattachedMonitorRemovalPanel
        locationPath="/monitors/unattached"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    await user.click(await screen.findByRole('button', { name: 'Remove monitor' }));
    await user.type(await screen.findByPlaceholderText('Reason recorded for audit history'), 'Returned to depot');
    await user.click(screen.getByRole('button', { name: 'Archive' }));

    await waitFor(() =>
      expect(api.removeUnattachedMonitor).toHaveBeenCalledWith('monitor-1', { reason: 'Returned to depot' }),
    );
  });

  it('drops a half-typed reason when the confirmation is dismissed', async () => {
    const user = userEvent.setup();
    render(
      <UnattachedMonitorRemovalPanel
        locationPath="/monitors/unattached"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    await user.click(await screen.findByRole('button', { name: 'Remove monitor' }));
    await user.type(await screen.findByPlaceholderText('Reason recorded for audit history'), 'Mistake');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await user.click(await screen.findByRole('button', { name: 'Remove monitor' }));

    expect(await screen.findByPlaceholderText('Reason recorded for audit history')).toHaveValue('');
  });
});

// File summary: Covers alert-level threshold validation on the standard and vibration forms.
// Major updates:
// - 2026-07-30 pending Covered blank and non-numeric thresholds being rejected instead of saved as 0.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AlertLevelsPanel } from './AlertLevelPanels';

const api = vi.hoisted(() => ({
  createAlertLevel: vi.fn(),
  deleteAlertLevel: vi.fn(),
  getAlertLevel: vi.fn(),
  getAlertLevelOptions: vi.fn(),
  queryAlertLevels: vi.fn(),
  updateAlertLevel: vi.fn(),
  updateVibrationAlertLevels: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  isAbortError: () => false,
}));

// Function summary: Builds the alert-level option lists backing the standard form.
function alertLevelOptions() {
  return {
    typeOfMonitor: 'Noise',
    alertFields: [{ value: 'laeq', label: 'LAeq' }],
    alertTypes: [{ value: 'Alert', label: 'Alert' }],
    averagingPeriods: [{ value: '900', label: '15 minutes' }],
  };
}

// Function summary: Builds a paged alert-level response for the vibration form seed request.
function alertLevelsResponse(alert: number, caution: number) {
  return {
    results: [
      { id: 'level-1', alertType: 'Alert', limitOn: alert, limitOff: alert },
      { id: 'level-2', alertType: 'Caution', limitOn: caution, limitOff: caution },
    ],
    total: 2,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}

describe('Alert level thresholds', () => {
  beforeEach(() => {
    Object.values(api).forEach((request) => request.mockReset());
    api.getAlertLevelOptions.mockResolvedValue(alertLevelOptions());
    api.queryAlertLevels.mockResolvedValue(alertLevelsResponse(10, 5));
    api.createAlertLevel.mockResolvedValue({});
    api.updateVibrationAlertLevels.mockResolvedValue({ externalSyncAttempted: true });
  });

  it('refuses a blank limit rather than creating a level that alerts on every reading', async () => {
    const user = userEvent.setup();
    render(
      <AlertLevelsPanel
        monitorId="monitor-1"
        locationPath="/monitors/monitor-1/alert-levels/new"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
        canManage
      />,
    );

    await user.click(await screen.findByRole('button', { name: /save alert level/i }));

    expect(await screen.findByText(/Limit On is required/)).toBeInTheDocument();
    expect(screen.getByText(/Limit Off is required/)).toBeInTheDocument();
    expect(api.createAlertLevel).not.toHaveBeenCalled();
  });

  it('sends the typed limits once both parse', async () => {
    const user = userEvent.setup();
    render(
      <AlertLevelsPanel
        monitorId="monitor-1"
        locationPath="/monitors/monitor-1/alert-levels/new"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
        canManage
      />,
    );

    await user.type(await screen.findByLabelText('Limit On'), '72.5');
    await user.type(screen.getByLabelText('Limit Off'), '70');
    await user.click(screen.getByRole('button', { name: /save alert level/i }));

    await waitFor(() =>
      expect(api.createAlertLevel).toHaveBeenCalledWith(expect.objectContaining({ limitOn: 72.5, limitOff: 70 })),
    );
  });

  it('refuses a non-numeric vibration threshold and keeps the saved notice visible', async () => {
    const user = userEvent.setup();
    render(
      <AlertLevelsPanel
        monitorId="monitor-1"
        locationPath="/monitors/monitor-1/alert-levels/vibration"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
        canManage
      />,
    );

    const alertInput = await screen.findByLabelText('Alert Level');
    await user.clear(alertInput);
    await user.type(alertInput, '1o');
    await user.click(screen.getByRole('button', { name: /save vibration levels/i }));

    expect(await screen.findByText(/Alert Level must be a number/)).toBeInTheDocument();
    expect(api.updateVibrationAlertLevels).not.toHaveBeenCalled();

    await user.clear(alertInput);
    await user.type(alertInput, '12');
    await user.click(screen.getByRole('button', { name: /save vibration levels/i }));

    expect(await screen.findByText('Vibration levels saved and synced.')).toBeInTheDocument();
    expect(api.updateVibrationAlertLevels).toHaveBeenCalledWith('monitor-1', { alertLevel: 12, cautionLevel: 5 });
  });
});

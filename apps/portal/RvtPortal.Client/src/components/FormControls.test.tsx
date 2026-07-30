// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-07-30 pending Covered confirmation focus handoff, Tab containment and Escape cancellation.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import type { SubmitEvent } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog, FormField, Notice, SubmitButton } from './FormControls';

describe('FormControls', () => {
  it('renders notices, validation summaries, and field errors', () => {
    render(
      <>
        <Notice tone="success" message="Saved" />
        <Notice tone="error" message="Failed" />
        <Notice tone="info" message="Review" />
        <FormField label="Email" error="Use a valid email">
          <input aria-label="Email" />
        </FormField>
      </>,
    );

    expect(screen.getByText('Saved')).toBeInTheDocument();
    expect(screen.getByText('Saved').closest('output')).not.toHaveAttribute('role');
    expect(screen.getByText('Failed').closest('output')).toHaveAttribute('role', 'alert');
    expect(screen.getByText('Review').closest('output')).not.toHaveAttribute('role');
    expect(screen.getByText('Use a valid email')).toBeInTheDocument();
  });

  it('prevents double-submit while a form action is pending', async () => {
    const user = userEvent.setup();
    let resolveSubmit: () => void = () => undefined;
    const submit = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveSubmit = resolve;
        }),
    );

    render(<SubmitHarness onSubmit={submit} />);

    await user.click(screen.getByRole('button', { name: /save changes/i }));
    const pendingButton = await screen.findByRole('button', { name: /saving/i });
    expect(pendingButton).toBeDisabled();
    await user.click(pendingButton);

    expect(submit).toHaveBeenCalledTimes(1);
    resolveSubmit();
    await waitFor(() => expect(screen.getByRole('button', { name: /save changes/i })).toBeEnabled());
  });

  it('renders confirmation dialogs with explicit actions', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const onConfirm = vi.fn();

    render(
      <ConfirmDialog
        open
        title="Delete item"
        message="This item will be removed."
        confirmLabel="Delete"
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    );

    await user.click(screen.getByRole('button', { name: /delete/i }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('moves focus into the confirmation, cancels on Escape, and restores focus to the trigger', async () => {
    const user = userEvent.setup();
    render(<ConfirmHarness />);

    const trigger = screen.getByRole('button', { name: /remove item/i });
    await user.click(trigger);

    const cancelButton = await screen.findByRole('button', { name: /^cancel$/i });
    expect(cancelButton).toHaveFocus();

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByText('This item will be removed.')).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it('keeps Tab inside the confirmation instead of walking the page behind it', async () => {
    const user = userEvent.setup();
    render(<ConfirmHarness />);

    await user.click(screen.getByRole('button', { name: /remove item/i }));
    const cancelButton = await screen.findByRole('button', { name: /^cancel$/i });
    const confirmButton = screen.getByRole('button', { name: /^delete$/i });

    await user.tab();
    expect(confirmButton).toHaveFocus();

    await user.tab();
    expect(cancelButton).toHaveFocus();

    await user.tab({ shift: true });
    expect(confirmButton).toHaveFocus();
  });

  it('leaves Escape inert while the confirmed action is still running', async () => {
    const user = userEvent.setup();
    render(<ConfirmHarness isBusy />);

    await user.click(screen.getByRole('button', { name: /remove item/i }));
    await screen.findByText('This item will be removed.');

    await user.keyboard('{Escape}');

    expect(screen.getByText('This item will be removed.')).toBeInTheDocument();
  });
});

// Function summary: Renders a trigger plus confirmation so dialog focus handoff can be asserted end to end.
function ConfirmHarness({ isBusy = false }: Readonly<{ isBusy?: boolean }>) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Remove item
      </button>
      <ConfirmDialog
        open={open}
        title="Delete item"
        message="This item will be removed."
        confirmLabel="Delete"
        isBusy={isBusy}
        onCancel={() => setOpen(false)}
        onConfirm={() => setOpen(false)}
      />
    </>
  );
}

// Function summary: Renders the SubmitHarness React component and wires its local UI behavior.
function SubmitHarness({ onSubmit }: Readonly<{ onSubmit: () => Promise<void> }>) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    await onSubmit();
    setIsSubmitting(false);
  }

  return (
    <form onSubmit={handleSubmit}>
      <SubmitButton isSubmitting={isSubmitting} idleLabel="Save changes" />
    </form>
  );
}

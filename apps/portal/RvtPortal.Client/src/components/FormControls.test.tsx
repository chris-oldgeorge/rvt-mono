// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog, ErrorSummary, FormField, Notice, SubmitButton } from './FormControls';

describe('FormControls', () => {
  it('renders notices, validation summaries, and field errors', () => {
    render(
      <>
        <Notice tone="success" message="Saved" />
        <ErrorSummary errors={['Email is required']} />
        <FormField label="Email" error="Use a valid email">
          <input aria-label="Email" />
        </FormField>
      </>
    );

    expect(screen.getByText('Saved')).toBeInTheDocument();
    expect(screen.getByText('Email is required')).toBeInTheDocument();
    expect(screen.getByText('Use a valid email')).toBeInTheDocument();
  });

  it('prevents double-submit while a form action is pending', async () => {
    const user = userEvent.setup();
    let resolveSubmit: () => void = () => undefined;
    const submit = vi.fn(() => new Promise<void>((resolve) => {
      resolveSubmit = resolve;
    }));

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
      />
    );

    await user.click(screen.getByRole('button', { name: /delete/i }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });
});

// Function summary: Renders the SubmitHarness React component and wires its local UI behavior.
function SubmitHarness({ onSubmit }: Readonly<{ onSubmit: () => Promise<void> }>) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
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

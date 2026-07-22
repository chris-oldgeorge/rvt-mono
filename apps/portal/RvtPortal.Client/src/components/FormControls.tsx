// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { AlertCircle, CheckCircle2, HelpCircle, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

type NoticeTone = 'success' | 'error' | 'info';

type NoticeProps = Readonly<{
  tone: NoticeTone;
  message: string;
}>;

// Function summary: Renders the Notice React component and wires its local UI behavior.
export function Notice({ tone, message }: NoticeProps) {
  const Icon = noticeIcon(tone);
  return (
    <output className={`notice ${tone}`} role={tone === 'error' ? 'alert' : undefined}>
      <Icon size={18} aria-hidden="true" />
      <span>{message}</span>
    </output>
  );
}

type ErrorSummaryProps = Readonly<{
  errors: ReadonlyArray<string>;
}>;

// Function summary: Renders the ErrorSummary React component and wires its local UI behavior.
export function ErrorSummary({ errors }: ErrorSummaryProps) {
  if (errors.length === 0) {
    return null;
  }

  return (
    <div className="notice error" role="alert">
      <AlertCircle size={18} aria-hidden="true" />
      <div>
        <strong>Check the form</strong>
        <ul>
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      </div>
    </div>
  );
}

type FormFieldProps = Readonly<{
  label: string;
  children: ReactNode;
  error?: string | null;
}>;

// Function summary: Renders the FormField React component and wires its local UI behavior.
export function FormField({ label, children, error }: FormFieldProps) {
  return (
    <label className={`form-field${error ? ' has-error' : ''}`}>
      <span>{label}</span>
      {children}
      {error && <em>{error}</em>}
    </label>
  );
}

type SubmitButtonProps = Readonly<{
  icon?: ReactNode;
  isSubmitting: boolean;
  disabled?: boolean;
  idleLabel: string;
  submittingLabel?: string;
}>;

// Function summary: Renders the SubmitButton React component and wires its local UI behavior.
export function SubmitButton({ icon, isSubmitting, disabled, idleLabel, submittingLabel = 'Saving' }: SubmitButtonProps) {
  return (
    <button className="secondary-button" disabled={disabled || isSubmitting} type="submit">
      {icon}
      <span>{isSubmitting ? submittingLabel : idleLabel}</span>
    </button>
  );
}

type ConfirmDialogProps = Readonly<{
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel?: string;
  isBusy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}>;

// Function summary: Renders the ConfirmDialog React component and wires its local UI behavior.
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel = 'Cancel',
  isBusy = false,
  onCancel,
  onConfirm
}: ConfirmDialogProps) {
  if (!open) {
    return null;
  }

  return (
    <div className="dialog-backdrop">
      <dialog className="confirm-dialog" open aria-modal="true" aria-labelledby="confirm-dialog-title">
        <h2 id="confirm-dialog-title">{title}</h2>
        <p>{message}</p>
        <div className="dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel} disabled={isBusy}>
            {cancelLabel}
          </button>
          <button className="danger-button" type="button" onClick={onConfirm} disabled={isBusy}>
            {isBusy ? 'Working' : confirmLabel}
          </button>
        </div>
      </dialog>
    </div>
  );
}

// Function summary: Handles the notice icon workflow for this module.
function noticeIcon(tone: NoticeTone): LucideIcon {
  if (tone === 'success') {
    return CheckCircle2;
  }
  if (tone === 'error') {
    return AlertCircle;
  }
  return HelpCircle;
}

// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-07-30 pending Opened confirmations with showModal so focus, Escape and background inertness work.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { AlertCircle, CheckCircle2, HelpCircle } from 'lucide-react';
import { useEffect, useRef } from 'react';
import type { KeyboardEvent, ReactNode, SyntheticEvent } from 'react';

type NoticeTone = 'success' | 'error' | 'info';

type NoticeProps = Readonly<{
  tone: NoticeTone;
  message: string;
}>;

// Function summary: Renders the Notice React component and wires its local UI behavior.
export function Notice({ tone, message }: NoticeProps) {
  return (
    <output className={`notice ${tone}`} role={tone === 'error' ? 'alert' : undefined}>
      <NoticeIcon tone={tone} />
      <span>{message}</span>
    </output>
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
export function SubmitButton({
  icon,
  isSubmitting,
  disabled,
  idleLabel,
  submittingLabel = 'Saving',
}: SubmitButtonProps) {
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
  // Extra controls rendered inside the modal, for confirmations that also capture input.
  children?: ReactNode;
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
  children,
  onCancel,
  onConfirm,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement | null>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!open || !dialog) {
      return undefined;
    }

    const trigger = document.activeElement;
    // showModal() is what makes the element a real modal: top layer, ::backdrop, an
    // Escape close request and inert page content behind it. Environments without the
    // dialog element (jsdom) fall back to the open attribute; the handlers below carry
    // the focus and Escape behaviour in both cases.
    if (typeof dialog.showModal === 'function') {
      dialog.showModal();
    } else {
      dialog.open = true;
    }
    focusableElements(dialog)[0]?.focus();

    return () => {
      if (dialog.open && typeof dialog.close === 'function') {
        dialog.close();
      }
      if (isFocusable(trigger) && trigger.isConnected) {
        trigger.focus();
      }
    };
  }, [open]);

  // Function summary: Routes a native dialog close request through the caller's cancel handler.
  function handleCloseRequest(event: SyntheticEvent) {
    event.preventDefault();
    if (!isBusy) {
      onCancel();
    }
  }

  // Function summary: Keeps Tab inside the confirmation and maps Escape onto cancel.
  function handleKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      handleCloseRequest(event);
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }

    const dialog = dialogRef.current;
    const focusable = focusableElements(dialog);
    if (!dialog || focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || !dialog.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  if (!open) {
    return null;
  }

  return (
    <dialog
      className="confirm-dialog"
      ref={dialogRef}
      aria-labelledby="confirm-dialog-title"
      onCancel={handleCloseRequest}
      onKeyDown={handleKeyDown}
    >
      <h2 id="confirm-dialog-title">{title}</h2>
      <p>{message}</p>
      {children}
      <div className="dialog-actions">
        <button className="secondary-button" type="button" onClick={onCancel} disabled={isBusy}>
          {cancelLabel}
        </button>
        <button className="danger-button" type="button" onClick={onConfirm} disabled={isBusy}>
          {isBusy ? 'Working' : confirmLabel}
        </button>
      </div>
    </dialog>
  );
}

type Focusable = Readonly<{ focus: () => void; isConnected: boolean }>;

// Function summary: Lists the tabbable controls inside the confirmation dialog in document order.
function focusableElements(dialog: HTMLDialogElement | null): HTMLElement[] {
  if (!dialog) {
    return [];
  }

  const candidates = dialog.querySelectorAll<HTMLElement>(
    'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  );
  return Array.from(candidates);
}

// Function summary: Reports whether a node can receive focus without depending on DOM globals.
function isFocusable(node: unknown): node is Focusable {
  return typeof (node as Focusable | null)?.focus === 'function';
}

// Function summary: Renders the icon for the supplied notice tone.
function NoticeIcon({ tone }: Readonly<{ tone: NoticeTone }>) {
  if (tone === 'success') {
    return <CheckCircle2 size={18} aria-hidden="true" />;
  }
  if (tone === 'error') {
    return <AlertCircle size={18} aria-hidden="true" />;
  }
  return <HelpCircle size={18} aria-hidden="true" />;
}

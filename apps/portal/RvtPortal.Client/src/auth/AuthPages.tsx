// File summary: Renders the public authentication pages (sign in, password reset, email confirmation).
// Major updates:
// - 2026-07-30 pending Extracted from App.tsx during the shell/page split.

import { CheckCircle2, ChevronLeft, LockKeyhole, Mail, Save } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { SubmitEvent } from 'react';
import { confirmEmail, forgotPassword, login, resetPassword, setInitialPassword } from '../api/client';
import { Notice } from '../components/FormControls';
import { PasswordFields } from './PasswordFields';
import type { AuthStateResponse, ConfirmEmailResponse } from '../dtos';

export type PublicPageProps = Readonly<{
  onNavigate: (path: string) => void;
}>;

type LoginPageProps = PublicPageProps &
  Readonly<{
    onAuthenticated: (auth: AuthStateResponse) => void;
    onForgotPassword: (email: string) => void;
  }>;

// Function summary: Renders the LoginPage React component and wires its local UI behavior.
export function LoginPage({ onAuthenticated, onForgotPassword, onNavigate }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      const nextAuth = await login({ email, password, rememberMe: true });
      onAuthenticated(nextAuth);
      onNavigate('/');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="legacy-login-shell">
      <section className="legacy-login" aria-label="RVT portal sign in">
        <div className="legacy-promo">
          <img className="legacy-logo" src="/images/rvt.png" alt="RVT Group logo" />
          <img className="legacy-promo-image" src="/images/loginPromotion.png" alt="" />
        </div>
        <div className="legacy-form-column">
          <img className="legacy-logo narrow-logo" src="/images/rvt.png" alt="RVT Group logo" />
          <form className="legacy-login-form" onSubmit={handleSubmit}>
            <h1>Please sign in</h1>
            <label className="floating-field first">
              <input
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                type="email"
                placeholder="name@example.com"
                autoComplete="email"
              />
              <span>Email address</span>
            </label>
            <label className="floating-field last">
              <input
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                type="password"
                placeholder="Password"
                autoComplete="current-password"
              />
              <span>Password</span>
            </label>
            <div className="legacy-reset-row">
              <button className="legacy-text-link" type="button" onClick={() => onForgotPassword(email)}>
                Reset your password?
              </button>
            </div>
            {error && <div className="legacy-validation">{error}</div>}
            <button className="legacy-sign-in-button" disabled={isSubmitting} type="submit">
              {isSubmitting ? 'Signing in' : 'Sign in'}
            </button>
            <div className="legacy-contact">
              <h2>No account?</h2>
              <a href="mailto:monitoring@rvtgroup.co.uk" target="_blank" rel="noreferrer">
                Contact us
              </a>
              <span> to set you up on the platform.</span>
            </div>
            <p className="legacy-copyright">&copy; {new Date().getFullYear()} RVT Group Ltd.</p>
          </form>
        </div>
      </section>
    </main>
  );
}

// Function summary: Renders the ForgotPasswordPage React component and wires its local UI behavior.
export function ForgotPasswordPage({
  initialEmail = '',
  onNavigate,
}: PublicPageProps & Readonly<{ initialEmail?: string }>) {
  const [email, setEmail] = useState(initialEmail);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setMessage(null);
    setError(null);
    try {
      const response = await forgotPassword({ email });
      setMessage(response.message);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <Mail size={24} aria-hidden="true" />
          <h1>Reset Password</h1>
        </div>
        <form className="form-grid" onSubmit={handleSubmit}>
          <label className="form-field">
            <span>Email</span>
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" />
          </label>
          {message && <Notice tone="success" message={message} />}
          {error && <Notice tone="error" message={error} />}
          <button className="primary-button" disabled={isSubmitting} type="submit">
            <Mail size={18} aria-hidden="true" />
            <span>{isSubmitting ? 'Sending' : 'Send reset link'}</span>
          </button>
        </form>
      </section>
    </main>
  );
}

// Function summary: Renders the ResetPasswordPage React component and wires its local UI behavior.
export function ResetPasswordPage({ onNavigate }: PublicPageProps) {
  const code = new URLSearchParams(globalThis.location.search).get('code') ?? '';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(code ? null : 'A code must be supplied for password reset.');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setMessage(null);
    setError(null);
    try {
      const response = await resetPassword({ email, password, confirmPassword, code });
      setMessage(response.message);
      setPassword('');
      setConfirmPassword('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <LockKeyhole size={24} aria-hidden="true" />
          <h1>Choose Password</h1>
        </div>
        <form className="form-grid" onSubmit={handleSubmit}>
          <label className="form-field">
            <span>Email</span>
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" />
          </label>
          <PasswordFields
            password={password}
            confirmPassword={confirmPassword}
            onPasswordChange={setPassword}
            onConfirmPasswordChange={setConfirmPassword}
          />
          {message && <Notice tone="success" message={message} />}
          {error && <Notice tone="error" message={error} />}
          <button className="primary-button" disabled={isSubmitting || !code} type="submit">
            <Save size={18} aria-hidden="true" />
            <span>{isSubmitting ? 'Saving' : 'Save password'}</span>
          </button>
        </form>
      </section>
    </main>
  );
}

type ConfirmEmailPageProps = PublicPageProps &
  Readonly<{
    onAuthenticated: (auth: AuthStateResponse) => void;
  }>;

// Function summary: Renders the ConfirmEmailPage React component and wires its local UI behavior.
export function ConfirmEmailPage({ onAuthenticated, onNavigate }: ConfirmEmailPageProps) {
  const params = new URLSearchParams(globalThis.location.search);
  const userId = params.get('userId') ?? '';
  const code = params.get('code') ?? '';
  const parameterError = !userId || !code ? 'A user and confirmation code must be supplied.' : null;
  const confirmationCode = code;
  const [confirmation, setConfirmation] = useState<ConfirmEmailResponse | null>(null);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState(parameterError ? '' : 'Confirming email');
  const [error, setError] = useState<string | null>(parameterError);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (parameterError) {
      return;
    }
    confirmEmail(userId, confirmationCode)
      .then((response) => {
        setConfirmation(response);
        setMessage('Email confirmed');
      })
      .catch((err: Error) => {
        setError(err.message);
        setMessage('');
      });
  }, [confirmationCode, parameterError, userId]);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    if (!confirmation) {
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const nextAuth = await setInitialPassword({
        userId: confirmation.userId,
        code: confirmationCode,
        newPassword: password,
        confirmPassword,
      });
      onAuthenticated(nextAuth);
      onNavigate('/');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <button className="link-button align-left" type="button" onClick={() => onNavigate('/login')}>
          <ChevronLeft size={16} aria-hidden="true" />
          <span>Back to sign in</span>
        </button>
        <div className="auth-heading">
          <CheckCircle2 size={24} aria-hidden="true" />
          <h1>Confirm Email</h1>
        </div>
        {message && <Notice tone="success" message={message} />}
        {error && <Notice tone="error" message={error} />}
        {confirmation && (
          <form className="form-grid" onSubmit={handleSubmit}>
            <label className="form-field">
              <span>Email</span>
              <input value={confirmation.email} readOnly />
            </label>
            <PasswordFields
              password={password}
              confirmPassword={confirmPassword}
              onPasswordChange={setPassword}
              onConfirmPasswordChange={setConfirmPassword}
            />
            <button className="primary-button" disabled={isSubmitting} type="submit">
              <Save size={18} aria-hidden="true" />
              <span>{isSubmitting ? 'Saving' : 'Set password'}</span>
            </button>
          </form>
        )}
      </section>
    </main>
  );
}

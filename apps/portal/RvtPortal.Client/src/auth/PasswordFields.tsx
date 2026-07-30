// File summary: Renders the paired new-password/confirm-password inputs shared by auth and account forms.
// Major updates:
// - 2026-07-30 pending Extracted from App.tsx during the shell/page split.

type PasswordFieldsProps = Readonly<{
  password: string;
  confirmPassword: string;
  onPasswordChange: (value: string) => void;
  onConfirmPasswordChange: (value: string) => void;
}>;

// Function summary: Renders the PasswordFields React component and wires its local UI behavior.
export function PasswordFields({
  password,
  confirmPassword,
  onPasswordChange,
  onConfirmPasswordChange,
}: PasswordFieldsProps) {
  return (
    <>
      <label className="form-field">
        <span>Password</span>
        <input
          value={password}
          onChange={(event) => onPasswordChange(event.target.value)}
          type="password"
          autoComplete="new-password"
        />
      </label>
      <label className="form-field">
        <span>Confirm password</span>
        <input
          value={confirmPassword}
          onChange={(event) => onConfirmPasswordChange(event.target.value)}
          type="password"
          autoComplete="new-password"
        />
      </label>
    </>
  );
}

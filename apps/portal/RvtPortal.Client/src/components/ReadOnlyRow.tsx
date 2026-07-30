// File summary: Renders the shared label/value read-only detail row used across detail panels.
// Major updates:
// - 2026-07-30 pending Consolidated the identical copies from the contract/site and admin panel monoliths.

// Function summary: Renders the ReadOnlyRow React component and wires its local UI behavior.
export function ReadOnlyRow({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="readonly-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

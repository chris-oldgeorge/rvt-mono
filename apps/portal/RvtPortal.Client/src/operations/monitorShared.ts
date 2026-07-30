// File summary: Shares props types, paging constants, and search-reset helpers across the monitor panels.
// Major updates:
// - 2026-07-30 pending Extracted from MonitorPanels.tsx during the monitor panel split.

export const pageSize = 10;
export type ListExecution<TQuery> = Readonly<{ query: TQuery }>;

export type MonitorsPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
  canManage?: boolean;
  canUseInstallerTools?: boolean;
  installerOnly?: boolean;
}>;

// Function summary: Resets the list page alongside a monitor search text change.
export function resetSearchPage(
  value: string,
  setSearchText: (nextValue: string) => void,
  setPage: (nextPage: number) => void,
) {
  setSearchText(value);
  setPage(1);
}

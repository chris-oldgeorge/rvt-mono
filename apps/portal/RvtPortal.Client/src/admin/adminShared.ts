// File summary: Shares props and request-execution types across the company and user admin panels.
// Major updates:
// - 2026-07-30 pending Extracted from AdminPanels.tsx during the companies/users split.

export type AdminPanelCallbacks = Readonly<{
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

export type AdminPanelProps = AdminPanelCallbacks &
  Readonly<{
    locationPath: string;
  }>;

export type LookupExecution = Readonly<{
  query: string;
  companyId?: string;
}>;

export type RequestExecution<T> = Readonly<{
  query: T;
  generation: number;
}>;

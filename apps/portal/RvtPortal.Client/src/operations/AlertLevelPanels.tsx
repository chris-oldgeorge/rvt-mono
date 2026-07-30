// File summary: Renders the monitor alert-level list, form, and vibration threshold panels.
// Major updates:
// - 2026-07-30 pending Rejected blank and non-numeric thresholds instead of saving a 0 that alerts on every reading.
// - 2026-07-30 pending Split from NotificationAlertPanels.tsx so notifications and alert levels live in separate modules.
// - 2026-06-26 pending Added cancellation for notification and alert-level list requests.
// - 2026-06-26 pending Preserved origin-aware Back navigation for notification and alert-level forms.
// - 2026-06-25 pending Hid the Average column for vibration alert-level grids.
// - 2026-06-04 pending Replaced insecure route-parsing fallback URL literals with HTTPS.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { ChevronLeft, Edit3, Plus, Save, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  createAlertLevel,
  deleteAlertLevel,
  getAlertLevel,
  getAlertLevelOptions,
  isAbortError,
  queryAlertLevels,
  updateAlertLevel,
  updateVibrationAlertLevels,
} from '../api/client';
import { DataGrid } from '../components/DataGrid';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { normalizeSortDirection, parsePositiveInt, useGridSortHandler } from '../gridQuery';
import { useRequestLifecycle } from '../requestLifecycle';
import { alertLevelColumnsForMonitorType } from './alertLevelColumns';
import type {
  AlertLevelItem,
  AlertLevelMutationRequest,
  AlertLevelOptionsResponse,
  QueryAlertLevelsRequest,
  QueryAlertLevelsResponse,
  SortDirection,
} from '../dtos';

const pageSize = 10;
type ListExecution<TQuery> = Readonly<{ query: TQuery }>;

type OperationsPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type AlertLevelsPanelProps = Readonly<{
  monitorId: string;
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
  canManage?: boolean;
}>;

type AlertLevelRoute = { kind: 'list' } | { kind: 'new' } | { kind: 'edit'; levelId: string } | { kind: 'vibration' };

// Function summary: Renders the AlertLevelsPanel React component and wires its local UI behavior.
export function AlertLevelsPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage = false,
}: AlertLevelsPanelProps) {
  const route = parseAlertLevelRoute(locationPath);
  if (route.kind === 'new' && canManage) {
    return (
      <AlertLevelForm
        monitorId={monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (route.kind === 'edit' && canManage) {
    return (
      <AlertLevelForm
        levelId={route.levelId}
        monitorId={monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  if (route.kind === 'vibration' && canManage) {
    return (
      <VibrationAlertLevelForm
        monitorId={monitorId}
        locationPath={locationPath}
        onNavigate={onNavigate}
        onRequestError={onRequestError}
      />
    );
  }
  return (
    <AlertLevelsListPanel
      monitorId={monitorId}
      locationPath={locationPath}
      onNavigate={onNavigate}
      onRequestError={onRequestError}
      canManage={canManage}
    />
  );
}

// Function summary: Renders the AlertLevelsListPanel React component and wires its local UI behavior.
function AlertLevelsListPanel({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
  canManage,
}: AlertLevelsPanelProps) {
  const initialParams = useMemo(() => new URL(locationPath, 'https://rvt.local').searchParams, [locationPath]);
  const [response, setResponse] = useState<QueryAlertLevelsResponse | null>(null);
  const [page, setPage] = useState(parsePositiveInt(initialParams.get('page'), 1));
  const [sortKey, setSortKey] = useState(initialParams.get('sort') ?? 'alertField');
  const [sortDir, setSortDir] = useState<SortDirection>(normalizeSortDirection(initialParams.get('sortDir')));
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmDeleteLevel, setConfirmDeleteLevel] = useState<AlertLevelItem | null>(null);
  const [completedExecution, setCompletedExecution] = useState<ListExecution<QueryAlertLevelsRequest> | null>(null);
  const [refreshExecution, setRefreshExecution] = useState<ListExecution<QueryAlertLevelsRequest> | null>(null);
  const { claimRequest, ownsRequest, currentGeneration } = useRequestLifecycle();
  const manageAllowed = Boolean(canManage && response?.canManage);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}`);
  const returnPath = currentRoutePath(locationPath);

  const query = useMemo<QueryAlertLevelsRequest>(
    () => ({
      monitorId,
      page,
      pageSize,
      sort: sortKey,
      sortDir,
    }),
    [monitorId, page, sortDir, sortKey],
  );
  const execution = useMemo<ListExecution<QueryAlertLevelsRequest>>(() => ({ query }), [query]);
  const currentExecution = refreshExecution?.query === query ? refreshExecution : execution;
  const isLoading = completedExecution !== currentExecution;
  const handleSortChange = useGridSortHandler(setSortKey, setSortDir, setPage);

  const refreshAlertLevels = useCallback(async () => {
    const nextExecution: ListExecution<QueryAlertLevelsRequest> = { query };
    const { controller, generation } = claimRequest();
    setRefreshExecution(nextExecution);
    setCompletedExecution(null);
    try {
      const nextResponse = await queryAlertLevels(nextExecution.query, { signal: controller.signal });
      if (!ownsRequest(controller, generation)) {
        return;
      }
      setResponse(nextResponse);
      setError(null);
      setCompletedExecution(nextExecution);
    } catch (err) {
      if (!ownsRequest(controller, generation) || isAbortError(err)) {
        return;
      }
      setError((err as Error).message);
      onRequestError(err);
      setCompletedExecution(nextExecution);
    }
  }, [claimRequest, onRequestError, ownsRequest, query]);

  useEffect(() => {
    const { controller, generation } = claimRequest();
    globalThis.history.replaceState(null, '', buildAlertLevelsUrl(monitorId, { page, sort: sortKey, sortDir }));
    queryAlertLevels(execution.query, { signal: controller.signal })
      .then((nextResponse) => {
        if (!ownsRequest(controller, generation)) {
          return;
        }
        setResponse(nextResponse);
        setError(null);
        setCompletedExecution(execution);
      })
      .catch((err: Error) => {
        if (!ownsRequest(controller, generation) || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedExecution(execution);
      });
    return () => controller.abort();
  }, [claimRequest, execution, monitorId, onRequestError, ownsRequest, page, sortDir, sortKey]);

  async function handleDelete(level: AlertLevelItem) {
    const mutationGeneration = currentGeneration();
    setNotice(null);
    setError(null);
    try {
      await deleteAlertLevel(level.id);
      setNotice('Alert level has been deleted.');
      if (currentGeneration() !== mutationGeneration) {
        return;
      }
      await refreshAlertLevels();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <p>Monitor</p>
          <h2>{response?.fleetNumber || response?.serialId || 'Alert Levels'}</h2>
        </div>
        <div className="button-row">
          <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
            <ChevronLeft size={17} aria-hidden="true" />
            <span>Back</span>
          </button>
          {manageAllowed && response?.typeOfMonitor === 'Vibration' && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/vibration`, returnPath))}
            >
              <Edit3 size={17} aria-hidden="true" />
              <span>Vibration</span>
            </button>
          )}
          {manageAllowed && response?.typeOfMonitor !== 'Vibration' && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/new`, returnPath))}
            >
              <Plus size={17} aria-hidden="true" />
              <span>Add Level</span>
            </button>
          )}
        </div>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      {response?.typeOfMonitor === 'Vibration' && (
        <Notice tone="info" message="Vibration monitors use one Omnidots-backed alert/caution threshold pair." />
      )}
      <DataGrid
        columns={alertLevelColumnsForMonitorType(response?.typeOfMonitor)}
        rows={response?.results ?? []}
        getRowKey={(level) => level.id}
        emptyMessage="No alert levels are configured for this monitor."
        error={error}
        isLoading={isLoading}
        page={page}
        pageSize={pageSize}
        total={response?.total ?? 0}
        totalPages={response?.totalPages ?? 0}
        sortKey={sortKey}
        sortDirection={sortDir}
        onPageChange={setPage}
        onSortChange={handleSortChange}
        rowActions={
          manageAllowed && response?.typeOfMonitor !== 'Vibration'
            ? [
                {
                  label: 'Edit alert level',
                  icon: <Edit3 size={16} aria-hidden="true" />,
                  onClick: (level) =>
                    onNavigate(withReturnTo(`/monitors/${monitorId}/alert-levels/${level.id}/edit`, returnPath)),
                },
                {
                  label: 'Delete alert level',
                  icon: <Trash2 size={16} aria-hidden="true" />,
                  onClick: (level) => setConfirmDeleteLevel(level),
                },
              ]
            : []
        }
      />
      <ConfirmDialog
        open={confirmDeleteLevel !== null}
        title="Delete alert level"
        message={`Delete ${confirmDeleteLevel?.alertType ?? ''} ${confirmDeleteLevel?.alertField ?? ''} alert level?`}
        confirmLabel="Delete"
        onCancel={() => setConfirmDeleteLevel(null)}
        onConfirm={() => {
          const level = confirmDeleteLevel;
          setConfirmDeleteLevel(null);
          if (level) {
            void handleDelete(level);
          }
        }}
      />
    </section>
  );
}

// Function summary: Renders the AlertLevelForm React component and wires its local UI behavior.
function AlertLevelForm({
  monitorId,
  levelId,
  locationPath,
  onNavigate,
  onRequestError,
}: OperationsPanelProps & Readonly<{ monitorId: string; levelId?: string }>) {
  const [options, setOptions] = useState<AlertLevelOptionsResponse | null>(null);
  const [form, setForm] = useState<AlertLevelMutationRequest>(() => emptyAlertLevelForm(monitorId));
  // The limits are edited as text so a partial entry ("5.", "-") survives keystrokes and a
  // blank or unparseable value can be refused on submit instead of collapsing to 0.
  const [limitOnText, setLimitOnText] = useState('');
  const [limitOffText, setLimitOffText] = useState('');
  const [limitOnError, setLimitOnError] = useState<string | null>(null);
  const [limitOffError, setLimitOffError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}/alert-levels`);

  useEffect(() => {
    getAlertLevelOptions(monitorId)
      .then((nextOptions) => {
        setOptions(nextOptions);
        setForm((current) => ({
          ...current,
          alertField: current.alertField || nextOptions.alertFields[0]?.value || '',
          alertType: current.alertType || nextOptions.alertTypes[0]?.value || 'Alert',
          averagingPeriod: current.averagingPeriod || Number(nextOptions.averagingPeriods[0]?.value || 0),
        }));
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [monitorId, onRequestError]);

  useEffect(() => {
    if (!levelId) {
      return;
    }
    getAlertLevel(levelId)
      .then((response) => {
        const level = response.item;
        if (!level) {
          return;
        }
        setForm({
          monitorId: level.monitorId,
          alertField: level.alertField,
          limitOn: level.limitOn,
          limitOff: level.limitOff,
          alertType: level.alertType,
          averagingPeriod: level.averagingPeriod,
          weekdays: level.weekdays,
          saturdays: level.saturdays,
          sundays: level.sundays,
          startTime: level.startTime ?? '',
          endTime: level.endTime ?? '',
        });
        setLimitOnText(String(level.limitOn));
        setLimitOffText(String(level.limitOff));
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [levelId, onRequestError]);

  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    const nextLimitOnError = thresholdError(limitOnText, 'Limit On');
    const nextLimitOffError = thresholdError(limitOffText, 'Limit Off');
    setLimitOnError(nextLimitOnError);
    setLimitOffError(nextLimitOffError);
    if (nextLimitOnError || nextLimitOffError) {
      return;
    }

    const request: AlertLevelMutationRequest = {
      ...form,
      limitOn: Number(limitOnText),
      limitOff: Number(limitOffText),
    };
    setIsSubmitting(true);
    setError(null);
    try {
      if (levelId) {
        await updateAlertLevel(levelId, request);
      } else {
        await createAlertLevel(request);
      }
      onNavigate(backPath);
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Alert Levels</p>
          <h2>{levelId ? 'Edit Alert Level' : 'Add Alert Level'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>Back</span>
        </button>
      </div>
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Parameter">
          <select value={form.alertField} onChange={(event) => setForm({ ...form, alertField: event.target.value })}>
            {options?.alertFields.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Alert Type">
          <select value={form.alertType} onChange={(event) => setForm({ ...form, alertType: event.target.value })}>
            {options?.alertTypes.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Limit On" error={limitOnError}>
          <input
            value={limitOnText}
            inputMode="decimal"
            onChange={(event) => {
              setLimitOnText(event.target.value);
              setLimitOnError(null);
            }}
          />
        </FormField>
        <FormField label="Limit Off" error={limitOffError}>
          <input
            value={limitOffText}
            inputMode="decimal"
            onChange={(event) => {
              setLimitOffText(event.target.value);
              setLimitOffError(null);
            }}
          />
        </FormField>
        <FormField label="Averaging Period">
          <select
            value={form.averagingPeriod}
            onChange={(event) => setForm({ ...form, averagingPeriod: Number(event.target.value) })}
          >
            {options?.averagingPeriods.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </FormField>
        <div className="checkbox-cluster" aria-label="Active days">
          <label className="checkbox-row">
            <input
              checked={form.weekdays}
              type="checkbox"
              onChange={(event) => setForm({ ...form, weekdays: event.target.checked })}
            />
            <span>Weekdays</span>
          </label>
          <label className="checkbox-row">
            <input
              checked={form.saturdays}
              type="checkbox"
              onChange={(event) => setForm({ ...form, saturdays: event.target.checked })}
            />
            <span>Saturday</span>
          </label>
          <label className="checkbox-row">
            <input
              checked={form.sundays}
              type="checkbox"
              onChange={(event) => setForm({ ...form, sundays: event.target.checked })}
            />
            <span>Sunday</span>
          </label>
        </div>
        {options?.typeOfMonitor === 'Noise' && form.averagingPeriod !== 0 && (
          <div className="time-grid">
            <FormField label="Start Time">
              <input
                value={form.startTime ?? ''}
                type="time"
                onChange={(event) => setForm({ ...form, startTime: event.target.value })}
              />
            </FormField>
            <FormField label="End Time">
              <input
                value={form.endTime ?? ''}
                type="time"
                onChange={(event) => setForm({ ...form, endTime: event.target.value })}
              />
            </FormField>
          </div>
        )}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel="Save Alert Level"
        />
      </form>
    </section>
  );
}

// Function summary: Renders the VibrationAlertLevelForm React component and wires its local UI behavior.
function VibrationAlertLevelForm({
  monitorId,
  locationPath,
  onNavigate,
  onRequestError,
}: OperationsPanelProps & Readonly<{ monitorId: string }>) {
  const [alertLevel, setAlertLevel] = useState('');
  const [cautionLevel, setCautionLevel] = useState('');
  const [alertLevelError, setAlertLevelError] = useState<string | null>(null);
  const [cautionLevelError, setCautionLevelError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const backPath = returnToOr(locationPath, `/monitors/${monitorId}/alert-levels`);

  useEffect(() => {
    const controller = new AbortController();
    queryAlertLevels({ monitorId, page: 1, pageSize: 10, sort: 'alertType' }, { signal: controller.signal })
      .then((response) => {
        setAlertLevel(String(response.results.find((level) => level.alertType === 'Alert')?.limitOn ?? ''));
        setCautionLevel(String(response.results.find((level) => level.alertType === 'Caution')?.limitOn ?? ''));
      })
      .catch((err: Error) => {
        if (controller.signal.aborted || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      });
    return () => controller.abort();
  }, [monitorId, onRequestError]);

  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    const nextAlertError = thresholdError(alertLevel, 'Alert Level');
    const nextCautionError = thresholdError(cautionLevel, 'Caution Level');
    setAlertLevelError(nextAlertError);
    setCautionLevelError(nextCautionError);
    if (nextAlertError || nextCautionError) {
      return;
    }

    setIsSubmitting(true);
    setNotice(null);
    setError(null);
    try {
      const response = await updateVibrationAlertLevels(monitorId, {
        alertLevel: Number(alertLevel),
        cautionLevel: Number(cautionLevel),
      });
      // Stay on the form: whether the external sync was attempted is the substance of this
      // response, and navigating away unmounted the component before the notice could render.
      setNotice(response.externalSyncAttempted ? 'Vibration levels saved and synced.' : 'Vibration levels saved.');
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Alert Levels</p>
          <h2>Vibration Thresholds</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          <ChevronLeft size={17} aria-hidden="true" />
          <span>Back</span>
        </button>
      </div>
      {notice && <Notice tone="success" message={notice} />}
      {error && <Notice tone="error" message={error} />}
      <form className="form-grid" onSubmit={handleSubmit}>
        <FormField label="Alert Level" error={alertLevelError}>
          <input
            value={alertLevel}
            inputMode="decimal"
            onChange={(event) => {
              setAlertLevel(event.target.value);
              setAlertLevelError(null);
            }}
          />
        </FormField>
        <FormField label="Caution Level" error={cautionLevelError}>
          <input
            value={cautionLevel}
            inputMode="decimal"
            onChange={(event) => {
              setCautionLevel(event.target.value);
              setCautionLevelError(null);
            }}
          />
        </FormField>
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel="Save Vibration Levels"
        />
      </form>
    </section>
  );
}

// Function summary: Handles the parse alert level route workflow for this module.
function parseAlertLevelRoute(locationPath: string): AlertLevelRoute {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  if (/\/alert-levels\/new$/i.test(path)) {
    return { kind: 'new' };
  }
  if (/\/alert-levels\/vibration$/i.test(path)) {
    return { kind: 'vibration' };
  }
  const editMatch = /\/alert-levels\/([^/]+)\/edit$/i.exec(path);
  if (editMatch) {
    return { kind: 'edit', levelId: editMatch[1] };
  }
  return { kind: 'list' };
}

// Function summary: Builds alert levels url data for callers.
function buildAlertLevelsUrl(
  monitorId: string,
  { page, sort, sortDir }: Readonly<{ page: number; sort: string; sortDir: SortDirection }>,
) {
  const params = new URLSearchParams({ page: String(page), sort, sortDir });
  return `/monitors/${monitorId}/alert-levels?${params.toString()}`;
}

// Function summary: Handles the empty alert level form workflow for this module.
function emptyAlertLevelForm(monitorId: string): AlertLevelMutationRequest {
  return {
    monitorId,
    alertField: '',
    limitOn: 0,
    limitOff: 0,
    alertType: 'Alert',
    averagingPeriod: 0,
    weekdays: true,
    saturdays: false,
    sundays: false,
    startTime: '',
    endTime: '',
  };
}

// Function summary: Validates one alert threshold instead of coercing blanks and typos to zero.
// A 0 threshold is not a missing threshold: it latches on the first reading of every monitor
// it is saved against, so an unparseable value has to be refused rather than defaulted.
function thresholdError(value: string, field: string) {
  if (!value.trim()) {
    return `${field} is required. A blank threshold would alert on every reading.`;
  }

  return Number.isFinite(Number(value)) ? null : `${field} must be a number.`;
}

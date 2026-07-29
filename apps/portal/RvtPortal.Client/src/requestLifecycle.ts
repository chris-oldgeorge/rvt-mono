// File summary: Shared abort-and-supersede lifecycle for list screens that refresh on query changes.
// Four panels carried this byte-identical pair of callbacks plus their two refs.

import { useCallback, useRef } from 'react';

// Function summary: Provides claim/owns callbacks so only the latest request may apply its result.
export function useRequestLifecycle() {
  const activeRequestController = useRef<AbortController | null>(null);
  const requestGeneration = useRef(0);

  const claimRequest = useCallback(() => {
    activeRequestController.current?.abort();
    const controller = new AbortController();
    const generation = requestGeneration.current + 1;
    requestGeneration.current = generation;
    activeRequestController.current = controller;
    return { controller, generation };
  }, []);

  const ownsRequest = useCallback(
    (controller: AbortController, generation: number) =>
      activeRequestController.current === controller &&
      requestGeneration.current === generation &&
      !controller.signal.aborted,
    [],
  );

  // Mutations snapshot the generation before awaiting so they can skip their
  // follow-up refresh when a newer list request superseded them mid-flight.
  const currentGeneration = useCallback(() => requestGeneration.current, []);

  return { claimRequest, ownsRequest, currentGeneration };
}

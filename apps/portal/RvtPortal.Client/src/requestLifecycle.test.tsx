// File summary: Pins the shared abort-and-supersede request lifecycle four panels used to copy.

import { renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useRequestLifecycle } from './requestLifecycle';

describe('useRequestLifecycle', () => {
  it('grants ownership to the latest claim only', () => {
    const { result } = renderHook(() => useRequestLifecycle());

    const first = result.current.claimRequest();
    expect(result.current.ownsRequest(first.controller, first.generation)).toBe(true);

    const second = result.current.claimRequest();
    expect(result.current.ownsRequest(first.controller, first.generation)).toBe(false);
    expect(result.current.ownsRequest(second.controller, second.generation)).toBe(true);
  });

  it('aborts the superseded request', () => {
    const { result } = renderHook(() => useRequestLifecycle());

    const first = result.current.claimRequest();
    expect(first.controller.signal.aborted).toBe(false);

    result.current.claimRequest();
    expect(first.controller.signal.aborted).toBe(true);
  });

  it('denies ownership once the claimed controller is aborted', () => {
    const { result } = renderHook(() => useRequestLifecycle());

    const claim = result.current.claimRequest();
    claim.controller.abort();

    expect(result.current.ownsRequest(claim.controller, claim.generation)).toBe(false);
  });

  it('exposes the generation so mutations can detect being superseded', () => {
    const { result } = renderHook(() => useRequestLifecycle());

    const before = result.current.currentGeneration();
    result.current.claimRequest();

    expect(result.current.currentGeneration()).toBe(before + 1);
  });
});

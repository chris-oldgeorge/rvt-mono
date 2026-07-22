// File summary: Provides reusable React UI components shared across portal screens.
// Major updates:
// - 2026-06-29 pending Covered single-host OSM tiles to avoid partial subdomain tile gaps.
// - 2026-06-29 pending Covered Leaflet CSS import and size invalidation for complete tile rendering.
// - 2026-06-26 pending Covered stable Leaflet map lifecycle when marker arrays are rebuilt with unchanged content.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import { render, waitFor } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MapMonitorMarker } from '../dtos';
import { MonitorMap } from './MonitorMap';

const leafletMocks = vi.hoisted(() => {
  const mapRemove = vi.fn();
  const mapSetView = vi.fn();
  const mapFitBounds = vi.fn();
  const mapInvalidateSize = vi.fn();
  return {
    mapRemove,
    mapSetView,
    mapFitBounds,
    mapInvalidateSize,
    map: vi.fn(() => ({
      setView: mapSetView,
      fitBounds: mapFitBounds,
      invalidateSize: mapInvalidateSize,
      remove: mapRemove
    })),
    tileLayer: vi.fn(() => ({
      addTo: vi.fn()
    })),
    latLngBounds: vi.fn((latLngs: Array<[number, number]>) => ({ latLngs })),
    marker: vi.fn(() => {
      const marker = {
        bindTooltip: vi.fn(() => marker),
        addTo: vi.fn()
      };
      return marker;
    }),
    divIcon: vi.fn((options: unknown) => options)
  };
});

vi.mock('leaflet', () => leafletMocks);

describe('MonitorMap', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', class ResizeObserver {
      observe() {}
      unobserve() {}
      disconnect() {}
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('keeps the Leaflet map mounted when marker content is unchanged', async () => {
    const { rerender } = render(<MonitorMap markers={[markerFixture()]} />);

    await waitFor(() => expect(leafletMocks.map).toHaveBeenCalledTimes(1));

    rerender(<MonitorMap markers={[markerFixture()]} />);
    await nextTask();

    expect(leafletMocks.map).toHaveBeenCalledTimes(1);
    expect(leafletMocks.mapRemove).not.toHaveBeenCalled();
  });

  it('invalidates the Leaflet size after creation so tiles fill the container', async () => {
    render(<MonitorMap markers={[markerFixture()]} />);

    await waitFor(() => expect(leafletMocks.map).toHaveBeenCalledTimes(1));
    await nextTask();

    expect(leafletMocks.mapInvalidateSize).toHaveBeenCalledTimes(1);
  });

  it('loads Leaflet core CSS before application map overrides', () => {
    const sourceDirectory = dirname(fileURLToPath(import.meta.url));
    const mainSource = readFileSync(resolve(sourceDirectory, '../main.tsx'), 'utf8');
    const leafletCssIndex = mainSource.indexOf("import 'leaflet/dist/leaflet.css';");
    const appCssIndex = mainSource.indexOf("import './styles/app.css';");

    expect(leafletCssIndex).toBeGreaterThanOrEqual(0);
    expect(appCssIndex).toBeGreaterThan(leafletCssIndex);
  });

  it('uses the canonical single-host OpenStreetMap tile endpoint', async () => {
    render(<MonitorMap markers={[markerFixture()]} />);

    await waitFor(() => expect(leafletMocks.tileLayer).toHaveBeenCalledTimes(1));

    expect(leafletMocks.tileLayer).toHaveBeenCalledWith(
      'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
      expect.objectContaining({
        attribution: expect.stringContaining('OpenStreetMap')
      })
    );
  });
});

// Function summary: Builds a stable monitor marker fixture for map lifecycle tests.
function markerFixture(): MapMonitorMarker {
  return {
    monitorId: 'monitor-1',
    deploymentId: 'deployment-1',
    latitude: 51.5072,
    longitude: -0.1276,
    typeOfMonitor: 'Noise',
    offline: false,
    alert: false,
    caution: false,
    siteName: 'RVT Test Site',
    fleetNumber: 'RVT-001',
    serialId: 'SER-001',
    lastDataTime: '2026-06-26T12:00:00Z',
    what3words: 'filled.count.soap'
  };
}

// Function summary: Lets dynamic imports and effect follow-up work settle in jsdom tests.
function nextTask() {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

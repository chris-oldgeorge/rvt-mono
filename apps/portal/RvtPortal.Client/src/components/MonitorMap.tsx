// File summary: Renders reusable monitor map views for dashboard, site detail, and monitor detail pages.
// Major updates:
// - 2026-06-29 pending Used single-host OSM tiles to avoid partial subdomain tile gaps.
// - 2026-06-29 pending Invalidated Leaflet size after initialization so tiles fill resized containers.
// - 2026-06-26 pending Stabilized Leaflet lifecycle against marker array identity churn.
// - 2026-06-26 pending Kept helper exports component-only for React fast-refresh linting.
// - 2026-06-09 pending Extracted embedded map rendering for legacy monitor/site detail parity.

import { MapPin } from 'lucide-react';
import { useEffect, useRef } from 'react';
import type { MapMonitorMarker } from '../dtos';

type MonitorMapProps = Readonly<{
  markers: ReadonlyArray<MapMonitorMarker>;
  label?: string;
}>;

// Function summary: Renders a Leaflet-backed monitor map with an accessible fallback pin layer.
export function MonitorMap({ markers, label = 'Leaflet monitor map' }: MonitorMapProps) {
  const mapNode = useRef<HTMLDivElement | null>(null);
  const markerSignature = leafletMarkerSignature(markers);
  const leafletMarkers = useRef(markers);
  const leafletSignature = useRef(markerSignature);

  if (leafletSignature.current !== markerSignature) {
    leafletSignature.current = markerSignature;
    leafletMarkers.current = markers;
  }

  useEffect(() => {
    const currentMarkers = leafletMarkers.current;
    if (currentMarkers.length === 0 || !mapNode.current || globalThis.ResizeObserver === undefined) {
      return undefined;
    }

    let disposed = false;
    let cleanup: (() => void) | null = null;
    import('leaflet')
      .then((leaflet) => {
        if (disposed || !mapNode.current) {
          return;
        }
        const mapElement = mapNode.current;
        const map = leaflet.map(mapElement, { scrollWheelZoom: false });
        const resizeObserver = new ResizeObserver(() => map.invalidateSize());
        const latLngs = currentMarkers.map((marker) => [marker.latitude, marker.longitude] as [number, number]);
        map.setView(averageLatLng(currentMarkers), mapZoomLevel(currentMarkers.length));
        leaflet
          .tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
          })
          .addTo(map);
        const bounds = leaflet.latLngBounds(latLngs);
        currentMarkers.forEach((marker) => {
          leaflet
            .marker([marker.latitude, marker.longitude], {
              icon: leaflet.divIcon({
                className: `leaflet-dashboard-pin ${markerStatusClass(marker)}`,
                html: '<span></span>',
                iconSize: [24, 24],
                iconAnchor: [12, 12]
              })
            })
            .bindTooltip(markerLabel(marker))
            .addTo(map);
        });
        if (currentMarkers.length > 1) {
          map.fitBounds(bounds, { padding: [28, 28] });
        }
        resizeObserver.observe(mapElement);
        window.setTimeout(() => {
          if (!disposed) {
            map.invalidateSize();
          }
        }, 0);
        cleanup = () => {
          resizeObserver.disconnect();
          map.remove();
        };
      })
      .catch(() => {
        cleanup = null;
      });

    return () => {
      disposed = true;
      cleanup?.();
    };
  }, [markerSignature]);

  return (
    <div className="map-shell">
      <div className="leaflet-map" ref={mapNode} aria-label={label} />
      <div className="map-pin-layer" aria-label="Monitor marker overview">
        {markers.map((marker) => (
          <span className={`map-pin ${markerStatusClass(marker)}`} style={markerPosition(marker, markers)} key={marker.deploymentId}>
            <MapPin size={20} aria-hidden="true" />
            <span className="sr-only">{markerLabel(marker)}</span>
          </span>
        ))}
      </div>
      {markers.length === 0 && <div className="map-empty">No visible monitor markers.</div>}
    </div>
  );
}

// Function summary: Builds a semantic map-marker key so Leaflet is not recreated for unchanged marker content.
function leafletMarkerSignature(markers: ReadonlyArray<MapMonitorMarker>) {
  return JSON.stringify(markers.map((marker) => [
    marker.monitorId,
    marker.deploymentId,
    marker.latitude,
    marker.longitude,
    marker.typeOfMonitor,
    marker.offline,
    marker.alert,
    marker.caution,
    marker.fleetNumber,
    marker.serialId
  ]));
}

// Function summary: Renders a compact list of monitor map markers.
export function MonitorMarkerList({ markers }: Readonly<{ markers: ReadonlyArray<MapMonitorMarker> }>) {
  if (markers.length === 0) {
    return null;
  }

  return (
    <div className="map-marker-list" aria-label="Map marker list">
      {markers.map((marker) => (
        <div className="detail-item" key={marker.deploymentId}>
          <span>{marker.siteName || 'Site'}</span>
          <strong>{markerLabel(marker)}</strong>
          <em>{markerSubtitle(marker)}</em>
        </div>
      ))}
    </div>
  );
}

// Function summary: Builds display text for monitor map markers.
function markerLabel(marker: MapMonitorMarker) {
  return `${marker.fleetNumber || marker.serialId} (${marker.typeOfMonitor})`;
}

// Function summary: Handles the marker subtitle workflow for this module.
function markerSubtitle(marker: MapMonitorMarker) {
  const parts = [formatDateTime(marker.lastDataTime) || 'No data'];
  if (marker.what3words) {
    parts.push(marker.what3words);
  }

  return parts.join(' / ');
}

// Function summary: Maps zoom level into the shape required by callers.
function mapZoomLevel(markerCount: number) {
  return markerCount === 1 ? 13 : 8;
}

// Function summary: Handles the marker status class workflow for this module.
function markerStatusClass(marker: MapMonitorMarker) {
  if (marker.alert) {
    return 'danger';
  }
  if (marker.caution) {
    return 'warning';
  }
  if (marker.offline) {
    return 'muted';
  }

  return 'success';
}

// Function summary: Handles the average lat lng workflow for this module.
function averageLatLng(markers: ReadonlyArray<MapMonitorMarker>): [number, number] {
  const total = markers.reduce((current, marker) => ({
    lat: current.lat + marker.latitude,
    lng: current.lng + marker.longitude
  }), { lat: 0, lng: 0 });
  return [total.lat / markers.length, total.lng / markers.length];
}

// Function summary: Handles the marker position workflow for this module.
function markerPosition(marker: MapMonitorMarker, markers: ReadonlyArray<MapMonitorMarker>) {
  const latitudes = markers.map((item) => item.latitude);
  const longitudes = markers.map((item) => item.longitude);
  const minLat = Math.min(...latitudes);
  const maxLat = Math.max(...latitudes);
  const minLng = Math.min(...longitudes);
  const maxLng = Math.max(...longitudes);
  return {
    left: `${rangePosition(marker.longitude, minLng, maxLng)}%`,
    top: `${100 - rangePosition(marker.latitude, minLat, maxLat)}%`
  };
}

// Function summary: Handles the range position workflow for this module.
function rangePosition(value: number, min: number, max: number) {
  if (Math.abs(max - min) < 0.000001) {
    return 50;
  }

  return 8 + ((value - min) / (max - min)) * 84;
}

// Function summary: Formats date/time values for marker subtitles.
function formatDateTime(value?: string | null) {
  return value
    ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
    : '';
}

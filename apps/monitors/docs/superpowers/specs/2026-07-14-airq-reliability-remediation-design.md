# AirQ Reliability Remediation Design

## Goal

Bring AirQ's operational behaviour in line with the preferred monitor design: controlled local runs, truthful one-shot job outcomes, and resilient catalogue imports.

## Design

- `testlocal=true` enables an AirQ-specific monitor filter. It requires `AirQ__TestLocal__SerialId`; a missing value fails startup rather than polling every active AirQ monitor. The filter applies to both catalogue writes and database monitor reads.
- AirQ ingestion handlers continue recording each monitor-level error, complete the remaining monitors, and then throw `AggregateException`. The shared `MonitorHost` converts that exception into one-shot exit code `1`.
- A valid empty AirQ metadata array is treated as absent optional metadata. Catalogue processing still writes that monitor and continues with the remaining records.

## Boundaries

`AirQTestLocalMonitorFilter` owns only testlocal selection. `AirQMonitorReader` and `StoreMonitorsHandler` consume it. The existing `IDBClient` remains only the composition-facade compatibility type; handlers retain narrow query/command dependencies.

## Verification

Focused unit tests cover the filter, missing target validation, aggregate failure propagation, and empty metadata. The complete AirQ suite runs against the existing local PostgreSQL fixture configuration.

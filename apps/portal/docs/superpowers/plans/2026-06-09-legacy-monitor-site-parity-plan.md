# Legacy Monitor and Site Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the tester-confirmed legacy gaps for monitor details and site details in the React SPA.

**Architecture:** Extend the existing SPA API contracts/controllers rather than adding parallel endpoints. Reuse existing `/data`, `/maps`, `/calendar`, and `/notifications` capabilities where possible, while adding missing monitor-detail summary data and monitor picture upload/display support.

**Tech Stack:** .NET, EF Core, React, TypeScript, Vitest, xUnit, Leaflet, Plane.

---

## Scope

- Add richer monitor detail data: latest reading, average reading, battery level, monitor notes, deployment summary, and latest breach drill-through metadata.
- Add monitor picture upload and rendering using the existing `Deployment.PictureLink` field.
- Reuse the current Leaflet map implementation for embedded site/monitor context.
- Add site-detail parity links/sections for map, data, calendar, and notifications instead of duplicating entire global pages.
- Keep changes compatible with both SQL Server and PostgreSQL providers.

## Tasks

### Task 1: Track Work In Plane

- [x] Create a focused cycle named `SPA Legacy Parity - Monitor and Site Details`.
- [x] Create issue `[SPA-PARITY.1] Monitor detail latest readings and deployment summary`.
- [x] Create issue `[SPA-PARITY.2] Monitor picture upload and display`.
- [x] Create issue `[SPA-PARITY.3] Embed map context on monitor and site details`.
- [x] Create issue `[SPA-PARITY.4] Site detail legacy workflow links for data, calendar, and notifications`.
- [x] Create issue `[SPA-PARITY.5] Legacy list/status column parity`.

### Task 2: Add Failing Tests

- [x] Extend `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs` to assert monitor detail returns latest metrics, deployment summary, picture link, and notification IDs.
- [x] Extend `RvtPortal.Client/src/App.test.tsx` to assert monitor detail renders latest reading cards, deployment summary, image, map affordance, and notification view actions.
- [x] Extend `RvtPortal.Client/src/App.test.tsx` to assert site detail exposes map/data/calendar/notification actions.

### Task 3: API Implementation

- [x] Extend `MonitorDetailResponse` with latest metric DTOs, deployment summary DTO, notes, and upload action metadata.
- [x] Add a multipart monitor picture upload endpoint that validates admin access, active deployment, file type, and size.
- [x] Persist uploaded pictures under the existing SPA static file root and store a stable relative link in `Deployment.PictureLink`.
- [x] Populate detail metrics from existing measurement tables with provider-safe EF queries or existing data-source helpers.

### Task 4: SPA Implementation

- [x] Render monitor latest metric cards above the identity grid.
- [x] Render deployment summary and monitor notes sections.
- [x] Render monitor picture if `pictureLink` is present and add upload control on edit when a deployment exists.
- [x] Add view actions to recent notifications that navigate to `/notifications/{id}`.
- [x] Add map/data/calendar/notifications action buttons to site details, pre-filtered by current site or deployment where supported.

### Task 5: Verification and Documentation

- [x] Run focused backend tests for monitor/site workflows.
- [x] Run focused frontend tests.
- [x] Run the full SPA test suite against the current database configuration.
- [x] Update `project_state.md` with implemented parity state and any known follow-up gaps.
- [x] Update Plane issue comments/statuses with verification evidence.

## Follow-Up

- Latest reading now prefers live measurement data and falls back to the latest notification only when no measurement row is available.
- Latest average is populated through the existing monitor data source for Dust, Noise, and Vibration rows where data exists.
- Latest battery is populated from known vendor status tables where battery data exists.
- Verification completed in `/private/tmp/rvtportal-spa-alpha-codegraph-work` because the mounted Windows repository still has external file locks on build props/NuGet files.

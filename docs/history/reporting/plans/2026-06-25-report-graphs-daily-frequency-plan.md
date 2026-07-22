# Report Graphs And Daily Frequency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add report-period graphs with active alert limit lines to generated reports, and add Daily as a selectable Report Rule Wizard frequency.

**Architecture:** The reporting service will hydrate existing monitor average buckets from Timescale/Postgres, group them by monitor type and average period, generate compact SVG line charts, and embed those charts in QuestPDF. The SPA backend will expose Daily in the existing report-rule options/validation path, and the React wizard will treat Daily as a no-extra-schedule frequency.

**Tech Stack:** .NET 10, Npgsql, QuestPDF SVG rendering, xUnit, React/TypeScript, Vitest, Plane API.

---

### Task 1: Plane And Planning

**Files:**
- Create: `docs/superpowers/plans/2026-06-25-report-graphs-daily-frequency-plan.md`
- Modify: Plane issues `#422`, `#423`, `#424`

- [x] **Step 1: Create Plane items**

Created and linked to `Reporting Service Migration`:

- `#422` / `b007a774-31bf-431c-ad58-f28fa0ae0914`: `[ID12] Report-period monitor graphs`
- `#423` / `e3c886a2-de51-48a4-b5f7-023754968555`: `[ID13] Alert limit lines on report graphs`
- `#424` / `d189a9c4-c7e5-4b56-a095-38ac269ea887`: `[ID14] Daily report option in Report Rule Wizard`

- [ ] **Step 2: Update Plane after implementation**

Patch each issue description with files changed and verification evidence, then move completed items to Done.

### Task 2: Reporting Graph Model And SVG Builder

**Files:**
- Modify: `src/Rvt.Reporting.Core/Models/ReportModels.cs`
- Modify: `src/Rvt.Reporting.Pdf/Documents/QuestPdfReportRenderer.cs`
- Test: `tests/Rvt.Reporting.Core.Tests/Reports/ReportGraphTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests that define the graph API from current monitor data:

```csharp
[Fact]
public void BuildReportGraphs_GroupsNoiseDailyAveragesAcrossMonitors()
{
    var site = new SiteReportData
    {
        Monitors =
        [
            new MonitorReportData
            {
                SerialId = "N1",
                FleetNumber = "Noise 1",
                TypeOfMonitor = MonitorType.Noise,
                NoiseDailyAverage = [new MeasurementPoint(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), 54m)]
            },
            new MonitorReportData
            {
                SerialId = "N2",
                FleetNumber = "Noise 2",
                TypeOfMonitor = MonitorType.Noise,
                NoiseDailyAverage = [new MeasurementPoint(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), 58m)]
            }
        ]
    };

    var graphs = QuestPdfReportRenderer.BuildReportGraphs(site);

    var graph = Assert.Single(graphs, item => item.Title == "Noise Daily Averages");
    Assert.Equal("dB", graph.Unit);
    Assert.Equal(2, graph.Series.Count);
}

[Fact]
public void BuildReportGraphs_AddsMatchingAlertLimitLines()
{
    var monitor = new MonitorReportData
    {
        SerialId = "N1",
        TypeOfMonitor = MonitorType.Noise,
        NoiseDailyAverage = [new MeasurementPoint(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), 54m)],
        AlertRules = [new AlertRuleData(AlertType.Alert, "NoiseDailyAverage", 70m, 86400, "dB", "Daily Average", 0)]
    };

    var graph = Assert.Single(QuestPdfReportRenderer.BuildReportGraphs(new SiteReportData { Monitors = [monitor] }));

    var limit = Assert.Single(graph.Limits);
    Assert.Equal(70m, limit.Value);
    Assert.Equal("Alert 70 dB", limit.Label);
}
```

- [ ] **Step 2: Run red tests**

Run:

```bash
dotnet test tests/Rvt.Reporting.Core.Tests/Rvt.Reporting.Core.Tests.csproj --filter ReportGraphTests -v minimal
```

Expected: fail because `ReportGraphTests`/`BuildReportGraphs` do not exist.

- [ ] **Step 3: Implement graph records and builder**

Add records such as `ReportGraph`, `ReportGraphSeries`, and `ReportGraphLimit`, then implement `BuildReportGraphs(SiteReportData)` in `QuestPdfReportRenderer`.

- [ ] **Step 4: Render SVG graphs in PDFs**

Add a `Graphs` section before the monitor detail cards. Generate one SVG per graph using `SvgImage.FromText(...)`; include axes, legend, monitor series, and dashed horizontal limit lines.

- [ ] **Step 5: Run green tests**

Run:

```bash
dotnet test tests/Rvt.Reporting.Core.Tests/Rvt.Reporting.Core.Tests.csproj --filter ReportGraphTests -v minimal
```

Expected: pass.

### Task 3: Reporting Repository Average Hydration

**Files:**
- Modify: `src/Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs`
- Modify: `tests/Rvt.Reporting.Service.Tests/TimescaleSchemaIntegrationTests.cs`

- [ ] **Step 1: Write failing tests**

Extend schema integration tests to require the average source objects/columns discovered in local Timescale and add a live test that seeds or reads one point when the schema has data.

- [ ] **Step 2: Run red tests**

Run live Timescale tests with `RVT_REPORTING_TIMESCALE_TEST_CONNECTION` set.

- [ ] **Step 3: Implement average point loading**

Populate:

- `DustHourlyAverage`
- `DustDailyAverage`
- `NoiseHourlyAverage`
- `NoiseDailyAverage`
- `NoiseSiteAverage`
- `VibrationDailyPeak`

Filter all points to `fromUtc <= measured_at <= toUtc`, with report periods capped by the request dates.

### Task 4: Daily Report Rule Option

**Files:**
- Modify: `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtportal-spa-alpha/RvtPortal.Spa/Api/ReportRulesController.cs`
- Modify: `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtportal-spa-alpha/RvtPortal.Client/src/operations/ReportPanels.tsx`
- Test: `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtportal-spa-alpha/RvtPortal.Spa.Tests/ReportWorkflowTests.cs`
- Test: `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtportal-spa-alpha/RvtPortal.Client/src/App.test.tsx`

- [ ] **Step 1: Write failing backend test**

Add a test that `GET /api/report-rules/options` includes `{ value: "1", label: "Daily" }` and `POST /api/report-rules` accepts `Frequency = ReportFrequencyType.Daily` without day fields.

- [ ] **Step 2: Write failing frontend test**

Add a React test that the Report Rule Wizard frequency dropdown includes `Daily` and selecting Daily hides both `Day of Week` and `Day of Month`.

- [ ] **Step 3: Implement backend**

Add `ReportFrequencyType.Daily` to `SupportedFrequencies`; keep validation so Daily does not require day fields.

- [ ] **Step 4: Implement frontend**

Add `dailyFrequency = 1`; ensure helper functions return false for Daily and `formatRuleSchedule` returns `Daily`.

### Task 5: Verification And Documentation

**Files:**
- Modify: `project_state.md`
- Modify: Plane issues `#422`, `#423`, `#424`

- [ ] **Step 1: Run reporting verification**

Run:

```bash
dotnet test Rvt.Reporting.New.slnx -v minimal
dotnet build Rvt.Reporting.New.slnx --configuration Release -v minimal
git diff --check
```

- [ ] **Step 2: Run SPA verification**

Run in the Windows VM or mounted repo:

```powershell
dotnet test RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj -v minimal
npm run test:run -- src/App.test.tsx
npm run build
```

- [ ] **Step 3: Update docs and Plane**

Update `project_state.md` with implemented files, verification results, Plane issue ids, and any schema/data-source follow-up. Move completed Plane issues to Done with evidence.


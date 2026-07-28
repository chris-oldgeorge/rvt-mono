# RVT Source Import Manifest

| Module path | Source repository | Branch | Pinned revision |
| --- | --- | --- | --- |
| `apps/monitors` | `https://github.com/chris-oldgeorge/rvt-monitors.git` | `main` | `5935f40614073afa6c4ef954db1308a72a5f8f2b` |
| `apps/portal` | `https://github.com/chris-oldgeorge/rvtportal-spa-alpha.git` | `master` | `8355070f094a591297c9f8468057f44a6c876986` |
| `libs/rvt-monitor-common` | `https://github.com/RVT-Group-LTD/rvt-reporting.git` | `main` | `f00d5b8a320945ed08e248da8641ca0c3f7e3b82` |
| `services/reporting` (retired) | `https://github.com/chris-oldgeorge/rvt-reporting-new.git` | `main` | `e602e8317e35bd94a1eb4dd017759b91713ea111` |

Source snapshots are imported as file content only. Source `.git` metadata is
excluded so this repository retains one fresh Git history.

The `services/reporting` import was retired on 2026-07-28. It had become a
stale duplicate of the reporting code in `apps/monitors/reportingmonitor`, and
that module is now the single authoritative implementation. The row is retained
because it records where the imported content originated; the path no longer
exists in this repository. See the
[reporting consolidation record](../modules/reporting/migration-notes.md).

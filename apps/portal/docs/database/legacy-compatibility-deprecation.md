# Legacy Compatibility Deprecation Plan

Generated: 2026-06-09. **Removed: 2026-07-14.**

## Status: retired

The compatibility layer is gone. The scripts that created the `legacy` schema —
`database/{postgres,sqlserver}/legacy_compatibility_views.sql` — were deleted on 2026-07-14, and
`database/{postgres,sqlserver}/drop_legacy_compatibility_views.sql` removes the schema from any database that
still carries it.

This document is kept as the record of what the old names were and what they map to. The table below is the
canonical answer to "what was `AirQNoiseLevels` called after the rename?", and it stays useful long after the
views themselves are gone.

### What this means if something breaks

Nothing in this repository read the `legacy` schema — a guardrail test
(`CutoverReadinessTests.ApplicationCode_DoesNotReferenceLegacyCompatibilitySchema`) has enforced that since the
cutover, and it still does. But the layer existed for consumers *outside* this repository, and the repository
cannot see them. If a report or dashboard breaks with "relation does not exist", find the old name in the table
below and repoint the query at the canonical relation. Do not recreate the views: a second guardrail
(`LegacyCompatibilityLayer_IsRetiredAndCannotBeRecreated`) now fails the build if the create scripts come back.

The removal was made ahead of the original 90-day window (which ran to roughly 2026-09-07) at RVT engineering's
direction. The create scripts remain in git history if the layer ever has to be restored.

## Ownership And Rules (as they stood)

- Owner: RVT engineering.
- Scope: old relation and column names that were renamed by the DBR canonical naming registry.
- Read-only policy: PostgreSQL revoked insert/update/delete on `legacy`; SQL Server denied insert/update/delete on `SCHEMA::[legacy]`.
- Application rule: RVT Portal, SPA, API, migrator, and monitors must not read from `legacy`; new code must use canonical names only. **This rule outlives the layer.**
- ASP.NET Identity objects were excluded because their physical names are intentionally unchanged.

## Removal Plan

1. ~~Create the compatibility views immediately after the canonical rename scripts during cutover.~~ Done, 2026-06-09.
2. ~~Inventory any external/reporting users that still query old names.~~ None identified in this repository.
3. ~~Assign an owner and migration date to each external consumer.~~ No consumers identified.
4. ~~Remove the compatibility scripts and `legacy` schema after consumer sign-off.~~ Done, 2026-07-14.

## Provider Scripts

- PostgreSQL: `database/postgres/drop_legacy_compatibility_views.sql` — drops the `legacy` schema.
- SQL Server: `database/sqlserver/drop_legacy_compatibility_views.sql` — drops the `[legacy]` schema.

The superseded create scripts (`legacy_compatibility_views.sql`, which built views such as `legacy."Sites"` and
`[legacy].[Sites]`) are in git history as of the commit that removed them.

## Compatibility Objects

| Old relation | Canonical relation | Notes |
| --- | --- | --- |
| `AdminDashboardData` | `admin_dashboard_data` | Temporary read-only view. |
| `AirQErrorMessages` | `air_q_error_message` | Temporary read-only view. |
| `AirQMonitorStatus` | `air_q_monitor_status` | Temporary read-only view. |
| `AirQNoise8HourAverage` | `air_q_noise_8_hour_average` | Temporary read-only view. |
| `AirQNoiseLevels` | `air_q_noise_level` | Temporary read-only view. |
| `Companies` | `company` | Temporary read-only view. |
| `CompanySearch` | `company_search` | Temporary read-only view. |
| `Contracts` | `contract` | Temporary read-only view. |
| `ContractSearch` | `contract_search` | Temporary read-only view. |
| `CustomerDashboardMonitorData` | `customer_dashboard_monitor_data` | Temporary read-only view. |
| `CustomerDashboardNotificationData` | `customer_dashboard_notification_data` | Temporary read-only view. |
| `Deployments` | `deployment` | Temporary read-only view. |
| `ErrorLog` | `error_log` | Temporary read-only view. |
| `HelpArticles` | `help_article` | Temporary read-only view. |
| `HelpAssets` | `help_asset` | Temporary read-only view. |
| `HelpSections` | `help_section` | Temporary read-only view. |
| `MonitorCurrentSearch` | `monitor_current_search` | Temporary read-only view. |
| `MonitorSearch` | `monitor_search` | Temporary read-only view. |
| `MonitorsList` | `monitor` | Temporary read-only view. |
| `MonitorUserSearch` | `monitor_user_search` | Temporary read-only view. |
| `MyAtmAccessoryInfo` | `my_atm_accessory_info` | Temporary read-only view. |
| `MyAtmDustLevel8hourAvg` | `my_atm_dust_level_8_hour_avg` | Temporary read-only view. |
| `MyAtmDustLevels` | `my_atm_dust_level` | Temporary read-only view. |
| `MyAtmErrorMessages` | `my_atm_error_message` | Temporary read-only view. |
| `NoiseLevel15minAvg` | `noise_level_15_min_avg` | Temporary read-only view. |
| `NoiseLevel1dayAvg` | `noise_level_1_day_avg` | Temporary read-only view. |
| `NoiseLevel1hourAvg` | `noise_level_1_hour_avg` | Temporary read-only view. |
| `NoiseLevelSiteAvg` | `noise_level_site_avg` | Temporary read-only view. |
| `Notifications` | `notification` | Temporary read-only view. |
| `NotificationSearch` | `notification_search` | Temporary read-only view. |
| `NotificationSettings` | `notification_setting` | Temporary read-only view. |
| `NotificationsSent` | `notification_sent` | Temporary read-only view. |
| `NotificationUserSearch` | `notification_user_search` | Temporary read-only view. |
| `OmnidotsErrorMessages` | `omnidots_error_message` | Temporary read-only view. |
| `OmnidotsMonitorStatus` | `omnidots_monitor_status` | Temporary read-only view. |
| `OmnidotsPeakLevel15min` | `omnidots_peak_level_15_min` | Temporary read-only view. |
| `OmnidotsPeakLevel1dayPeak` | `omnidots_peak_level_1_day_peak` | Temporary read-only view. |
| `OmnidotsPeakLevel1min` | `omnidots_peak_level_1_min` | Temporary read-only view. |
| `OmnidotsPeakLevel20min` | `omnidots_peak_level_20_min` | Temporary read-only view. |
| `OmnidotsPeakLevel5min` | `omnidots_peak_level_5_min` | Temporary read-only view. |
| `OmnidotsPeakLevels` | `omnidots_peak_level` | Temporary read-only view. |
| `OmnidotsSensors` | `omnidots_sensor` | Temporary read-only view. |
| `OmnidotsTraces` | `omnidots_trace` | Temporary read-only view. |
| `OmnidotsTracesIndex` | `omnidots_trace_index` | Temporary read-only view. |
| `OmnidotsVdvLevels` | `omnidots_vdv_level` | Temporary read-only view. |
| `OmnidotsVeffLevels` | `omnidots_veff_level` | Temporary read-only view. |
| `ReportRules` | `report_rule` | Temporary read-only view. |
| `ReportRuleSearch` | `report_rule_search` | Temporary read-only view. |
| `ReportRuleUserSearch` | `report_rule_user_search` | Temporary read-only view. |
| `Reports` | `report` | Temporary read-only view. |
| `ReportSearch` | `report_search` | Temporary read-only view. |
| `ReportsErrorMessages` | `report_error_message` | Temporary read-only view. |
| `ReportsSent` | `report_sent` | Temporary read-only view. |
| `ReportUsers` | `report_user` | Temporary read-only view. |
| `ReportUserSearch` | `report_user_search` | Temporary read-only view. |
| `RvtAlertRules` | `rvt_alert_rule` | Temporary read-only view. |
| `SiteArchived` | `site_archived` | Temporary read-only view. |
| `SiteAverages` | `site_average` | Temporary read-only view. |
| `SiteOperatingHours` | `site_operating_hour` | Temporary read-only view. |
| `Sites` | `site` | Temporary read-only view. |
| `SiteSearch` | `site_search` | Temporary read-only view. |
| `SiteUsers` | `site_user` | Temporary read-only view. |
| `SiteUserSearch` | `site_user_search` | Temporary read-only view. |
| `SvantekMonitorStatus` | `svantek_monitor_status` | Temporary read-only view. |
| `SvantekNoise8HourAverage` | `svantek_noise_8_hour_average` | Temporary read-only view. |
| `SvantekNoiseLevels` | `svantek_noise_level` | Temporary read-only view. |
| `UserActionsHistory` | `user_action_history` | Temporary read-only view. |
| `UserSearch` | `user_search` | Temporary read-only view. |
| `UsersForReportSearch` | `users_for_report_search` | Temporary read-only view. |
| `UsersForSiteSearch` | `users_for_site_search` | Temporary read-only view. |

-- Canonical constraint/index naming migration draft for PostgreSQL/TimescaleDB.
-- ASP.NET Identity tables are intentionally excluded from this refactor.
-- Run after canonical_database_naming.sql during a reviewed rehearsal.
BEGIN;
SET LOCAL lock_timeout = '5s';

-- Constraint and index renames.
ALTER TABLE IF EXISTS "public"."air_q_monitor_status" RENAME CONSTRAINT "PK__AirQMoni__5E5B3EE492A9E513" TO "pk_air_q_monitor_status";
ALTER INDEX IF EXISTS "public"."AirQNoise8HourAverage_SampleTime_idx" RENAME TO "ix_air_q_noise_8_hour_average_sample_time";
ALTER INDEX IF EXISTS "public"."AirQNoiseLevels_SampleTime_idx" RENAME TO "ix_air_q_noise_level_sample_time";
ALTER TABLE IF EXISTS "public"."company" RENAME CONSTRAINT "PK_Companies" TO "pk_company";
ALTER TABLE IF EXISTS "public"."contract" RENAME CONSTRAINT "FK_Contracts_Companies_CompanyId" TO "fk_contract_company_id";
ALTER TABLE IF EXISTS "public"."contract" RENAME CONSTRAINT "FK_Contracts_Sites_SiteiD" TO "fk_contract_site_id";
ALTER TABLE IF EXISTS "public"."contract" RENAME CONSTRAINT "PK_Contracts" TO "pk_contract";
ALTER TABLE IF EXISTS "public"."deployment" RENAME CONSTRAINT "FK_Deployments_Contracts_ContractId" TO "fk_deployment_contract_id";
ALTER TABLE IF EXISTS "public"."deployment" RENAME CONSTRAINT "FK_Deployments_MonitorsList_MonitorId" TO "fk_deployment_monitor_id";
ALTER TABLE IF EXISTS "public"."deployment" RENAME CONSTRAINT "PK2_Deployments" TO "pk_deployment";
ALTER INDEX IF EXISTS "public"."ErrorLog_Timestamtp_idx" RENAME TO "ix_error_log_logged_at";
ALTER TABLE IF EXISTS "public"."error_log" RENAME CONSTRAINT "pk_errorlog" TO "pk_error_log";
ALTER TABLE IF EXISTS "public"."help_article" RENAME CONSTRAINT "FK_HelpArticles_HelpSections_SectionId" TO "fk_help_article_help_section_id";
ALTER INDEX IF EXISTS "public"."IX_HelpArticles_SectionId" RENAME TO "ix_help_article_help_section_id";
ALTER INDEX IF EXISTS "public"."IX_HelpArticles_Slug" RENAME TO "ix_help_article_slug";
ALTER TABLE IF EXISTS "public"."help_article" RENAME CONSTRAINT "PK_HelpArticles" TO "pk_help_article";
ALTER TABLE IF EXISTS "public"."help_asset" RENAME CONSTRAINT "FK_HelpAssets_HelpArticles_HelpArticleId" TO "fk_help_asset_help_article_id";
ALTER INDEX IF EXISTS "public"."IX_HelpAssets_HelpArticleId" RENAME TO "ix_help_asset_help_article_id";
ALTER TABLE IF EXISTS "public"."help_asset" RENAME CONSTRAINT "PK_HelpAssets" TO "pk_help_asset";
ALTER INDEX IF EXISTS "public"."IX_HelpSections_Slug" RENAME TO "ix_help_section_slug";
ALTER TABLE IF EXISTS "public"."help_section" RENAME CONSTRAINT "PK_HelpSections" TO "pk_help_section";
ALTER TABLE IF EXISTS "public"."monitor" RENAME CONSTRAINT "PK_MonitorsList" TO "pk_monitor";
ALTER INDEX IF EXISTS "public"."MyAtmDustLevels_SampleTime_idx" RENAME TO "ix_my_atm_dust_level_sample_time";
ALTER TABLE IF EXISTS "public"."notification_setting" RENAME CONSTRAINT "FK_NotificationSettings_SiteUsers" TO "fk_notification_setting_site_user_id";
ALTER TABLE IF EXISTS "public"."notification_setting" RENAME CONSTRAINT "PK_NotificationSettings" TO "pk_notification_setting";
ALTER TABLE IF EXISTS "public"."notification" RENAME CONSTRAINT "PK_Notifications" TO "pk_notification";
ALTER INDEX IF EXISTS "public"."NotificationsSent_SendTime_idx" RENAME TO "ix_notification_sent_send_time";
ALTER TABLE IF EXISTS "public"."notification_sent" RENAME CONSTRAINT "pk_notificationssent" TO "pk_notification_sent";
ALTER TABLE IF EXISTS "public"."omnidots_monitor_status" RENAME CONSTRAINT "PK__Omnidots__3214EC0736C6B626" TO "pk_omnidots_monitor_status";
ALTER INDEX IF EXISTS "public"."IX_OmnidotsPeakLevels_SerialId_SampleTimeDesc" RENAME TO "ix_omnidots_peak_level_serial_id_sample_time";
ALTER INDEX IF EXISTS "public"."OmnidotsPeakLevels_SampleTime_idx" RENAME TO "ix_omnidots_peak_level_sample_time";
ALTER INDEX IF EXISTS "public"."_dta_index_OmnidotsPeakLevels_25_1957582012__K2_K1_3_4_5_6_7_8_" RENAME TO "ix_omnidots_peak_level_sample_time_serial_id";
ALTER TABLE IF EXISTS "public"."omnidots_sensor" RENAME CONSTRAINT "PK__Omnidots__3214EC0704E3BD04" TO "pk_omnidots_sensor";
ALTER TABLE IF EXISTS "public"."omnidots_trace" RENAME CONSTRAINT "FK__OmnidotsT__Trace__2DE6D218" TO "fk_omnidots_trace_omnidots_trace_index_id";
ALTER TABLE IF EXISTS "public"."omnidots_trace_index" RENAME CONSTRAINT "PK__Omnidots__3214EC071EF1F55E" TO "pk_omnidots_trace_index";
ALTER TABLE IF EXISTS "public"."report_rule" RENAME CONSTRAINT "PK_ReportConfig" TO "pk_report_rule";
ALTER TABLE IF EXISTS "public"."report_user" RENAME CONSTRAINT "PK_ReportUsers" TO "pk_report_user";
ALTER TABLE IF EXISTS "public"."report" RENAME CONSTRAINT "PK_Reports" TO "pk_report";
ALTER TABLE IF EXISTS "public"."report_sent" RENAME CONSTRAINT "PK_ReportsSent" TO "pk_report_sent";
ALTER TABLE IF EXISTS "public"."rvt_alert_rule" RENAME CONSTRAINT "FK_RvtAlertRules_MonitorsList_MonitorId" TO "fk_rvt_alert_rule_monitor_id";
ALTER TABLE IF EXISTS "public"."rvt_alert_rule" RENAME CONSTRAINT "PK_RvtAlertRules" TO "pk_rvt_alert_rule";
ALTER TABLE IF EXISTS "public"."site_archived" RENAME CONSTRAINT "FK_SiteArchived_Sites_SiteId" TO "fk_site_archived_site_id";
ALTER TABLE IF EXISTS "public"."site_archived" RENAME CONSTRAINT "PK_SiteArchived" TO "pk_site_archived";
ALTER INDEX IF EXISTS "public"."SiteAverages_CollectionTime_idx" RENAME TO "ix_site_average_collection_time";
ALTER TABLE IF EXISTS "public"."site_average" RENAME CONSTRAINT "pk_siteaverages" TO "pk_site_average";
ALTER TABLE IF EXISTS "public"."site_operating_hour" RENAME CONSTRAINT "FK_SiteOperatingHours_Sites_SiteId" TO "fk_site_operating_hour_site_id";
ALTER INDEX IF EXISTS "public"."IX_SiteOperatingHours_SiteId_DayOfWeek" RENAME TO "ix_site_operating_hour_site_id_day_of_week";
ALTER TABLE IF EXISTS "public"."site_operating_hour" RENAME CONSTRAINT "PK_SiteOperatingHours" TO "pk_site_operating_hour";
ALTER TABLE IF EXISTS "public"."site_user" RENAME CONSTRAINT "FK_SiteUsers_Sites_SiteId" TO "fk_site_user_site_id";
ALTER TABLE IF EXISTS "public"."site_user" RENAME CONSTRAINT "PK_SiteUsers" TO "pk_site_user";
ALTER TABLE IF EXISTS "public"."site" RENAME CONSTRAINT "PK_Sites" TO "pk_site";
ALTER INDEX IF EXISTS "public"."SvantekNoise8HourAverage_SampleTime_idx" RENAME TO "ix_svantek_noise_8_hour_average_sample_time";
ALTER INDEX IF EXISTS "public"."SvantekNoiseLevels_SampleTime_idx" RENAME TO "ix_svantek_noise_level_sample_time";
ALTER INDEX IF EXISTS "public"."UserActionsHistory_Timestamp_idx" RENAME TO "ix_user_action_history_recorded_at";
ALTER TABLE IF EXISTS "public"."user_action_history" RENAME CONSTRAINT "pk_useractionshistory" TO "pk_user_action_history";

COMMIT;

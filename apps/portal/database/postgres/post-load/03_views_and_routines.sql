-- File summary: Deploys canonical PostgreSQL views after data load so the migrator can rebuild search, dashboard, report, and aggregate read models.
-- Deploy canonical PostgreSQL views, functions, and procedures after data load.
-- Generated from database/sqlserver/canonical_view_module_rewrite.sql during the DBR canonical schema gate.
-- Keep this file and sibling post-load scripts in canonical lowercase names.
-- ASP.NET Identity objects are intentionally excluded from the database naming refactor.
-- Major updates:
-- - 2026-06-09 pending Fixed unterminated view statements and Identity UUID casts found during Timescale timing rehearsal.

SET search_path TO public;

-- View: public.admin_dashboard_data
DROP VIEW IF EXISTS public.admin_dashboard_data CASCADE;
CREATE OR REPLACE VIEW public.admin_dashboard_data AS 
 	--  ,(CASE   WHEN COALESCE(last_data_time_1_min ,'1970-01-01') > CURRENT_TIMESTAMP - interval '1 hour'  THEN 0  ELSE 1   END)::boolean as off_line
SELECT  count(*) as nr,
		case 
			when fleet_nr is null then 'New' 
			when (D.contract_id is null and fleet_nr is not null) then 'NotUsed'
			when offline IS TRUE AND end_date IS NULL then 'Offline'
			when offline IS FALSE AND end_date IS NULL then 'Online'
			else 'Other' 
		END as monitor_state 
  FROM public.monitor M
  left join public.deployment D on D.monitor_id = M.id and D.end_date is null
  Group By
  		case 
			when fleet_nr is null then 'New' 
			when (D.contract_id is null and fleet_nr is not null)then 'NotUsed'
			when offline IS TRUE AND end_date IS NULL then 'Offline'
			when offline IS FALSE AND end_date IS NULL then 'Online'
			else 'Other' 
		END;

-- View: public.air_q_noise_level_1_day_avg
DROP VIEW IF EXISTS public.air_q_noise_level_1_day_avg CASCADE;
CREATE OR REPLACE VIEW public.air_q_noise_level_1_day_avg AS 
  SELECT serial_id
      , CAST(sample_time as date) as sample_time  
      ,avg(laeq) as laeq
      ,avg(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,avg(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10  
  FROM public.air_q_noise_level   
  Group by  serial_id, CAST(sample_time as date);

-- View: public.air_q_noise_level_1_hour_avg
DROP VIEW IF EXISTS public.air_q_noise_level_1_hour_avg CASCADE;
CREATE OR REPLACE VIEW public.air_q_noise_level_1_hour_avg AS 
 SELECT serial_id
      ,COALESCE(date_trunc('hour', sample_time) + interval '1 hour',CURRENT_TIMESTAMP) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg(laeq) as laeq
      ,avg(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,avg(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10  
  FROM public.air_q_noise_level
  Group by serial_id, date_trunc('hour', sample_time) + interval '1 hour';

-- View: public.air_q_noise_level_site_avg
DROP VIEW IF EXISTS public.air_q_noise_level_site_avg CASCADE;
CREATE OR REPLACE VIEW public.air_q_noise_level_site_avg AS
SELECT
    m.serial_id,
    a.collection_time AS sample_time,
    max(a.level) FILTER (WHERE a.field = 'LAeq') AS laeq,
    max(a.level) FILTER (WHERE a.field = 'LAmax') AS lamax,
    max(a.level) FILTER (WHERE a.field = 'LA90') AS la_90,
    max(a.level) FILTER (WHERE a.field = 'LA10') AS la_10,
    max(a.level) FILTER (WHERE a.field = 'LCeq') AS lceq,
    max(a.level) FILTER (WHERE a.field = 'LCmax') AS lcmax,
    max(a.level) FILTER (WHERE a.field = 'LC90') AS lc_90,
    max(a.level) FILTER (WHERE a.field = 'LC10') AS lc_10
FROM public.site_average a
INNER JOIN public.monitor m ON m.id = a.monitor_id
GROUP BY m.serial_id, a.collection_time;

-- View: public.company_search
DROP VIEW IF EXISTS public.company_search CASCADE;
CREATE OR REPLACE VIEW public.company_search AS 
                            SELECT   C.id
                                  ,company_name
 	                              ,COALESCE(U.user_count,0) as user_count
	                              ,s.sites
	                              ,con.contracts

                               FROM public.company C
                             left join (Select Count(*) as user_count, "CompanyId" as company_id from  public."AspNetUsers" group by "CompanyId") as U     on C.id = U.company_id
                             left join (SELECT STRING_AGG(site_name, ', ') as sites, company_id as company_id    FROM (SELECT Distinct    company_id  ,site_id  ,S.site_name as site_name   FROM  public.contract C  join public.site S on S.id =C.site_id ) t  Group by company_id ) s  on s.company_id=C.id
                             left join ( SELECT STRING_AGG(contract_number, ', ') as contracts, company_id as company_id   FROM public.contract Group by company_id)  con  on con.company_id = C.id;

-- View: public.contract_search
DROP VIEW IF EXISTS public.contract_search CASCADE;
CREATE OR REPLACE VIEW public.contract_search AS 
                        SELECT   C.id
                              ,contract_number
                              ,off_hire_date
                              ,on_hire_date
	                          ,company_id
                              ,site_id
	                          ,Comp.company_name
	                          ,site_name
	                          ,concat_ws(' ', S.site_name, S.address_line_1, S.address_line_2, S.postcode, S.city, S.county) as site_address
                          FROM public.contract C
                         left join public.company Comp on comp.id=C.company_id 
                         left join public.site S on S.id=C.site_id;

-- View: public.customer_dashboard_monitor_data
DROP VIEW IF EXISTS public.customer_dashboard_monitor_data CASCADE;
CREATE OR REPLACE VIEW public.customer_dashboard_monitor_data AS 
SELECT  count(*) as nr,
		case 
			when fleet_nr is null then 'New' 
			when contract_number is null then 'NotUsed'
			when offline IS TRUE AND D.end_date IS NULL then 'Offline'
			when offline IS FALSE AND D.end_date IS NULL then 'Online'
			else 'Other' 
		END as monitor_state,
        SU.user_id
  FROM public.monitor M
  left join public.deployment D on D.monitor_id = M.id and D.end_date is null
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  join  public.site_user SU on SU.site_id = S.id
  WHERE fleet_nr IS NOT NULL AND C.site_id IS NOT NULL
 Group By
  		case 
            when fleet_nr is null then 'New' 
			when contract_number is null then 'NotUsed'
			when offline IS TRUE AND D.end_date IS NULL then 'Offline'
			when offline IS FALSE AND D.end_date IS NULL then 'Online'
			else 'Other' 
		END,
        SU.user_id;

-- View: public.customer_dashboard_notification_data
DROP VIEW IF EXISTS public.customer_dashboard_notification_data CASCADE;
CREATE OR REPLACE VIEW public.customer_dashboard_notification_data AS 
SELECT  count(*) as nr,
		N.alert_type,
		case 
			when closed_time IS NULL then 'Open'
			else 'Closed' 
		END as alert_state,
        SU.user_id
  FROM public.notification N

  left join public.monitor M on N.monitor_id = M.id
--  left join public.deployment D on D.monitor_id = M.id and D.end_date is null -- THIS END DATE PART IS WRONG
  left join public.deployment D on D.monitor_id = M.id and N.notification_time between D.start_date and COALESCE(D.end_date , CURRENT_TIMESTAMP)
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  join  public.site_user SU on SU.site_id = S.id
  WHERE fleet_nr IS NOT NULL AND C.site_id IS NOT NULL
 Group By
  		N.alert_type,
		case 
			when closed_time IS NULL then 'Open'
			else 'Closed' 
		END,
        SU.user_id;

-- View: public.monitor_current_search
DROP VIEW IF EXISTS public.monitor_current_search CASCADE;
CREATE OR REPLACE VIEW public.monitor_current_search AS 
SELECT M.id
      ,D.id as deployment_id
      ,fleet_nr
      ,serial_id
      --,manufacturer
      --,model
      --,firmware_version
      ,type_of_monitor
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,(CASE   WHEN COALESCE(M.battery_status ,0)>0  THEN 1  ELSE 0   END)::boolean as battery
	  ,(CASE   WHEN COALESCE(alerts.nr,0)>0  THEN 1  ELSE 0   END)::boolean as alerts
	  ,(CASE   WHEN COALESCE(cautions.nr,0)>0  THEN 1  ELSE 0   END)::boolean as cautions
       ,location_id 
	  , D.lat as latitude
	  , D.lng as longitude 
 
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
       --,location_address
      --,time_zone
      --,customer_id
      --,customer_display_name

      --,listed_at_time
	  ,CASE   
		  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,last_data_time_1_min as last_data_time
      --,last_data_time_15_min
      --,last_data_time_1_hour
      --,last_data_time_24_hour
  FROM public.monitor M
  left join public.deployment D on D.monitor_id = M.id and D.end_date is null
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT CURRENT_TIMESTAMP - (averaging_period * interval '1 second')  as offline_time ,alert_type  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 2   ORDER BY averaging_period DESC LIMIT 1) as offlines on offlines.alert_type = 2;

-- View: public.monitor_report
DROP VIEW IF EXISTS public.monitor_report CASCADE;
CREATE OR REPLACE VIEW public.monitor_report AS 

SELECT M.id
	  ,(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END)::boolean as active
	  ,D.id as deployment_id
      ,fleet_nr
      ,serial_id
      --,manufacturer
      --,model
      --,firmware_version
      ,type_of_monitor
	  ,(CASE   WHEN COALESCE(last_data_time_1_min ,'1970-01-01') > offline_time or  COALESCE(last_data_time_15_min ,'1970-01-01') > offline_time    THEN 0  ELSE 1   END)::boolean as off_line
	  ,(CASE   WHEN COALESCE(alerts.nr,0)>0  THEN 1  ELSE 0   END)::boolean as alerts
	  ,(CASE   WHEN COALESCE(cautions.nr,0)>0  THEN 1  ELSE 0   END)::boolean as cautions
       ,location_id 
	  , D.lat as latitude
	  , D.lng as longitude 
      , D.location
	  ,D.start_date
	  ,D.end_date
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
      ,M.calibration_date
       --,location_address
      --,time_zone
      --,customer_id
      --,customer_display_name

      --,listed_at_time
	  ,CASE   
		  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,last_data_time_1_min as last_data_time
      --,last_data_time_15_min
      --,last_data_time_1_hour
      --,last_data_time_24_hour
  FROM public.monitor M
  left join public.deployment D on D.monitor_id = M.id --and D.end_date is null
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT CURRENT_TIMESTAMP - (averaging_period * interval '1 second')  as offline_time ,alert_type  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 2   ORDER BY averaging_period DESC LIMIT 1) as offlines on offlines.alert_type = 2;

-- View: public.monitor_search
DROP VIEW IF EXISTS public.monitor_search CASCADE;
CREATE OR REPLACE VIEW public.monitor_search AS 

SELECT M.id  
	  ,(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END)::boolean as active
	  ,D.id as deployment_id
      ,fleet_nr
      ,M.serial_id as serial_id
	  ,(CASE  WHEN type_of_monitor=2 THEN COALESCE(OS.name, M.customer_display_name) ELSE Null END) as monitor_name
      --,manufacturer
      --,model
      --,firmware_version
      ,type_of_monitor
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,(CASE   WHEN COALESCE(M.battery_status ,0)>0  THEN 1  ELSE 0   END)::boolean as battery
	  ,(CASE   WHEN COALESCE(alerts.nr,0)>0  THEN 1  ELSE 0   END)::boolean as alerts
	  ,(CASE   WHEN COALESCE(cautions.nr,0)>0  THEN 1  ELSE 0   END)::boolean as cautions
       ,location_id 
	  , D.lat as latitude
	  , D.lng as longitude 
	  ,D.end_date
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
       --,location_address
      --,time_zone
      --,customer_id
      --,customer_display_name

      --,listed_at_time
	  ,CASE   
		  WHEN  D.end_date is not null  THEN D.end_date  
		  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,last_data_time_1_min as last_data_time
      --,last_data_time_15_min
      --,last_data_time_1_hour
      --,last_data_time_24_hour
  FROM public.monitor M
  left join  public.omnidots_sensor OS on OS.serial_id = M.serial_id
  left join public.deployment D on D.monitor_id = M.id --and D.end_date is null
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT CURRENT_TIMESTAMP - (averaging_period * interval '1 second')  as offline_time ,alert_type  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 2   ORDER BY averaging_period DESC LIMIT 1) as offlines on offlines.alert_type = 2;

-- View: public.monitor_user_search
DROP VIEW IF EXISTS public.monitor_user_search CASCADE;
CREATE OR REPLACE VIEW public.monitor_user_search AS 
SELECT M.id	
	  ,(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END)::boolean as active
	  ,D.id as deployment_id
      ,fleet_nr
      ,serial_id
      --,manufacturer
      --,model
      --,firmware_version
      ,type_of_monitor
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,(CASE   WHEN COALESCE(M.battery_status ,0)>0  THEN 1  ELSE 0   END)::boolean as battery
	  ,(CASE   WHEN COALESCE(alerts.nr,0)>0  THEN 1  ELSE 0   END)::boolean as alerts
	  ,(CASE   WHEN COALESCE(cautions.nr,0)>0  THEN 1  ELSE 0   END)::boolean as cautions
      ,location_id 
	  , D.lat as latitude
	  , D.lng as longitude 
	  ,D.what_3_words
	  ,C.contract_number
	  ,C.site_id
	  ,site_name
	  ,SU.user_id
       --,location_address
      --,time_zone
      --,customer_id
      --,customer_display_name

      --,listed_at_time
      --,last_data_time_1_min as last_data_time
	  ,CASE   
		 WHEN  D.end_date is not null  THEN D.end_date  
		 WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,last_data_time_15_min
      --,last_data_time_1_hour
      --,last_data_time_24_hour

  FROM public.monitor M
  left join public.deployment D on D.monitor_id = M.id --and D.end_date is null
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  join  public.site_user SU on SU.site_id = S.id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT CURRENT_TIMESTAMP - (averaging_period * interval '1 second')  as offline_time ,alert_type  FROM public.rvt_alert_rule   where   is_active IS TRUE and alert_type = 2   ORDER BY averaging_period DESC LIMIT 1) as offlines on offlines.alert_type = 2;

-- View: public.my_atm_dust_level_1_day_avg
DROP VIEW IF EXISTS public.my_atm_dust_level_1_day_avg CASCADE;
CREATE OR REPLACE VIEW public.my_atm_dust_level_1_day_avg AS 

SELECT D.serial_id
	  ,De.id as deployment_id
      ,avrg
      ,sample_time
      ,pm_1
      ,pm_2_5
      ,pm_10
      ,pm_total
  FROM public.my_atm_dust_level D
 join public.monitor m on m.serial_id = D.serial_id
 join public.deployment De on De.monitor_id =  m.id 
 where avrg = 86400 and D.sample_time between  De.start_date and COALESCE(De.end_date, CURRENT_TIMESTAMP);

-- View: public.my_atm_dust_level_8_hour_avg
DROP VIEW IF EXISTS public.my_atm_dust_level_8_hour_avg CASCADE;
CREATE OR REPLACE VIEW public.my_atm_dust_level_8_hour_avg AS 
 SELECT serial_id
      ,COALESCE(date_trunc('day', sample_time) + ((floor(extract(hour from sample_time) / 8) + 1) * interval '8 hours'),CURRENT_TIMESTAMP) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg(pm_1) as pm_1
      ,avg(pm_2_5) as pm_2_5
      ,avg(pm_10) as pm_10
      ,avg(pm_total) as pm_total  
  FROM public.my_atm_dust_level
  where avrg = 60 
  Group by serial_id, date_trunc('day', sample_time) + ((floor(extract(hour from sample_time) / 8) + 1) * interval '8 hours');

-- View: public.noise_level_15_min_avg
DROP VIEW IF EXISTS public.noise_level_15_min_avg CASCADE;
CREATE OR REPLACE VIEW public.noise_level_15_min_avg AS 
 
	SELECT serial_id
		  ,sample_time
		  ,laeq
		  ,lamax
		  ,la_90
		  ,la_10
		  ,lceq
		  ,lcmax
		  ,lc_90
		  ,lc_10
	  FROM public.air_q_noise_level
 
 UNION all

  SELECT serial_id
      ,date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 15) * interval '15 minutes')  as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg(laeq) as laeq
      ,max(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,max(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10  
  FROM public.svantek_noise_level
  Group by serial_id, date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 15) * interval '15 minutes');

-- View: public.noise_level_1_day_avg
DROP VIEW IF EXISTS public.noise_level_1_day_avg CASCADE;
CREATE OR REPLACE VIEW public.noise_level_1_day_avg AS 
  SELECT serial_id
      , CAST(sample_time as date) as sample_time  
      ,avg(laeq) as laeq
      ,max(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,max(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10  
  FROM public.air_q_noise_level   
  Group by  serial_id, CAST(sample_time as date)

Union all

  SELECT serial_id
      , CAST(sample_time as date) as sample_time  
      ,avg(laeq) as laeq
      ,max(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,max(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10  
  FROM public.svantek_noise_level   
  Group by  serial_id, CAST(sample_time as date);

-- View: public.noise_level_1_hour_avg
DROP VIEW IF EXISTS public.noise_level_1_hour_avg CASCADE;
CREATE OR REPLACE VIEW public.noise_level_1_hour_avg AS 
 SELECT serial_id
      ,COALESCE(date_trunc('hour', sample_time) + interval '1 hour',CURRENT_TIMESTAMP) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg(laeq) as laeq
      ,max(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,max(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10   
  FROM public.air_q_noise_level
  Group by serial_id, date_trunc('hour', sample_time) + interval '1 hour'
 
 UNION all

  SELECT serial_id
      ,COALESCE(date_trunc('hour', sample_time) + interval '1 hour',CURRENT_TIMESTAMP) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg(laeq) as laeq
      ,max(lamax) as lamax
      ,avg(la_90) as la_90
      ,avg(la_10) as la_10  
      ,avg(lceq) as lceq
      ,max(lcmax) as lcmax
      ,avg(lc_90) as lc_90
      ,avg(lc_10) as lc_10 
  FROM public.svantek_noise_level
  Group by serial_id, date_trunc('hour', sample_time) + interval '1 hour';

-- View: public.noise_level_site_avg
DROP VIEW IF EXISTS public.noise_level_site_avg CASCADE;
CREATE OR REPLACE VIEW public.noise_level_site_avg AS
SELECT
    m.serial_id,
    a.collection_time AS sample_time,
    max(a.level) FILTER (WHERE a.field = 'LAeq') AS laeq,
    max(a.level) FILTER (WHERE a.field = 'LAmax') AS lamax,
    max(a.level) FILTER (WHERE a.field = 'LA90') AS la_90,
    max(a.level) FILTER (WHERE a.field = 'LA10') AS la_10,
    max(a.level) FILTER (WHERE a.field = 'LCeq') AS lceq,
    max(a.level) FILTER (WHERE a.field = 'LCmax') AS lcmax,
    max(a.level) FILTER (WHERE a.field = 'LC90') AS lc_90,
    max(a.level) FILTER (WHERE a.field = 'LC10') AS lc_10
FROM public.site_average a
INNER JOIN public.monitor m ON m.id = a.monitor_id
GROUP BY m.serial_id, a.collection_time;

-- View: public.notification_search
DROP VIEW IF EXISTS public.notification_search CASCADE;
CREATE OR REPLACE VIEW public.notification_search AS 
SELECT N.id
      ,M.id as monitor_id
      ,fleet_nr
      ,M.serial_id
      ,type_of_monitor
	  ,N.alert_type as alert_type
      ,N.closed_time as closed_date
      ,N.alert_field
      ,N.limit_on
      ,N.level
      ,N.notification_time
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
	  ,CASE   
		  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
  FROM public.notification N
  left join public.monitor M on N.monitor_id = M.id
  join public.deployment D on D.monitor_id = M.id and N.notification_time between D.start_date and COALESCE(D.end_date , CURRENT_TIMESTAMP)
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id;

-- View: public.notification_user_search
DROP VIEW IF EXISTS public.notification_user_search CASCADE;
CREATE OR REPLACE VIEW public.notification_user_search AS 
SELECT N.id
      ,fleet_nr
      ,M.id as monitor_id
      ,M.serial_id
      ,type_of_monitor
	  ,N.alert_type as alert_type
      ,N.closed_time as closed_date
      ,N.alert_field
      ,N.limit_on
      ,N.level
      ,N.notification_time
	  ,C.id as contract_id
	  ,C.contract_number
	  ,C.site_id
	  ,site_name
	  ,SU.user_id
	  ,CASE   
		  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
  FROM public.notification N

  left join public.monitor M on N.monitor_id = M.id
  left join public.deployment D on D.monitor_id = M.id and N.notification_time between D.start_date and COALESCE(D.end_date , CURRENT_TIMESTAMP)
  left join public.contract C on C.id = D.contract_id 
  left join public.site S on S.id = C.site_id
  join  public.site_user SU on SU.site_id = S.id;
   --UNION
  --SELECT M.id
  --    ,fleet_nr
  --    ,M.serial_id
  --    ,type_of_monitor
	 -- ,(CASE   WHEN COALESCE(last_data_time_1_min ,'1970-01-01') > public.fnOfflineDateTime() or  COALESCE(last_data_time_15_min ,'1970-01-01') > public.fnOfflineDateTime()    THEN 0  ELSE 1   END)::boolean as off_line

  --    ,NULL as closed_date
  --    ,'' as alert_field
  --    ,NULL as limit_on
  --    ,NULL as level
  --    ,CASE   
		--  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		--  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		--  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		--  ELSE  last_data_time_24_hour  
		--END as notification_time
	 -- ,C.id as contract_id
	 -- ,C.contract_number
	 -- ,C.site_id
	 -- ,site_name
	 -- ,SU.user_id
	 -- ,CASE   
		--  WHEN  last_data_time_1_min is not null  THEN last_data_time_1_min  
		--  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		--  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		--  ELSE  last_data_time_24_hour  
		--END as last_data_time
  --FROM public.monitor M
  --left join public.deployment D on D.monitor_id = M.id and D.end_date is null
  --left join public.contract C on C.id = D.contract_id 
  --left join public.site S on S.id = C.site_id
  --join  public.site_user SU on SU.site_id = S.id
  --left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   alert_type = 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  --left join  (SELECT count(*) as nr ,monitor_id  FROM public.rvt_alert_rule   where   alert_type = 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id;

-- View: public.omnidots_peak_level_15_min
DROP VIEW IF EXISTS public.omnidots_peak_level_15_min CASCADE;
CREATE OR REPLACE VIEW public.omnidots_peak_level_15_min AS

 SELECT  
	serial_id,
	COALESCE(date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 15) * interval '15 minutes'),CURRENT_TIMESTAMP) as sample_time,
    max(x_vtop) as x_vtop,
    max(y_vtop) as y_vtop,
    max(z_vtop) as z_vtop
 FROM 
	public.omnidots_peak_level
GROUP BY 
    serial_id, date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 15) * interval '15 minutes');

-- View: public.omnidots_peak_level_1_day_peak
DROP VIEW IF EXISTS public.omnidots_peak_level_1_day_peak CASCADE;
CREATE OR REPLACE VIEW public.omnidots_peak_level_1_day_peak AS 
	SELECT  serial_id
		  , CAST(sample_time as date) as sample_time  
		  --,sample_time
		  --,x_fdom
		  ,MAX(x_vtop) as x_vtop
		  --,Max(x_vtop_overflow) as x_vtop
		  --,y_fdom
		  ,MAX(y_vtop) as y_vtop
		  --,MAX(y_vtop_overflow) as y_vtop
		  --,z_fdom
		  ,MAX(z_vtop) as z_vtop
		  --,MAX(z_vtop_overflow)  as z_vtop
	   FROM public.omnidots_peak_level
	   Group by  serial_id, CAST(sample_time as date);

-- View: public.omnidots_peak_level_1_min
DROP VIEW IF EXISTS public.omnidots_peak_level_1_min CASCADE;
CREATE OR REPLACE VIEW public.omnidots_peak_level_1_min AS

 SELECT  
    serial_id,
    COALESCE(date_trunc('minute', sample_time),CURRENT_TIMESTAMP) as sample_time,
    max(x_vtop) as x_vtop,
    max(y_vtop) as y_vtop,
    max(z_vtop) as z_vtop
 FROM 
	public.omnidots_peak_level
GROUP BY 
     serial_id, date_trunc('minute', sample_time);

-- View: public.omnidots_peak_level_20_min
DROP VIEW IF EXISTS public.omnidots_peak_level_20_min CASCADE;
CREATE OR REPLACE VIEW public.omnidots_peak_level_20_min AS

 SELECT  
	serial_id,
    COALESCE(date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 20) * interval '20 minutes'),CURRENT_TIMESTAMP) as sample_time,
    max(x_vtop) as x_vtop,
    max(y_vtop) as y_vtop,
    max(z_vtop) as z_vtop
 FROM 
	public.omnidots_peak_level
GROUP BY 
    serial_id, date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 20) * interval '20 minutes');

-- View: public.omnidots_peak_level_5_min
DROP VIEW IF EXISTS public.omnidots_peak_level_5_min CASCADE;
CREATE OR REPLACE VIEW public.omnidots_peak_level_5_min AS

 SELECT  
    serial_id,
    COALESCE(date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 5) * interval '5 minutes'),CURRENT_TIMESTAMP) as sample_time,
    max(x_vtop) as x_vtop,
    max(y_vtop) as y_vtop,
    max(z_vtop) as z_vtop
 FROM 
	public.omnidots_peak_level
GROUP BY 
     serial_id, date_trunc('hour', sample_time) + (floor(extract(minute from sample_time) / 5) * interval '5 minutes');

-- View: public.omnidots_read_status
DROP VIEW IF EXISTS public.omnidots_read_status CASCADE;
CREATE OR REPLACE VIEW public.omnidots_read_status AS 
		WITH cte AS
		(
			SELECT serial_id,sample_time,
			ROW_NUMBER() OVER (PARTITION BY serial_id ORDER BY sample_time DESC) AS rn
			FROM public.omnidots_peak_level where serial_id in (
				SELECT serial_id   FROM public.monitor  M
				join public.deployment D on D.monitor_id=M.id
				where type_of_monitor =2 )
		)
		SELECT  M.fleet_nr,C.serial_id,C.sample_time, M.last_data_time_1_min, O.lastseen, O.online
		FROM cte C
		join public.monitor  M on M.serial_id = C.serial_id
		join public.omnidots_sensor  O on O.serial_id = C.serial_id
		WHERE rn = 1; --and C.sample_time != M.last_data_time_1_min and fleet_nr='R78522V'
		--and O.online = 1
		--order by M.last_data_time_1_min desc,C.serial_id;

-- View: public.report_rule_search
DROP VIEW IF EXISTS public.report_rule_search CASCADE;
CREATE OR REPLACE VIEW public.report_rule_search AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.frequency
                          ,R.day_of_week
                          ,R.day_of_month
                          ,R.report_name
                          ,R.last_generated
                      FROM public.site S
  				    left join public.site_user SU on  S.id = SU.site_id and  SU.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU.user_id
                    left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct C.id as company_id,company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join public.report_rule R on R.site_id = S.id
                    WHERE R.deleted IS FALSE;

-- View: public.report_rule_user_search
DROP VIEW IF EXISTS public.report_rule_user_search CASCADE;
CREATE OR REPLACE VIEW public.report_rule_user_search AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.frequency
                          ,R.day_of_week
                          ,R.day_of_month
                          ,R.report_name
                          ,R.last_generated
						  ,SU.user_id
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  public.site_user SU on SU.site_id = S.id
                    left join public.site_user SU2 on  S.id = SU2.site_id and  SU2.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU2.user_id
                    inner join public.report_rule R on R.site_id = S.id
                    WHERE R.deleted IS FALSE;

-- View: public.report_search
DROP VIEW IF EXISTS public.report_search CASCADE;
CREATE OR REPLACE VIEW public.report_search AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.report_date
                          ,R.report_from
                          ,R.report_to
                          ,R.report_link
                          ,R.report_rule_id
                          ,R.frequency
                          ,RR.report_name
                          ,RR.deleted
						  ,contracts
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join public.report R on R.site_id = S.id
                    inner join public.report_rule RR on RR.id = R.report_rule_id;

-- View: public.report_search_2
DROP VIEW IF EXISTS public.report_search_2 CASCADE;
CREATE OR REPLACE VIEW public.report_search_2 AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.report_date
                          ,R.report_from
                          ,R.report_to
                          ,R.report_link
                          ,R.report_rule_id
                          ,R.frequency
                          ,RR.report_name
                          ,RR.deleted
						  ,contracts  as contracts
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract where site_id is not null
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join public.report R on R.site_id = S.id
                    inner join public.report_rule RR on RR.id = R.report_rule_id;

-- View: public.report_search_4
DROP VIEW IF EXISTS public.report_search_4 CASCADE;
CREATE OR REPLACE VIEW public.report_search_4 AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.report_date
                          ,R.report_from
                          ,R.report_to
                          ,R.report_link
                          ,R.report_rule_id
                          ,R.frequency
                          ,RR.report_name
                          ,RR.deleted
						  ,'' as contracts
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  public.site_user SU on SU.site_id = S.id
                    left join public.site_user SU2 on  S.id = SU2.site_id and  SU2.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU2.user_id
                    inner join public.report R on R.site_id = S.id
                    inner join public.report_rule RR on RR.id = R.report_rule_id;

-- View: public.report_user_search
DROP VIEW IF EXISTS public.report_user_search CASCADE;
CREATE OR REPLACE VIEW public.report_user_search AS 
                      SELECT   R.id as id
	                      ,site_name
                          ,R.site_id
                          ,R.report_date
                          ,R.report_from
                          ,R.report_to
                          ,R.report_link
                          ,R.report_rule_id
                          ,R.frequency
                          ,RR.report_name
                          ,RR.deleted
						  ,SU.user_id
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  public.site_user SU on SU.site_id = S.id
                    left join public.site_user SU2 on  S.id = SU2.site_id and  SU2.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU2.user_id
                    inner join public.report R on R.site_id = S.id
                    inner join public.report_rule RR on RR.id = R.report_rule_id;

-- View: public.site_search
DROP VIEW IF EXISTS public.site_search CASCADE;
CREATE OR REPLACE VIEW public.site_search AS 
                      SELECT   S.id
	                      ,site_name
						  ,S.archived
                          ,create_date
                          ,address_line_1
                          ,address_line_2
                          ,postcode
                          ,city
                          ,county
	                      ,concat_ws(' ', address_line_1, address_line_2, postcode, city, county)   as site_address
	                      ,con.contracts
	                      ,Comp.company_name
						  ,Comp.company_id
						  ,U."Email" as site_contact
                      FROM public.site S
  				    left join public.site_user SU on  S.id = SU.site_id and  SU.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU.user_id
                    left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct C.id as company_id,company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id;

-- View: public.site_user_search
DROP VIEW IF EXISTS public.site_user_search CASCADE;
CREATE OR REPLACE VIEW public.site_user_search AS 
                      SELECT   S.id
	                      ,site_name
						  ,S.archived
                          ,create_date
                          ,address_line_1
                          ,address_line_2
                          ,postcode
                          ,city
                          ,county
	                      ,concat_ws(' ', S.address_line_1, S.address_line_2, S.postcode, S.city, S.county) as site_address
	                      ,con.contracts
	                      ,Comp.company_name
						  ,U."Email" as site_contact
						  ,SU.user_id
                      FROM public.site S
					 left join (
		                    SELECT STRING_AGG(contract_number, ', ') as contracts, site_id as site_id  
		                    FROM public.contract
		                     Group by site_id
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM public.company C  inner join public.contract con on con.company_id =C.id  inner join public.site s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  public.site_user SU on SU.site_id = S.id
                    left join public.site_user SU2 on  S.id = SU2.site_id and  SU2.site_contact IS TRUE
					left join public."AspNetUsers" U on U."Id"::uuid=SU2.user_id;

-- View: public.user_search
DROP VIEW IF EXISTS public.user_search CASCADE;
CREATE OR REPLACE VIEW public.user_search AS 
                        SELECT  U."Id" as id
                              ,U."CompanyId" as company_id
                              ,U."IsDisabled" as is_disabled
                              ,U."Name" as name
                              ,U."UserName" as user_name
							  ,U."CompanyRole" as company_role
                              ,U."NormalizedUserName" as normalized_user_name
                              ,U."Email" as email
                              ,U."EmailConfirmed" as email_confirmed
                              ,U."ConcurrencyStamp" as concurrency_stamp
                              ,U."PhoneNumber" as phone_number
                              ,U."PhoneNumberConfirmed" as phone_number_confirmed
                              ,U."TwoFactorEnabled" as two_factor_enabled
                              ,U."LockoutEnd" as lockout_end
                              ,U."LockoutEnabled" as lockout_enabled
                              ,U."AccessFailedCount" as access_failed_count
	                          ,R."Name" as role
	                          ,C.company_name  
	                          ,COALESCE(SU.nr_sites,0) as nr_sites
                          FROM public."AspNetUsers" U
                          join public."AspNetUserRoles" RL on RL."UserId" = U."Id"
                          join public."AspNetRoles" R on R."Id" =RL."RoleId"
                          left join public.company C on C.id = U."CompanyId"
						  left join (Select Count(*) as nr_sites,user_id from  public.site_user group by user_id) as SU     on U."Id"::uuid = SU.user_id;

-- View: public.users_for_report_search
DROP VIEW IF EXISTS public.users_for_report_search CASCADE;
CREATE OR REPLACE VIEW public.users_for_report_search AS 
                        SELECT  U."Id" as id
                              ,U."CompanyId" as company_id
                              ,U."IsDisabled" as is_disabled
                              ,U."Name" as name
                              ,U."UserName" as user_name
							  ,U."CompanyRole" as company_role
                              ,U."NormalizedUserName" as normalized_user_name
                              ,U."Email" as email
                              ,U."EmailConfirmed" as email_confirmed
                              ,U."ConcurrencyStamp" as concurrency_stamp
                              ,U."PhoneNumber" as phone_number
                              ,U."PhoneNumberConfirmed" as phone_number_confirmed
                              ,U."TwoFactorEnabled" as two_factor_enabled
                              ,U."LockoutEnd" as lockout_end
                              ,U."LockoutEnabled" as lockout_enabled
                              ,U."AccessFailedCount" as access_failed_count
	                          ,R."Name" as role
	                          ,C.company_name  
	                        --   ,COALESCE(SU.nr_sites,0) as nr_sites
							--   ,SU2.site_id
							--   ,SU2.site_contact
                              ,RU.report_rule_id

                          FROM public."AspNetUsers" U
                          join public."AspNetUserRoles" RL on RL."UserId" = U."Id"
                          join public."AspNetRoles" R on R."Id" =RL."RoleId"
                          left join public.company C on C.id = U."CompanyId"
						--   left join (Select Count(*) as nr_sites,user_id from  public.site_user group by user_id) as SU     on U."Id" = SU.user_id
                        --   left join public.site_user SU2 on  U."Id" = SU2.user_id
						  left join public.report_user RU on  U."Id"::uuid = RU.user_id
                          inner join public.report_rule RR on  RU.report_rule_id = RR.id;

-- View: public.users_for_site_search
DROP VIEW IF EXISTS public.users_for_site_search CASCADE;
CREATE OR REPLACE VIEW public.users_for_site_search AS 
                        SELECT  U."Id" as id
                              ,U."CompanyId" as company_id
                              ,U."IsDisabled" as is_disabled
                              ,U."Name" as name
                              ,U."UserName" as user_name
							  ,U."CompanyRole" as company_role
                              ,U."NormalizedUserName" as normalized_user_name
                              ,U."Email" as email
                              ,U."EmailConfirmed" as email_confirmed
                              ,U."ConcurrencyStamp" as concurrency_stamp
                              ,U."PhoneNumber" as phone_number
                              ,U."PhoneNumberConfirmed" as phone_number_confirmed
                              ,U."TwoFactorEnabled" as two_factor_enabled
                              ,U."LockoutEnd" as lockout_end
                              ,U."LockoutEnabled" as lockout_enabled
                              ,U."AccessFailedCount" as access_failed_count
	                          ,R."Name" as role
	                          ,C.company_name  
	                          ,COALESCE(SU.nr_sites,0) as nr_sites
							  ,SU2.site_id
							  ,SU2.site_contact

                          FROM public."AspNetUsers" U
                          join public."AspNetUserRoles" RL on RL."UserId" = U."Id"
                          join public."AspNetRoles" R on R."Id" =RL."RoleId"
                          left join public.company C on C.id = U."CompanyId"
						  left join (Select Count(*) as nr_sites,user_id from  public.site_user group by user_id) as SU     on U."Id"::uuid = SU.user_id
						  left join public.site_user SU2 on  U."Id"::uuid = SU2.user_id;


-- monitor_measurement_removal_impact
-- Added 2026-07-14. This view is also created by the AddMonitorMeasurementRemovalImpactView EF migration, but
-- the DROP ... CASCADE statements above take it out (it depends on the measurement relations they drop), and
-- nothing here put it back - so every run of this script silently destroyed it and the monitor-removal screen
-- broke until someone re-ran the migration. Recreating it here makes this script self-consistent: whatever it
-- drops, it restores.
CREATE OR REPLACE VIEW public.monitor_measurement_removal_impact AS
WITH measurement_counts AS (
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.my_atm_dust_level
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.my_atm_dust_level_8_hour_avg
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.noise_level_15_min_avg
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.noise_level_1_hour_avg
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.noise_level_1_day_avg
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level_1_min
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level_15_min
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level_20_min
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level_5_min
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_peak_level_1_day_peak
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_trace_index
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.omnidots_monitor_status
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id

    UNION ALL
    SELECT serial_id, COUNT(*)::bigint AS row_count
    FROM public.svantek_monitor_status
    WHERE serial_id IS NOT NULL AND btrim(serial_id) <> ''
    GROUP BY serial_id
)
SELECT
    serial_id,
    COUNT(*)::bigint AS measurement_table_count,
    COALESCE(SUM(row_count), 0)::bigint AS measurement_row_count
FROM measurement_counts
GROUP BY serial_id;

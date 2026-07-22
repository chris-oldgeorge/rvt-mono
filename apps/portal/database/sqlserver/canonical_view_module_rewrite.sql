-- !! SUPERSEDED (2026-07-14) !!
-- This script is a record of the original canonical cutover and still emits the OLD mangled column names
-- (fleet_row_count / row_count / row_count_sites). Those were corrected by the RenameMangledColumns EF
-- migration. Re-running this script against a migrated database would recreate views and routines bound to
-- columns that no longer exist. The current definitions live in database/postgres/post-load/, which the
-- migrator re-applies on every run. Kept for historical reference only.
-- Canonical SQL Server view/module rewrite draft.
-- Generated from docs/database/sqlserver-view-definitions-source.csv and docs/database/sqlserver-name-registry.csv.
-- ASP.NET Identity tables are intentionally excluded from this refactor.
-- Run after canonical_database_naming.sql and before canonical_constraint_index_naming.sql.
BEGIN TRANSACTION;

-- View: dbo.AdminDashboardData -> dbo.admin_dashboard_data
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[admin_dashboard_data] AS 
 	--  ,CAST(CASE   WHEN ISNULL([last_data_time_1_min] ,''1970-01-01'') > DATEADD(hour, -1, GETDATE())  THEN 0  ELSE 1   END AS bit) as off_line
SELECT  count(*) as row_count,
		case 
			when fleet_row_count is null then ''New'' 
			when (D.contract_id is null and fleet_row_count is not null) then ''NotUsed''
			when offline = 1 AND end_date IS NULL then ''Offline''
			when offline = 0 AND end_date IS NULL then ''Online''
			else ''Other'' 
		End as monitor_state 
  FROM [dbo].[monitor] M
  left join [dbo].[deployment] D on D.monitor_id = M.id and D.end_date is null
  Group By
  		case 
			when fleet_row_count is null then ''New'' 
			when (D.contract_id is null and fleet_row_count is not null)then ''NotUsed''
			when offline = 1 AND end_date IS NULL then ''Offline''
			when offline = 0 AND end_date IS NULL then ''Online''
			else ''Other'' 
		End';

-- View: dbo.AirQNoiseLevel1dayAvg -> dbo.air_q_noise_level_1_day_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[air_q_noise_level_1_day_avg] AS 
  SELECT [serial_id]
      , CAST(sample_time as date) as sample_time  
      ,avg([laeq]) as [laeq]
      ,avg([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,avg([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]  
  FROM [dbo].[air_q_noise_level]   
  Group by  [serial_id], CAST(sample_time as date)';

-- View: dbo.AirQNoiseLevel1hourAvg -> dbo.air_q_noise_level_1_hour_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[air_q_noise_level_1_hour_avg] AS 
 SELECT [serial_id]
      ,IsNull(DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0)),GETUTCDATE()) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg([laeq]) as [laeq]
      ,avg([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,avg([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]  
  FROM [dbo].[air_q_noise_level]
  Group by [serial_id], DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0))';

-- View: dbo.AirQNoiseLevelSiteAvg -> dbo.air_q_noise_level_site_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[air_q_noise_level_site_avg] AS 
SELECT
M.serial_id, 
    collection_time as sample_time,
    laeq,
    lamax,
    la_90,
    la_10,
    lceq,
    lcmax,
    lc_90,
    lc_10
    FROM
    (
        SELECT site_id, monitor_id, collection_time, level, field
        FROM [dbo].[site_average]
    ) A
    pivot
    (
        max(level)
        FOR field IN (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10)
    ) piv
  INNER JOIN [dbo].[monitor] M ON M.id = monitor_id';

-- View: dbo.CompanySearch -> dbo.company_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[company_search] AS 
                            SELECT   C.[id]
                                  ,[company_name]
 	                              ,ISNULL(U.user_count,0) as user_count
	                              ,s.sites
	                              ,con.contracts

                               FROM [dbo].[company] C
                             left join (Select Count(*) as user_count, CompanyId as company_id from  [dbo].[AspNetUsers] group by CompanyId) as U     on C.id = U.company_id
                             left join (SELECT STRING_AGG(site_name, '', '') as sites, [company_id] as [company_id]    FROM (SELECT Distinct    [company_id]  ,[site_id]  ,S.site_name as site_name   FROM  [dbo].[contract] C  join [dbo].[site] S on S.id =C.site_id ) t  Group by [company_id] ) s  on s.company_id=C.id
                             left join ( SELECT STRING_AGG([contract_number], '', '') as contracts, [company_id] as [company_id]   FROM [dbo].[contract] Group by [company_id])  con  on con.[company_id] = C.id';

-- View: dbo.ContractSearch -> dbo.contract_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[contract_search] AS 
                        SELECT   C.[id]
                              ,[contract_number]
                              ,[off_hire_date]
                              ,[on_hire_date]
	                          ,[company_id]
                              ,[site_id]
	                          ,Comp.company_name
	                          ,[site_name]
	                          ,S.[site_name] +'' ''+  S.[address_line_1]+'' ''+  S.[address_line_2]+'' ''+  S.[postcode]+'' ''+  S.[city]+'' ''+  S.[county] as site_address
                          FROM [dbo].[contract] C
                         left join [dbo].[company] Comp on comp.id=C.company_id 
                         left join [dbo].[site] S on S.id=C.site_id';

-- View: dbo.CustomerDashboardMonitorData -> dbo.customer_dashboard_monitor_data
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[customer_dashboard_monitor_data] AS 
SELECT  count(*) as row_count,
		case 
			when fleet_row_count is null then ''New'' 
			when [contract_number] is null then ''NotUsed''
			when offline = 1 AND D.end_date IS NULL then ''Offline''
			when offline = 0 AND D.end_date IS NULL then ''Online''
			else ''Other'' 
		End as monitor_state,
        SU.user_id
  FROM [dbo].[monitor] M
  left join [dbo].[deployment] D on D.monitor_id = M.id and D.end_date is null
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  join  [dbo].[site_user] SU on SU.site_id = S.id
  WHERE fleet_row_count IS NOT NULL AND C.site_id IS NOT NULL
 Group By
  		case 
            when fleet_row_count is null then ''New'' 
			when [contract_number] is null then ''NotUsed''
			when offline = 1 AND D.end_date IS NULL then ''Offline''
			when offline = 0 AND D.end_date IS NULL then ''Online''
			else ''Other'' 
		End,
        SU.user_id';

-- View: dbo.CustomerDashboardNotificationData -> dbo.customer_dashboard_notification_data
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[customer_dashboard_notification_data] AS 
SELECT  count(*) as row_count,
		N.alert_type,
		case 
			when closed_time IS NULL then ''Open''
			else ''Closed'' 
		End as alert_state,
        SU.user_id
  FROM [dbo].[notification] N
--   left join [dbo].[rvt_alert_rule] A on A.id = N.AlertlevelId
  left join [dbo].[monitor] M on N.monitor_id = M.id
--  left join [dbo].[deployment] D on D.monitor_id = M.id and D.end_date is null -- THIS END DATE PART IS WRONG
  left join [dbo].[deployment] D on D.monitor_id = M.id and N.notification_time between D.start_date and isnull(D.end_date , Getdate())
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  join  [dbo].[site_user] SU on SU.site_id = S.id
  WHERE fleet_row_count IS NOT NULL AND C.site_id IS NOT NULL
 Group By
  		N.alert_type,
		case 
			when closed_time IS NULL then ''Open''
			else ''Closed'' 
		End,
        SU.user_id';

-- View: dbo.MonitorCurrentSearch -> dbo.monitor_current_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[monitor_current_search] AS 
SELECT M.[id]
      ,D.id as deployment_id
      ,[fleet_row_count]
      ,[serial_id]
      --,[manufacturer]
      --,[model]
      --,[firmware_version]
      ,[type_of_monitor]
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.battery_status ,0)>0  THEN 1  ELSE 0   END AS bit) as battery
	  ,CAST(CASE   WHEN IsNUll(alerts.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as alerts
	  ,CAST(CASE   WHEN IsNUll(cautions.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as cautions
       ,[location_id] 
	  , D.lat as latitude
	  , D.lng as longitude 
 
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
       --,[location_address]
      --,[time_zone]
      --,[customer_id]
      --,[customer_display_name]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[listed_at_time]
	  ,CASE   
		  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,[last_data_time_1_min] as last_data_time
      --,[last_data_time_15_min]
      --,[last_data_time_1_hour]
      --,[last_data_time_24_hour]
  FROM [dbo].[monitor] M
  left join [dbo].[deployment] D on D.monitor_id = M.id and D.end_date is null
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT Top 1 DATEADD(SECOND, -(averaging_period), GETUTCDATE())  as OfflineTime ,alert_type  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 2  ) as Offlines  on alert_type=2';

-- View: dbo.MonitorReport -> dbo.monitor_report
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[monitor_report] AS 

SELECT M.[id]
	  ,CAST(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END AS bit) as active
	  ,D.id as deployment_id
      ,[fleet_row_count]
      ,[serial_id]
      --,[manufacturer]
      --,[model]
      --,[firmware_version]
      ,[type_of_monitor]
	  ,CAST(CASE   WHEN ISNULL([last_data_time_1_min] ,''1970-01-01'') > OfflineTime or  ISNULL(last_data_time_15_min ,''1970-01-01'') > OfflineTime    THEN 0  ELSE 1   END AS bit) as off_line
	  ,CAST(CASE   WHEN IsNUll(alerts.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as alerts
	  ,CAST(CASE   WHEN IsNUll(cautions.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as cautions
       ,[location_id] 
	  , D.lat as latitude
	  , D.lng as longitude 
      , D.[location]
	  ,D.start_date
	  ,D.end_date
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
      ,M.calibration_date
       --,[location_address]
      --,[time_zone]
      --,[customer_id]
      --,[customer_display_name]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[listed_at_time]
	  ,CASE   
		  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,[last_data_time_1_min] as last_data_time
      --,[last_data_time_15_min]
      --,[last_data_time_1_hour]
      --,[last_data_time_24_hour]
  FROM [dbo].[monitor] M
  left join [dbo].[deployment] D on D.monitor_id = M.id --and D.end_date is null
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT Top 1 DATEADD(SECOND, -(averaging_period), GETUTCDATE())  as OfflineTime ,alert_type  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 2  ) as Offlines  on alert_type=2';

-- View: dbo.MonitorSearch -> dbo.monitor_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[monitor_search] AS 

SELECT M.[id]  
	  ,CAST(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END AS bit) as active
	  ,D.id as deployment_id
      ,[fleet_row_count]
      ,M.[serial_id] as serial_id
	  ,(CASE  WHEN [type_of_monitor]=2 THEN isNull(OS.name, M.customer_display_name) ELSE Null END) as monitor_name
      --,[manufacturer]
      --,[model]
      --,[firmware_version]
      ,[type_of_monitor]
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.battery_status ,0)>0  THEN 1  ELSE 0   END AS bit) as battery
	  ,CAST(CASE   WHEN IsNUll(alerts.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as alerts
	  ,CAST(CASE   WHEN IsNUll(cautions.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as cautions
       ,[location_id] 
	  , D.lat as latitude
	  , D.lng as longitude 
	  ,D.end_date
	  ,D.what_3_words
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
       --,[location_address]
      --,[time_zone]
      --,[customer_id]
      --,[customer_display_name]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[listed_at_time]
	  ,CASE   
		  WHEN  D.end_date is not null  THEN D.end_date  
		  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,[last_data_time_1_min] as last_data_time
      --,[last_data_time_15_min]
      --,[last_data_time_1_hour]
      --,[last_data_time_24_hour]
  FROM [dbo].[monitor] M
  left join  [dbo].[omnidots_sensor] OS on OS.serial_id = M.serial_id
  left join [dbo].[deployment] D on D.monitor_id = M.id --and D.end_date is null
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT Top 1 DATEADD(SECOND, -(averaging_period), GETUTCDATE())  as OfflineTime ,alert_type  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 2  ) as Offlines  on alert_type=2';

-- View: dbo.MonitorUserSearch -> dbo.monitor_user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[monitor_user_search] AS 
SELECT M.[id]	
	  ,CAST(CASE WHEN D.end_date is null  THEN 1  ELSE 0 END AS bit) as active
	  ,D.id as deployment_id
      ,[fleet_row_count]
      ,[serial_id]
      --,[manufacturer]
      --,[model]
      --,[firmware_version]
      ,[type_of_monitor]
	  ,M.offline as off_line
	  --,M.battery_status   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.battery_status ,0)>0  THEN 1  ELSE 0   END AS bit) as battery
	  ,CAST(CASE   WHEN IsNUll(alerts.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as alerts
	  ,CAST(CASE   WHEN IsNUll(cautions.row_count,0)>0  THEN 1  ELSE 0   END AS bit) as cautions
      ,[location_id] 
	  , D.lat as latitude
	  , D.lng as longitude 
	  ,D.what_3_words
	  ,C.contract_number
	  ,C.site_id
	  ,site_name
	  ,SU.user_id
       --,[location_address]
      --,[time_zone]
      --,[customer_id]
      --,[customer_display_name]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[listed_at_time]
      --,[last_data_time_1_min] as last_data_time
	  ,CASE   
		 WHEN  D.end_date is not null  THEN D.end_date  
		 WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
	  --,[last_data_time_15_min]
      --,[last_data_time_1_hour]
      --,[last_data_time_24_hour]

  FROM [dbo].[monitor] M
  left join [dbo].[deployment] D on D.monitor_id = M.id --and D.end_date is null
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  join  [dbo].[site_user] SU on SU.site_id = S.id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id
  left join  (SELECT Top 1 DATEADD(SECOND, -(averaging_period), GETUTCDATE())  as OfflineTime ,alert_type  FROM [dbo].[rvt_alert_rule]   where   is_active =1 and alert_type= 2  ) as Offlines  on alert_type=2';

-- View: dbo.MyAtmDustLevel1DayAvg -> dbo.my_atm_dust_level_1_day_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[my_atm_dust_level_1_day_avg] AS 

SELECT D.[serial_id]
	  ,De.id as deployment_id
      ,[avrg]
      ,[sample_time]
      ,[pm_1]
      ,[pm_2_5]
      ,[pm_10]
      ,[pm_total]
  FROM [dbo].[my_atm_dust_level] D
 join [dbo].[monitor] m on m.serial_id = D.serial_id
 join [dbo].[deployment] De on De.monitor_id =  m.id 
 where avrg = 86400 and D.sample_time between  De.start_date and IsNull(De.end_date, GetDATE())';

-- View: dbo.MyAtmDustLevel8hourAvg -> dbo.my_atm_dust_level_8_hour_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[my_atm_dust_level_8_hour_avg] AS 
 SELECT [serial_id]
      ,IsNull(DATEADD(hh, (FORMAT(sample_time,''HH'')/8+1)*8, DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0)),GETUTCDATE()) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg([pm_1]) as [pm_1]
      ,avg([pm_2_5]) as [pm_2_5]
      ,avg([pm_10]) as [pm_10]
      ,avg([pm_total]) as [pm_total]  
  FROM [dbo].[my_atm_dust_level]
  where avrg = 60 
  Group by [serial_id], DATEADD(hh, (FORMAT(sample_time,''HH'')/8+1)*8, DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0))';

-- View: dbo.NoiseLevel15minAvg -> dbo.noise_level_15_min_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[noise_level_15_min_avg] AS 
 
	SELECT [serial_id]
		  ,[sample_time]
		  ,[laeq]
		  ,[lamax]
		  ,[la_90]
		  ,[la_10]
		  ,[lceq]
		  ,[lcmax]
		  ,[lc_90]
		  ,[lc_10]
	  FROM [dbo].[air_q_noise_level]
 
 UNION all

  SELECT [serial_id]
      ,DATEADD(minute, (DATEDIFF(minute, 0, sample_time) / 15) * 15, 0)  as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg([laeq]) as [laeq]
      ,max([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,max([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]  
  FROM [dbo].[svantek_noise_level]
  Group by [serial_id], DATEADD(minute, (DATEDIFF(minute, 0, sample_time) / 15) * 15, 0)';

-- View: dbo.NoiseLevel1dayAvg -> dbo.noise_level_1_day_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[noise_level_1_day_avg] AS 
  SELECT [serial_id]
      , CAST(sample_time as date) as sample_time  
      ,avg([laeq]) as [laeq]
      ,max([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,max([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]  
  FROM [dbo].[air_q_noise_level]   
  Group by  [serial_id], CAST(sample_time as date)

Union all

  SELECT [serial_id]
      , CAST(sample_time as date) as sample_time  
      ,avg([laeq]) as [laeq]
      ,max([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,max([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]  
  FROM [dbo].[svantek_noise_level]   
  Group by  [serial_id], CAST(sample_time as date)';

-- View: dbo.NoiseLevel1hourAvg -> dbo.noise_level_1_hour_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[noise_level_1_hour_avg] AS 
 SELECT [serial_id]
      ,IsNull(DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0)),GETUTCDATE()) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg([laeq]) as [laeq]
      ,max([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,max([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10]   
  FROM [dbo].[air_q_noise_level]
  Group by [serial_id], DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0))
 
 UNION all

  SELECT [serial_id]
      ,IsNull(DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0)),GETUTCDATE()) as sample_time --Isnull ony to get the imported variable to not nullable
      ,avg([laeq]) as [laeq]
      ,max([lamax]) as [lamax]
      ,avg([la_90]) as [la_90]
      ,avg([la_10]) as [la_10]  
      ,avg([lceq]) as [lceq]
      ,max([lcmax]) as [lcmax]
      ,avg([lc_90]) as [lc_90]
      ,avg([lc_10]) as [lc_10] 
  FROM [dbo].[svantek_noise_level]
  Group by [serial_id], DATEADD(hh, (FORMAT(sample_time,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, sample_time), 0))';

-- View: dbo.NoiseLevelSiteAvg -> dbo.noise_level_site_avg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[noise_level_site_avg] AS 
SELECT
M.serial_id, 
    collection_time as sample_time,
    laeq,
    lamax,
    la_90,
    la_10,
    lceq,
    lcmax,
    lc_90,
    lc_10
    FROM
    (
        SELECT site_id, monitor_id, collection_time, level, field
        FROM [dbo].[site_average]
    ) A
    pivot
    (
        max(level)
        FOR field IN (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10)
    ) piv
  INNER JOIN [dbo].[monitor] M ON M.id = monitor_id';

-- View: dbo.NotificationSearch -> dbo.notification_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[notification_search] AS 
SELECT N.[id]
      ,M.id as [monitor_id]
      ,[fleet_row_count]
      ,M.serial_id
      ,[type_of_monitor]
	  ,N.alert_type as alert_type
      ,N.closed_time as [closed_date]
      ,N.alert_field
      ,N.limit_on
      ,N.[level]
      ,N.notification_time
	  ,C.id as contract_id
	  ,C.contract_number
	  ,site_id
	  ,site_name
	  ,CASE   
		  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
  FROM [dbo].[notification] N
  left join [dbo].[monitor] M on N.monitor_id = M.id
  join [dbo].[deployment] D on D.monitor_id = M.id and N.notification_time between D.start_date and isnull(D.end_date , Getdate())
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id';

-- View: dbo.NotificationUserSearch -> dbo.notification_user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[notification_user_search] AS 
SELECT N.[id]
      ,[fleet_row_count]
      ,M.id as [monitor_id]
      ,M.serial_id
      ,[type_of_monitor]
	  ,N.alert_type as alert_type
      ,N.closed_time as [closed_date]
      ,N.alert_field
      ,N.limit_on
      ,N.[level]
      ,N.notification_time
	  ,C.id as contract_id
	  ,C.contract_number
	  ,C.site_id
	  ,site_name
	  ,SU.user_id
	  ,CASE   
		  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		  ELSE  last_data_time_24_hour  
		END as last_data_time
  FROM [dbo].[notification] N
--   left join [dbo].[rvt_alert_rule] A on A.id = N.AlertlevelId
  left join [dbo].[monitor] M on N.monitor_id = M.id
  left join [dbo].[deployment] D on D.monitor_id = M.id and N.notification_time between D.start_date and isnull(D.end_date , Getdate())
  left join [dbo].[contract] C on C.id = D.contract_id 
  left join [dbo].[site] S on S.id = C.site_id
  join  [dbo].[site_user] SU on SU.site_id = S.id
   --UNION
  --SELECT M.[id]
  --    ,[fleet_row_count]
  --    ,M.serial_id
  --    ,[type_of_monitor]
	 -- ,CAST(CASE   WHEN ISNULL([last_data_time_1_min] ,''1970-01-01'') > dbo.fnOfflineDateTime() or  ISNULL(last_data_time_15_min ,''1970-01-01'') > dbo.fnOfflineDateTime()    THEN 0  ELSE 1   END AS bit) as off_line
	 -- ,CAST(0 AS bit) as alerts
	 -- ,CAST(0 AS bit) as cautions
  --    ,NULL as [closed_date]
  --    ,'''' as alert_field
  --    ,NULL as limit_on
  --    ,NULL as level
  --    ,CASE   
		--  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
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
		--  WHEN  [last_data_time_1_min] is not null  THEN [last_data_time_1_min]  
		--  WHEN  last_data_time_15_min is not null  THEN last_data_time_15_min  
		--  WHEN  last_data_time_1_hour is not null  THEN last_data_time_1_hour  
		--  ELSE  last_data_time_24_hour  
		--END as last_data_time
  --FROM [dbo].[monitor] M
  --left join [dbo].[deployment] D on D.monitor_id = M.id and D.end_date is null
  --left join [dbo].[contract] C on C.id = D.contract_id 
  --left join [dbo].[site] S on S.id = C.site_id
  --join  [dbo].[site_user] SU on SU.site_id = S.id
  --left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   alert_type= 0 Group by monitor_id) as alerts on alerts.monitor_id =M.id
  --left join  (SELECT count(*) as row_count ,monitor_id  FROM [dbo].[rvt_alert_rule]   where   alert_type= 1 Group by monitor_id) as cautions on cautions.monitor_id =M.id';

-- View: dbo.OmnidotsPeakLevel15min -> dbo.omnidots_peak_level_15_min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_peak_level_15_min] AS

 SELECT  
	serial_id,
	ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 15 * 15, 0),GETDATE()) as sample_time,
    max([x_vtop]) as [x_vtop],
    max([y_vtop]) as [y_vtop],
    max([z_vtop]) as [z_vtop]
 FROM 
	[dbo].[omnidots_peak_level]
GROUP BY 
    serial_id, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 15 * 15, 0);';

-- View: dbo.OmnidotsPeakLevel1dayPeak -> dbo.omnidots_peak_level_1_day_peak
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_peak_level_1_day_peak] AS 
	SELECT  [serial_id]
		  , CAST(sample_time as date) as sample_time  
		  --,[sample_time]
		  --,[x_fdom]
		  ,MAX([x_vtop]) as [x_vtop]
		  --,Max([x_vtop_overflow]) as [x_vtop]
		  --,[y_fdom]
		  ,MAX([y_vtop]) as [y_vtop]
		  --,MAX([y_vtop_overflow]) as [y_vtop]
		  --,[z_fdom]
		  ,MAX([z_vtop]) as [z_vtop]
		  --,MAX([z_vtop_overflow])  as [z_vtop]
	   FROM [dbo].[omnidots_peak_level]
	   Group by  [serial_id], CAST(sample_time as date)';

-- View: dbo.OmnidotsPeakLevel1min -> dbo.omnidots_peak_level_1_min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_peak_level_1_min] AS

 SELECT  
    serial_id,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 1 * 1, 0),GETDATE()) as sample_time,
    max([x_vtop]) as [x_vtop],
    max([y_vtop]) as [y_vtop],
    max([z_vtop]) as [z_vtop]
 FROM 
	[dbo].[omnidots_peak_level]
GROUP BY 
     serial_id, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 1 * 1, 0);';

-- View: dbo.OmnidotsPeakLevel20min -> dbo.omnidots_peak_level_20_min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_peak_level_20_min] AS

 SELECT  
	serial_id,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 20 * 20, 0),GETDATE()) as sample_time,
    max([x_vtop]) as [x_vtop],
    max([y_vtop]) as [y_vtop],
    max([z_vtop]) as [z_vtop]
 FROM 
	[dbo].[omnidots_peak_level]
GROUP BY 
    serial_id, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 20 * 20, 0);';

-- View: dbo.OmnidotsPeakLevel5min -> dbo.omnidots_peak_level_5_min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_peak_level_5_min] AS

 SELECT  
    serial_id,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 5 * 5, 0),GETDATE()) as sample_time,
    max([x_vtop]) as [x_vtop],
    max([y_vtop]) as [y_vtop],
    max([z_vtop]) as [z_vtop]
 FROM 
	[dbo].[omnidots_peak_level]
GROUP BY 
     serial_id, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [sample_time]) / 5 * 5, 0);';

-- View: dbo.OmnidotsReadStatus -> dbo.omnidots_read_status
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[omnidots_read_status] AS 
		WITH cte AS
		(
			SELECT serial_id,sample_time,
			ROW_NUMBER() OVER (PARTITION BY serial_id ORDER BY sample_time DESC) AS rn
			FROM [dbo].[omnidots_peak_level] where serial_id in (
				SELECT [serial_id]   FROM [dbo].[monitor]  M
				join [dbo].[deployment] D on D.monitor_id=M.id
				where type_of_monitor =2 )
		)
		SELECT  M.fleet_row_count,C.serial_id,C.sample_time, M.last_data_time_1_min, O.[lastseen], O.[online]
		FROM cte C
		join [dbo].[monitor]  M on M.serial_id = C.serial_id
		join [dbo].[omnidots_sensor]  O on O.serial_id = C.serial_id
		WHERE rn = 1 --and C.sample_time != M.last_data_time_1_min and fleet_row_count=''R78522V''
		--and O.[online] = 1
		--order by M.last_data_time_1_min desc,C.serial_id';

-- View: dbo.ReportRuleSearch -> dbo.report_rule_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_rule_search] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
                          ,R.site_id
                          ,R.frequency
                          ,R.day_of_week
                          ,R.day_of_month
                          ,R.report_name
                          ,R.last_generated
                      FROM [dbo].[site] S
  				    left join [dbo].[site_user] SU on  S.id = SU.site_id and  SU.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU.user_id
                    left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct C.id as company_id,company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join [dbo].[report_rule] R on R.site_id = S.id
                    WHERE R.deleted = 0';

-- View: dbo.ReportRuleUserSearch -> dbo.report_rule_user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_rule_user_search] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
                          ,R.site_id
                          ,R.frequency
                          ,R.day_of_week
                          ,R.day_of_month
                          ,R.report_name
                          ,R.last_generated
						  ,SU.user_id
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  [dbo].[site_user] SU on SU.site_id = S.id
                    left join [dbo].[site_user] SU2 on  S.id = SU2.site_id and  SU2.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU2.user_id
                    inner join [dbo].[report_rule] R on R.site_id = S.id
                    WHERE R.deleted = 0';

-- View: dbo.ReportSearch -> dbo.report_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_search] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
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
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join [dbo].[report] R on R.site_id = S.id
                    inner join [dbo].[report_rule] RR on RR.Id = R.report_rule_id';

-- View: dbo.ReportSearch2 -> dbo.report_search_2
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_search_2] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
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
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract] where site_id is not null
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
                    inner join [dbo].[report] R on R.site_id = S.id
                    inner join [dbo].[report_rule] RR on RR.Id = R.report_rule_id';

-- View: dbo.ReportSearch4 -> dbo.report_search_4
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_search_4] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
                          ,R.site_id
                          ,R.report_date
                          ,R.report_from
                          ,R.report_to
                          ,R.report_link
                          ,R.report_rule_id
                          ,R.frequency
                          ,RR.report_name
                          ,RR.deleted
						  ,'''' as contracts
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  [dbo].[site_user] SU on SU.site_id = S.id
                    left join [dbo].[site_user] SU2 on  S.id = SU2.site_id and  SU2.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU2.user_id
                    inner join [dbo].[report] R on R.site_id = S.id
                    inner join [dbo].[report_rule] RR on RR.Id = R.report_rule_id';

-- View: dbo.ReportUserSearch -> dbo.report_user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[report_user_search] AS 
                      SELECT   R.Id as id
	                      ,[site_name]
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
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  [dbo].[site_user] SU on SU.site_id = S.id
                    left join [dbo].[site_user] SU2 on  S.id = SU2.site_id and  SU2.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU2.user_id
                    inner join [dbo].[report] R on R.site_id = S.id
                    inner join [dbo].[report_rule] RR on RR.Id = R.report_rule_id';

-- View: dbo.SiteSearch -> dbo.site_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[site_search] AS 
                      SELECT   S.id
	                      ,[site_name]
						  ,S.archived
                          ,[create_date]
                          ,[address_line_1]
                          ,[address_line_2]
                          ,[postcode]
                          ,[city]
                          ,[county]
	                      ,ISNULL([address_line_1], '''') +'' ''+  ISNULL([address_line_2], '''')  +'' ''+  ISNULL([postcode], '''') +'' ''+  ISNULL([city], '''') +'' ''+  ISNULL([county], '''')   as site_address
	                      ,con.contracts
	                      ,Comp.company_name
						  ,Comp.company_id
						  ,U.email as site_contact
                      FROM [dbo].[site] S
  				    left join [dbo].[site_user] SU on  S.id = SU.site_id and  SU.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU.user_id
                    left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct C.id as company_id,company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id';

-- View: dbo.SiteUserSearch -> dbo.site_user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[site_user_search] AS 
                      SELECT   S.id
	                      ,[site_name]
						  ,S.archived
                          ,[create_date]
                          ,[address_line_1]
                          ,[address_line_2]
                          ,[postcode]
                          ,[city]
                          ,[county]
	                      ,S.[address_line_1]+'' ''+  S.[address_line_2]+'' ''+  S.[postcode]+'' ''+  S.[city]+'' ''+  S.[county] as site_address
	                      ,con.contracts
	                      ,Comp.company_name
						  ,U.email as site_contact
						  ,SU.user_id
                      FROM [dbo].[site] S
					 left join (
		                    SELECT STRING_AGG([contract_number], '', '') as contracts, [site_id] as [site_id]  
		                    FROM [dbo].[contract]
		                     Group by [site_id]
                    )  con  on con.site_id = S.id
                    left join (SELECT distinct company_name,  site_id   FROM [dbo].[company] C  inner join [dbo].[contract] con on con.company_id =C.id  inner join [dbo].[site] s on S.id = con.site_id) comp  on comp.site_id = S.id
					join  [dbo].[site_user] SU on SU.site_id = S.id
                    left join [dbo].[site_user] SU2 on  S.id = SU2.site_id and  SU2.site_contact=1
					left join dbo.AspNetUsers U on U.Id=SU2.user_id';

-- View: dbo.UserSearch -> dbo.user_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[user_search] AS 
                        SELECT  U.[Id] as id
                              ,U.[CompanyId] as company_id
                              ,U.[IsDisabled] as is_disabled
                              ,U.[Name] as name
                              ,U.[UserName] as user_name
							  ,U.CompanyRole as company_role
                              ,U.[NormalizedUserName] as normalized_user_name
                              ,U.[Email] as email
                              ,U.[EmailConfirmed] as email_confirmed
                              ,U.[ConcurrencyStamp] as concurrency_stamp
                              ,U.[PhoneNumber] as phone_number
                              ,U.[PhoneNumberConfirmed] as phone_number_confirmed
                              ,U.[TwoFactorEnabled] as two_factor_enabled
                              ,U.[LockoutEnd] as lockout_end
                              ,U.[LockoutEnabled] as lockout_enabled
                              ,U.[AccessFailedCount] as access_failed_count
	                          ,R.Name as role
	                          ,C.company_name  
	                          ,ISNULL(SU.row_count_sites,0) as row_count_sites
                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[company] C on C.id = U.CompanyId
						  left join (Select Count(*) as row_count_sites,user_id from  [dbo].[site_user] group by user_id) as SU     on U.Id = SU.user_id';

-- View: dbo.UsersForReportSearch -> dbo.users_for_report_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[users_for_report_search] AS 
                        SELECT  U.[Id] as id
                              ,U.[CompanyId] as company_id
                              ,U.[IsDisabled] as is_disabled
                              ,U.[Name] as name
                              ,U.[UserName] as user_name
							  ,U.CompanyRole as company_role
                              ,U.[NormalizedUserName] as normalized_user_name
                              ,U.[Email] as email
                              ,U.[EmailConfirmed] as email_confirmed
                              ,U.[ConcurrencyStamp] as concurrency_stamp
                              ,U.[PhoneNumber] as phone_number
                              ,U.[PhoneNumberConfirmed] as phone_number_confirmed
                              ,U.[TwoFactorEnabled] as two_factor_enabled
                              ,U.[LockoutEnd] as lockout_end
                              ,U.[LockoutEnabled] as lockout_enabled
                              ,U.[AccessFailedCount] as access_failed_count
	                          ,R.Name as role
	                          ,C.company_name  
	                        --   ,ISNULL(SU.row_count_sites,0) as row_count_sites
							--   ,SU2.site_id
							--   ,SU2.site_contact
                              ,RU.report_rule_id

                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[company] C on C.id = U.CompanyId
						--   left join (Select Count(*) as row_count_sites,user_id from  [dbo].[site_user] group by user_id) as SU     on U.Id = SU.user_id
                        --   left join [dbo].[site_user] SU2 on  U.Id = SU2.user_id
						  left join [dbo].[report_user] RU on  U.Id = RU.user_id
                          inner join [dbo].[report_rule] RR on  RU.report_rule_id = RR.Id';

-- View: dbo.UsersForSiteSearch -> dbo.users_for_site_search
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[users_for_site_search] AS 
                        SELECT  U.[Id] as id
                              ,U.[CompanyId] as company_id
                              ,U.[IsDisabled] as is_disabled
                              ,U.[Name] as name
                              ,U.[UserName] as user_name
							  ,U.CompanyRole as company_role
                              ,U.[NormalizedUserName] as normalized_user_name
                              ,U.[Email] as email
                              ,U.[EmailConfirmed] as email_confirmed
                              ,U.[ConcurrencyStamp] as concurrency_stamp
                              ,U.[PhoneNumber] as phone_number
                              ,U.[PhoneNumberConfirmed] as phone_number_confirmed
                              ,U.[TwoFactorEnabled] as two_factor_enabled
                              ,U.[LockoutEnd] as lockout_end
                              ,U.[LockoutEnabled] as lockout_enabled
                              ,U.[AccessFailedCount] as access_failed_count
	                          ,R.Name as role
	                          ,C.company_name  
	                          ,ISNULL(SU.row_count_sites,0) as row_count_sites
							  ,SU2.site_id
							  ,SU2.site_contact

                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[company] C on C.id = U.CompanyId
						  left join (Select Count(*) as row_count_sites,user_id from  [dbo].[site_user] group by user_id) as SU     on U.Id = SU.user_id
						  left join [dbo].[site_user] SU2 on  U.Id = SU2.user_id';

COMMIT TRANSACTION;

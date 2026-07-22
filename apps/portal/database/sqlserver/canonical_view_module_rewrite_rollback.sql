-- SQL Server view/module rewrite rollback draft.
-- Restores source view definitions from docs/database/sqlserver-view-definitions-source.csv after rollback renames.
-- ASP.NET Identity tables are intentionally excluded from this refactor.
BEGIN TRANSACTION;

-- View: dbo.admin_dashboard_data -> dbo.AdminDashboardData
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[AdminDashboardData] AS 
 	--  ,CAST(CASE   WHEN ISNULL([LastDataTime1Min] ,''1970-01-01'') > DATEADD(hour, -1, GETDATE())  THEN 0  ELSE 1   END AS bit) as OffLine
SELECT  count(*) as nr,
		case 
			when FleetNr is null then ''New'' 
			when (D.ContractId is null and FleetNr is not null) then ''NotUsed''
			when Offline = 1 AND EndDate IS NULL then ''Offline''
			when Offline = 0 AND EndDate IS NULL then ''Online''
			else ''Other'' 
		End as MonitorState 
  FROM [dbo].[MonitorsList] M
  left join [dbo].[Deployments] D on D.MonitorId = M.Id and D.EndDate is null
  Group By
  		case 
			when FleetNr is null then ''New'' 
			when (D.ContractId is null and FleetNr is not null)then ''NotUsed''
			when Offline = 1 AND EndDate IS NULL then ''Offline''
			when Offline = 0 AND EndDate IS NULL then ''Online''
			else ''Other'' 
		End';

-- View: dbo.air_q_noise_level_1_day_avg -> dbo.AirQNoiseLevel1dayAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[AirQNoiseLevel1dayAvg] AS 
  SELECT [SerialId]
      , CAST(SampleTime as date) as SampleTime  
      ,avg([LAeq]) as [LAeq]
      ,avg([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,avg([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]  
  FROM [dbo].[AirQNoiseLevels]   
  Group by  [SerialId], CAST(SampleTime as date)';

-- View: dbo.air_q_noise_level_1_hour_avg -> dbo.AirQNoiseLevel1hourAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[AirQNoiseLevel1hourAvg] AS 
 SELECT [SerialId]
      ,IsNull(DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0)),GETUTCDATE()) as SampleTime --Isnull ony to get the imported variable to not nullable
      ,avg([LAeq]) as [LAeq]
      ,avg([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,avg([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]  
  FROM [dbo].[AirQNoiseLevels]
  Group by [SerialId], DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0))';

-- View: dbo.air_q_noise_level_site_avg -> dbo.AirQNoiseLevelSiteAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[AirQNoiseLevelSiteAvg] AS 
SELECT
M.SerialId, 
    CollectionTime AS SampleTime,
    LAeq,
    LAmax,
    LA90,
    LA10,
    LCeq,
    LCmax,
    LC90,
    LC10
    FROM
    (
        SELECT SiteId, MonitorId, CollectionTime, Level, Field
        FROM [dbo].[SiteAverages]
    ) A
    pivot
    (
        max(Level)
        FOR Field IN (LAeq, LAmax, LA90, LA10, LCeq, LCmax, LC90, LC10)
    ) piv
  INNER JOIN [dbo].[MonitorsList] M ON M.Id = MonitorId';

-- View: dbo.company_search -> dbo.CompanySearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[CompanySearch] AS 
                            SELECT   C.[Id]
                                  ,[CompanyName]
 	                              ,ISNULL(U.nrUsers,0) as nrUsers
	                              ,s.sites
	                              ,con.contracts

                               FROM [dbo].[Companies] C
                             left join (Select Count(*) as nrUsers,CompanyId from  [dbo].[AspNetUsers] group by CompanyId) as U     on C.Id = U.CompanyId
                             left join (SELECT STRING_AGG(SiteName, '', '') as sites, [CompanyId] as [CompanyId]    FROM (SELECT Distinct    [CompanyId]  ,[SiteiD]  ,S.SiteName as SiteName   FROM  [dbo].[Contracts] C  join dbo.Sites S on S.Id =c.SiteiD ) t  Group by [CompanyId] ) s  on s.CompanyId=c.Id
                             left join ( SELECT STRING_AGG([ContractNumber], '', '') as contracts, [CompanyId] as [CompanyId]   FROM [dbo].[Contracts] Group by [CompanyId])  con  on con.[CompanyId] = c.Id';

-- View: dbo.contract_search -> dbo.ContractSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ContractSearch] AS 
                        SELECT   C.[Id]
                              ,[ContractNumber]
                              ,[OffHireDate]
                              ,[OnHireDate]
	                          ,[CompanyId]
                              ,[SiteiD]
	                          ,Comp.CompanyName
	                          ,[SiteName]
	                          ,S.[SiteName] +'' ''+  S.[AddressLine1]+'' ''+  S.[AddressLine2]+'' ''+  S.[Postcode]+'' ''+  S.[City]+'' ''+  S.[County] as siteAddress
                          FROM [dbo].[Contracts] C
                         left join dbo.Companies Comp on comp.Id=C.CompanyId 
                         left join dbo.Sites S on S.Id=C.SiteiD';

-- View: dbo.customer_dashboard_monitor_data -> dbo.CustomerDashboardMonitorData
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[CustomerDashboardMonitorData] AS 
SELECT  count(*) as nr,
		case 
			when FleetNr is null then ''New'' 
			when [ContractNumber] is null then ''NotUsed''
			when Offline = 1 AND D.EndDate IS NULL then ''Offline''
			when Offline = 0 AND D.EndDate IS NULL then ''Online''
			else ''Other'' 
		End as MonitorState,
        SU.UserId
  FROM [dbo].[MonitorsList] M
  left join [dbo].[Deployments] D on D.MonitorId = M.Id and D.EndDate is null
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
  WHERE FleetNr IS NOT NULL AND C.SiteiD IS NOT NULL
 Group By
  		case 
            when FleetNr is null then ''New'' 
			when [ContractNumber] is null then ''NotUsed''
			when Offline = 1 AND D.EndDate IS NULL then ''Offline''
			when Offline = 0 AND D.EndDate IS NULL then ''Online''
			else ''Other'' 
		End,
        SU.UserId';

-- View: dbo.customer_dashboard_notification_data -> dbo.CustomerDashboardNotificationData
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[CustomerDashboardNotificationData] AS 
SELECT  count(*) as nr,
		N.AlertType,
		case 
			when ClosedTime IS NULL then ''Open''
			else ''Closed'' 
		End as AlertState,
        SU.UserId
  FROM [dbo].[Notifications] N
--   left join [dbo].[RvtAlertRules] A on A.Id = N.AlertlevelId
  left join [dbo].[MonitorsList] M on N.MonitorId = M.Id
--  left join [dbo].[Deployments] D on D.MonitorId = M.Id and D.EndDate is null -- THIS END DATE PART IS WRONG
  left join [dbo].[Deployments] D on D.MonitorId = M.Id and N.NotificationTime between D.StartDate and isnull(D.EndDate , Getdate())
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
  WHERE FleetNr IS NOT NULL AND C.SiteiD IS NOT NULL
 Group By
  		N.AlertType,
		case 
			when ClosedTime IS NULL then ''Open''
			else ''Closed'' 
		End,
        SU.UserId';

-- View: dbo.monitor_current_search -> dbo.MonitorCurrentSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MonitorCurrentSearch] AS 
SELECT M.[Id]
      ,D.Id as DeploymentId
      ,[FleetNr]
      ,[SerialId]
      --,[Manufacturer]
      --,[Model]
      --,[FirmwareVersion]
      ,[TypeOfMonitor]
	  ,M.Offline as OffLine
	  --,M.BatteryStatus   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.BatteryStatus ,0)>0  THEN 1  ELSE 0   END AS bit) as Battery
	  ,CAST(CASE   WHEN IsNUll(Alerts.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Alerts
	  ,CAST(CASE   WHEN IsNUll(Cautions.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Cautions
       ,[LocationId] 
	  , D.Lat as Latitude
	  , D.Lng as Longitude 
 
	  ,D.What3words
	  ,C.Id as ContractId
	  ,C.ContractNumber
	  ,SiteiD
	  ,SiteName
       --,[LocationAddress]
      --,[TimeZone]
      --,[CustomerId]
      --,[CustomerDisplayName]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[ListedAtTime]
	  ,CASE   
		  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
	  --,[LastDataTime1Min] as LastDataTime
      --,[LastDataTime15Min]
      --,[LastDataTime1Hour]
      --,[LastDataTime24Hour]
  FROM [dbo].[MonitorsList] M
  left join [dbo].[Deployments] D on D.MonitorId = M.Id and D.EndDate is null
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 0 Group by MonitorId) as Alerts on Alerts.MonitorId =M.Id
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 1 Group by MonitorId) as Cautions on Cautions.MonitorId =M.Id
  left join  (SELECT Top 1 DATEADD(SECOND, -(AveragingPeriod), GETUTCDATE())  as OfflineTime ,AlertType  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 2  ) as Offlines  on AlertType=2';

-- View: dbo.monitor_report -> dbo.MonitorReport
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MonitorReport] AS 

SELECT M.[Id]
	  ,CAST(CASE WHEN D.enddate is null  THEN 1  ELSE 0 END AS bit) as Active
	  ,D.Id as DeploymentId
      ,[FleetNr]
      ,[SerialId]
      --,[Manufacturer]
      --,[Model]
      --,[FirmwareVersion]
      ,[TypeOfMonitor]
	  ,CAST(CASE   WHEN ISNULL([LastDataTime1Min] ,''1970-01-01'') > OfflineTime or  ISNULL(LastDataTime15Min ,''1970-01-01'') > OfflineTime    THEN 0  ELSE 1   END AS bit) as OffLine
	  ,CAST(CASE   WHEN IsNUll(Alerts.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Alerts
	  ,CAST(CASE   WHEN IsNUll(Cautions.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Cautions
       ,[LocationId] 
	  , D.Lat as Latitude
	  , D.Lng as Longitude 
      , D.[Location]
	  ,D.StartDate
	  ,D.EndDate
	  ,D.What3words
	  ,C.Id as ContractId
	  ,C.ContractNumber
	  ,SiteiD
	  ,SiteName
      ,M.CalibrationDate
       --,[LocationAddress]
      --,[TimeZone]
      --,[CustomerId]
      --,[CustomerDisplayName]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[ListedAtTime]
	  ,CASE   
		  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
	  --,[LastDataTime1Min] as LastDataTime
      --,[LastDataTime15Min]
      --,[LastDataTime1Hour]
      --,[LastDataTime24Hour]
  FROM [dbo].[MonitorsList] M
  left join [dbo].[Deployments] D on D.MonitorId = M.Id --and D.EndDate is null
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 0 Group by MonitorId) as Alerts on Alerts.MonitorId =M.Id
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 1 Group by MonitorId) as Cautions on Cautions.MonitorId =M.Id
  left join  (SELECT Top 1 DATEADD(SECOND, -(AveragingPeriod), GETUTCDATE())  as OfflineTime ,AlertType  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 2  ) as Offlines  on AlertType=2';

-- View: dbo.monitor_search -> dbo.MonitorSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MonitorSearch] AS 

SELECT M.[Id]  
	  ,CAST(CASE WHEN D.enddate is null  THEN 1  ELSE 0 END AS bit) as Active
	  ,D.Id as DeploymentId
      ,[FleetNr]
      ,M.[SerialId] AS  SerialId
	  ,(CASE  WHEN [TypeOfMonitor]=2 THEN isNull(OS.Name, M.CustomerDisplayName) ELSE Null END) AS MonitorName
      --,[Manufacturer]
      --,[Model]
      --,[FirmwareVersion]
      ,[TypeOfMonitor]
	  ,M.Offline as OffLine
	  --,M.BatteryStatus   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.BatteryStatus ,0)>0  THEN 1  ELSE 0   END AS bit) as Battery
	  ,CAST(CASE   WHEN IsNUll(Alerts.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Alerts
	  ,CAST(CASE   WHEN IsNUll(Cautions.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Cautions
       ,[LocationId] 
	  , D.Lat as Latitude
	  , D.Lng as Longitude 
	  ,D.EndDate
	  ,D.What3words
	  ,C.Id as ContractId
	  ,C.ContractNumber
	  ,SiteiD
	  ,SiteName
       --,[LocationAddress]
      --,[TimeZone]
      --,[CustomerId]
      --,[CustomerDisplayName]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[ListedAtTime]
	  ,CASE   
		  WHEN  D.EndDate is not null  THEN D.EndDate  
		  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
	  --,[LastDataTime1Min] as LastDataTime
      --,[LastDataTime15Min]
      --,[LastDataTime1Hour]
      --,[LastDataTime24Hour]
  FROM [dbo].[MonitorsList] M
  left join  [dbo].[OmnidotsSensors] OS on OS.SerialId = M.SerialId
  left join [dbo].[Deployments] D on D.MonitorId = M.Id --and D.EndDate is null
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 0 Group by MonitorId) as Alerts on Alerts.MonitorId =M.Id
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 1 Group by MonitorId) as Cautions on Cautions.MonitorId =M.Id
  left join  (SELECT Top 1 DATEADD(SECOND, -(AveragingPeriod), GETUTCDATE())  as OfflineTime ,AlertType  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 2  ) as Offlines  on AlertType=2';

-- View: dbo.monitor_user_search -> dbo.MonitorUserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MonitorUserSearch] AS 
SELECT M.[Id]	
	  ,CAST(CASE WHEN D.enddate is null  THEN 1  ELSE 0 END AS bit) as Active
	  ,D.Id as DeploymentId
      ,[FleetNr]
      ,[SerialId]
      --,[Manufacturer]
      --,[Model]
      --,[FirmwareVersion]
      ,[TypeOfMonitor]
	  ,M.Offline as OffLine
	  --,M.BatteryStatus   as Battery2
	  ,CAST(CASE   WHEN IsNUll(M.BatteryStatus ,0)>0  THEN 1  ELSE 0   END AS bit) as Battery
	  ,CAST(CASE   WHEN IsNUll(Alerts.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Alerts
	  ,CAST(CASE   WHEN IsNUll(Cautions.nr,0)>0  THEN 1  ELSE 0   END AS bit) as Cautions
      ,[LocationId] 
	  , D.Lat as Latitude
	  , D.Lng as Longitude 
	  ,D.What3words
	  ,C.ContractNumber
	  ,C.SiteiD
	  ,SiteName
	  ,SU.UserId
       --,[LocationAddress]
      --,[TimeZone]
      --,[CustomerId]
      --,[CustomerDisplayName]
      --,[EffectiveSince]
      --,[EffectiveTill]
      --,[ListedAtTime]
      --,[LastDataTime1Min] as LastDataTime
	  ,CASE   
		 WHEN  D.EndDate is not null  THEN D.EndDate  
		 WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
	  --,[LastDataTime15Min]
      --,[LastDataTime1Hour]
      --,[LastDataTime24Hour]

  FROM [dbo].[MonitorsList] M
  left join [dbo].[Deployments] D on D.MonitorId = M.Id --and D.EndDate is null
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 0 Group by MonitorId) as Alerts on Alerts.MonitorId =M.Id
  left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 1 Group by MonitorId) as Cautions on Cautions.MonitorId =M.Id
  left join  (SELECT Top 1 DATEADD(SECOND, -(AveragingPeriod), GETUTCDATE())  as OfflineTime ,AlertType  FROM [dbo].[RvtAlertRules]   where   IsActive =1 and AlertType= 2  ) as Offlines  on AlertType=2';

-- View: dbo.my_atm_dust_level_1_day_avg -> dbo.MyAtmDustLevel1DayAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MyAtmDustLevel1DayAvg] AS 

SELECT D.[SerialId]
	  ,DE.Id AS DeploymentId
      ,[Avrg]
      ,[SampleTime]
      ,[Pm1]
      ,[Pm2_5]
      ,[Pm10]
      ,[PmTotal]
  FROM [dbo].[MyAtmDustLevels] D
 join [dbo].[MonitorsList] m on M.SerialId = D.SerialId
 join [dbo].[Deployments] De on DE.MonitorId =  m.Id 
 where Avrg = 86400 and D.SampleTime between  de.StartDate and IsNull(de.EndDate, GetDATE())';

-- View: dbo.my_atm_dust_level_8_hour_avg -> dbo.MyAtmDustLevel8hourAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[MyAtmDustLevel8hourAvg] AS 
 SELECT [SerialId]
      ,IsNull(DATEADD(hh, (FORMAT(SampleTime,''HH'')/8+1)*8, DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0)),GETUTCDATE()) as SampleTime --Isnull ony to get the imported variable to not nullable
      ,avg([Pm1]) as [Pm1]
      ,avg([Pm2_5]) as  [Pm2_5]
      ,avg([Pm10]) as [Pm10]
      ,avg([PmTotal]) as [PmTotal]  
  FROM [dbo].[MyAtmDustLevels]
  where Avrg = 60 
  Group by [SerialId], DATEADD(hh, (FORMAT(SampleTime,''HH'')/8+1)*8, DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0))';

-- View: dbo.noise_level_15_min_avg -> dbo.NoiseLevel15minAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NoiseLevel15minAvg] AS 
 
	SELECT [SerialId]
		  ,[SampleTime]
		  ,[LAeq]
		  ,[LAmax]
		  ,[LA90]
		  ,[LA10]
		  ,[LCeq]
		  ,[LCmax]
		  ,[LC90]
		  ,[LC10]
	  FROM [dbo].[AirQNoiseLevels]
 
 UNION all

  SELECT [SerialId]
      ,DATEADD(minute, (DATEDIFF(minute, 0, SampleTime) / 15) * 15, 0)  as SampleTime --Isnull ony to get the imported variable to not nullable
      ,avg([LAeq]) as [LAeq]
      ,max([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,max([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]  
  FROM [dbo].[SvantekNoiseLevels]
  Group by [SerialId], DATEADD(minute, (DATEDIFF(minute, 0, SampleTime) / 15) * 15, 0)';

-- View: dbo.noise_level_1_day_avg -> dbo.NoiseLevel1dayAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NoiseLevel1dayAvg] AS 
  SELECT [SerialId]
      , CAST(SampleTime as date) as SampleTime  
      ,avg([LAeq]) as [LAeq]
      ,max([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,max([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]  
  FROM [dbo].[AirQNoiseLevels]   
  Group by  [SerialId], CAST(SampleTime as date)

Union all

  SELECT [SerialId]
      , CAST(SampleTime as date) as SampleTime  
      ,avg([LAeq]) as [LAeq]
      ,max([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,max([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]  
  FROM [dbo].[SvantekNoiseLevels]   
  Group by  [SerialId], CAST(SampleTime as date)';

-- View: dbo.noise_level_1_hour_avg -> dbo.NoiseLevel1hourAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NoiseLevel1hourAvg] AS 
 SELECT [SerialId]
      ,IsNull(DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0)),GETUTCDATE()) as SampleTime --Isnull ony to get the imported variable to not nullable
      ,avg([LAeq]) as [LAeq]
      ,max([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,max([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10]   
  FROM [dbo].[AirQNoiseLevels]
  Group by [SerialId], DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0))
 
 UNION all

  SELECT [SerialId]
      ,IsNull(DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0)),GETUTCDATE()) as SampleTime --Isnull ony to get the imported variable to not nullable
      ,avg([LAeq]) as [LAeq]
      ,max([LAmax]) as  [LAmax]
      ,avg([LA90]) as [LA90]
      ,avg([LA10]) as [LA10]  
      ,avg([LCeq]) as [LCeq]
      ,max([LCmax]) as  [LCmax]
      ,avg([LC90]) as [LC90]
      ,avg([LC10]) as [LC10] 
  FROM [dbo].[SvantekNoiseLevels]
  Group by [SerialId], DATEADD(hh, (FORMAT(SampleTime,''HH'')+1), DATEADD(dd, DATEDIFF(dd, 0, SampleTime), 0))';

-- View: dbo.noise_level_site_avg -> dbo.NoiseLevelSiteAvg
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NoiseLevelSiteAvg] AS 
SELECT
M.SerialId, 
    CollectionTime AS SampleTime,
    LAeq,
    LAmax,
    LA90,
    LA10,
    LCeq,
    LCmax,
    LC90,
    LC10
    FROM
    (
        SELECT SiteId, MonitorId, CollectionTime, Level, Field
        FROM [dbo].[SiteAverages]
    ) A
    pivot
    (
        max(Level)
        FOR Field IN (LAeq, LAmax, LA90, LA10, LCeq, LCmax, LC90, LC10)
    ) piv
  INNER JOIN [dbo].[MonitorsList] M ON M.Id = MonitorId';

-- View: dbo.notification_search -> dbo.NotificationSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NotificationSearch] AS 
SELECT N.[Id]
      ,M.Id AS [MonitorId]
      ,[FleetNr]
      ,M.SerialId
      ,[TypeOfMonitor]
	  ,N.AlertType as AlertType
      ,N.ClosedTime as [ClosedDate]
      ,N.AlertField
      ,N.LimitOn
      ,N.[Level]
      ,N.NotificationTime
	  ,C.Id as ContractId
	  ,C.ContractNumber
	  ,SiteiD
	  ,SiteName
	  ,CASE   
		  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
  FROM [dbo].[Notifications] N
  left join [dbo].[MonitorsList] M on N.MonitorId = M.Id
  join [dbo].[Deployments] D on D.MonitorId = M.Id and N.NotificationTime between D.StartDate and isnull(D.EndDate , Getdate())
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD';

-- View: dbo.notification_user_search -> dbo.NotificationUserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[NotificationUserSearch] AS 
SELECT N.[Id]
      ,[FleetNr]
      ,M.Id AS [MonitorId]
      ,M.SerialId
      ,[TypeOfMonitor]
	  ,N.AlertType as AlertType
      ,N.ClosedTime as [ClosedDate]
      ,N.AlertField
      ,N.LimitOn
      ,N.[Level]
      ,N.NotificationTime
	  ,C.Id as ContractId
	  ,C.ContractNumber
	  ,C.SiteiD
	  ,SiteName
	  ,SU.UserId
	  ,CASE   
		  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		  ELSE  LastDataTime24Hour  
		END as LastDataTime
  FROM [dbo].[Notifications] N
--   left join [dbo].[RvtAlertRules] A on A.Id = N.AlertlevelId
  left join [dbo].[MonitorsList] M on N.MonitorId = M.Id
  left join [dbo].[Deployments] D on D.MonitorId = M.Id and N.NotificationTime between D.StartDate and isnull(D.EndDate , Getdate())
  left join [dbo].[Contracts] C on C.Id = D.ContractId 
  left join [dbo].[Sites] S on S.Id = C.SiteiD
  join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
   --UNION
  --SELECT M.[Id]
  --    ,[FleetNr]
  --    ,M.SerialId
  --    ,[TypeOfMonitor]
	 -- ,CAST(CASE   WHEN ISNULL([LastDataTime1Min] ,''1970-01-01'') > dbo.fnOfflineDateTime() or  ISNULL(LastDataTime15Min ,''1970-01-01'') > dbo.fnOfflineDateTime()    THEN 0  ELSE 1   END AS bit) as OffLine
	 -- ,CAST(0 AS bit) as Alerts
	 -- ,CAST(0 AS bit) as Cautions
  --    ,NULL as [ClosedDate]
  --    ,'''' AS AlertField
  --    ,NULL AS LimitOn
  --    ,NULL AS Level
  --    ,CASE   
		--  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		--  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		--  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		--  ELSE  LastDataTime24Hour  
		--END as NotificationTime
	 -- ,C.Id as ContractId
	 -- ,C.ContractNumber
	 -- ,C.SiteiD
	 -- ,SiteName
	 -- ,SU.UserId
	 -- ,CASE   
		--  WHEN  [LastDataTime1Min] is not null  THEN [LastDataTime1Min]  
		--  WHEN  LastDataTime15Min is not null  THEN LastDataTime15Min  
		--  WHEN  LastDataTime1Hour is not null  THEN LastDataTime1Hour  
		--  ELSE  LastDataTime24Hour  
		--END as LastDataTime
  --FROM [dbo].[MonitorsList] M
  --left join [dbo].[Deployments] D on D.MonitorId = M.Id and D.EndDate is null
  --left join [dbo].[Contracts] C on C.Id = D.ContractId 
  --left join [dbo].[Sites] S on S.Id = C.SiteiD
  --join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
  --left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   AlertType= 0 Group by MonitorId) as Alerts on Alerts.MonitorId =M.Id
  --left join  (SELECT count(*) as nr ,MonitorId  FROM [dbo].[RvtAlertRules]   where   AlertType= 1 Group by MonitorId) as Cautions on Cautions.MonitorId =M.Id';

-- View: dbo.omnidots_peak_level_15_min -> dbo.OmnidotsPeakLevel15min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsPeakLevel15min] AS

 SELECT  
	SerialId,
	ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 15 * 15, 0),GETDATE()) AS SampleTime,
    max([XVtop]) AS [XVtop],
    max([YVtop]) AS [YVtop],
    max([ZVtop]) AS [ZVtop]
 FROM 
	[dbo].[OmnidotsPeakLevels]
GROUP BY 
    SerialId, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 15 * 15, 0);';

-- View: dbo.omnidots_peak_level_1_day_peak -> dbo.OmnidotsPeakLevel1dayPeak
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsPeakLevel1dayPeak] AS 
	SELECT  [SerialId]
		  , CAST(SampleTime as date) as SampleTime  
		  --,[SampleTime]
		  --,[XFdom]
		  ,MAX([XVtop]) as [XVtop]
		  --,Max([XVtopOverflow]) as [XVtop]
		  --,[YFdom]
		  ,MAX([YVtop]) as [YVtop]
		  --,MAX([YVtopOverflow]) as [YVtop]
		  --,[ZFdom]
		  ,MAX([ZVtop]) as [ZVtop]
		  --,MAX([ZVtopOverflow])  as [ZVtop]
	   FROM [dbo].[OmnidotsPeakLevels]
	   Group by  [SerialId], CAST(SampleTime as date)';

-- View: dbo.omnidots_peak_level_1_min -> dbo.OmnidotsPeakLevel1min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsPeakLevel1min] AS

 SELECT  
    SerialId,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 1 * 1, 0),GETDATE()) AS SampleTime,
    max([XVtop]) AS [XVtop],
    max([YVtop]) AS [YVtop],
    max([ZVtop]) AS [ZVtop]
 FROM 
	[dbo].[OmnidotsPeakLevels]
GROUP BY 
     SerialId, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 1 * 1, 0);';

-- View: dbo.omnidots_peak_level_20_min -> dbo.OmnidotsPeakLevel20min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsPeakLevel20min] AS

 SELECT  
	SerialId,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 20 * 20, 0),GETDATE()) AS SampleTime,
    max([XVtop]) AS [XVtop],
    max([YVtop]) AS [YVtop],
    max([ZVtop]) AS [ZVtop]
 FROM 
	[dbo].[OmnidotsPeakLevels]
GROUP BY 
    SerialId, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 20 * 20, 0);';

-- View: dbo.omnidots_peak_level_5_min -> dbo.OmnidotsPeakLevel5min
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsPeakLevel5min] AS

 SELECT  
    SerialId,
    ISNULL(DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 5 * 5, 0),GETDATE()) AS SampleTime,
    max([XVtop]) AS [XVtop],
    max([YVtop]) AS [YVtop],
    max([ZVtop]) AS [ZVtop]
 FROM 
	[dbo].[OmnidotsPeakLevels]
GROUP BY 
     SerialId, DATEADD(MINUTE, DATEDIFF(MINUTE, 0, [SampleTime]) / 5 * 5, 0);';

-- View: dbo.omnidots_read_status -> dbo.OmnidotsReadStatus
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[OmnidotsReadStatus] AS 
		WITH cte AS
		(
			SELECT SerialID,SampleTime,
			ROW_NUMBER() OVER (PARTITION BY SerialId ORDER BY SampleTime DESC) AS rn
			FROM [OmnidotsPeakLevels] where SerialId in (
				SELECT [SerialId]   FROM [dbo].[MonitorsList]  M
				join [dbo].[Deployments] D on D.MonitorId=M.Id
				where TypeOfMonitor =2 )
		)
		SELECT  M.FleetNr,C.SerialID,C.SampleTime, M.LastDataTime1Min, O.[Lastseen], O.[Online]
		FROM cte C
		join [dbo].[MonitorsList]  M on M.SerialId = C.SerialId
		join [dbo].[OmnidotsSensors]  O on O.SerialId = C.SerialId
		WHERE rn = 1 --and C.SampleTime != M.LastDataTime1Min and fleetnr=''R78522V''
		--and O.[Online] = 1
		--order by M.LastDataTime1Min desc,C.serialId';

-- View: dbo.report_rule_search -> dbo.ReportRuleSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportRuleSearch] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.Frequency
                          ,R.DayOfWeek
                          ,R.DayOfMonth
                          ,R.ReportName
                          ,R.LastGenerated
                      FROM [dbo].[Sites] S
  				    left join [dbo].[SiteUsers] SU on  S.Id = SU.SiteId and  Su.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU.UserId
                    left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct C.Id as CompanyId,CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
                    inner join [dbo].[ReportRules] R on R.SiteId = S.Id
                    WHERE R.Deleted = 0';

-- View: dbo.report_rule_user_search -> dbo.ReportRuleUserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportRuleUserSearch] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.Frequency
                          ,R.DayOfWeek
                          ,R.DayOfMonth
                          ,R.ReportName
                          ,R.LastGenerated
						  ,su.UserId
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
					join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
                    left join [dbo].[SiteUsers] SU2 on  S.Id = SU2.SiteId and  Su2.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU2.UserId
                    inner join [dbo].[ReportRules] R on R.SiteId = S.Id
                    WHERE R.Deleted = 0';

-- View: dbo.report_search -> dbo.ReportSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportSearch] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.ReportDate
                          ,R.ReportFrom
                          ,R.ReportTo
                          ,R.ReportLink
                          ,R.ReportRuleId
                          ,R.Frequency
                          ,RR.ReportName
                          ,RR.Deleted
						  ,Contracts
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
                    inner join [dbo].[Reports] R on R.SiteId = S.Id
                    inner join [dbo].[ReportRules] RR on RR.Id = R.ReportRuleId';

-- View: dbo.report_search_2 -> dbo.ReportSearch2
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportSearch2] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.ReportDate
                          ,R.ReportFrom
                          ,R.ReportTo
                          ,R.ReportLink
                          ,R.ReportRuleId
                          ,R.Frequency
                          ,RR.ReportName
                          ,RR.Deleted
						  ,contracts  as Contracts
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts] where SiteiD is not null
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
                    inner join [dbo].[Reports] R on R.SiteId = S.Id
                    inner join [dbo].[ReportRules] RR on RR.Id = R.ReportRuleId';

-- View: dbo.report_search_4 -> dbo.ReportSearch4
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportSearch4] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.ReportDate
                          ,R.ReportFrom
                          ,R.ReportTo
                          ,R.ReportLink
                          ,R.ReportRuleId
                          ,R.Frequency
                          ,RR.ReportName
                          ,RR.Deleted
						  ,'''' as Contracts
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
					join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
                    left join [dbo].[SiteUsers] SU2 on  S.Id = SU2.SiteId and  Su2.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU2.UserId
                    inner join [dbo].[Reports] R on R.SiteId = S.Id
                    inner join [dbo].[ReportRules] RR on RR.Id = R.ReportRuleId';

-- View: dbo.report_user_search -> dbo.ReportUserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[ReportUserSearch] AS 
                      SELECT   R.Id
	                      ,[SiteName]
                          ,R.SiteId
                          ,R.ReportDate
                          ,R.ReportFrom
                          ,R.ReportTo
                          ,R.ReportLink
                          ,R.ReportRuleId
                          ,R.Frequency
                          ,RR.ReportName
                          ,RR.Deleted
						  ,su.UserId
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
					join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
                    left join [dbo].[SiteUsers] SU2 on  S.Id = SU2.SiteId and  Su2.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU2.UserId
                    inner join [dbo].[Reports] R on R.SiteId = S.Id
                    inner join [dbo].[ReportRules] RR on RR.Id = R.ReportRuleId';

-- View: dbo.site_search -> dbo.SiteSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[SiteSearch] AS 
                      SELECT   S.Id
	                      ,[SiteName]
						  ,S.Archived
                          ,[CreateDate]
                          ,[AddressLine1]
                          ,[AddressLine2]
                          ,[Postcode]
                          ,[City]
                          ,[County]
	                      ,ISNULL([AddressLine1], '''') +'' ''+  ISNULL([AddressLine2], '''')  +'' ''+  ISNULL([Postcode], '''') +'' ''+  ISNULL([City], '''') +'' ''+  ISNULL([County], '''')   as siteAddress
	                      ,con.contracts
	                      ,Comp.CompanyName
						  ,Comp.CompanyId
						  ,U.Email as SiteContact
                      FROM [dbo].[Sites] S
  				    left join [dbo].[SiteUsers] SU on  S.Id = SU.SiteId and  Su.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU.UserId
                    left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct C.Id as CompanyId,CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id';

-- View: dbo.site_user_search -> dbo.SiteUserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[SiteUserSearch] AS 
                      SELECT   S.Id
	                      ,[SiteName]
						  ,S.Archived
                          ,[CreateDate]
                          ,[AddressLine1]
                          ,[AddressLine2]
                          ,[Postcode]
                          ,[City]
                          ,[County]
	                      ,S.[AddressLine1]+'' ''+  S.[AddressLine2]+'' ''+  S.[Postcode]+'' ''+  S.[City]+'' ''+  S.[County] as siteAddress
	                      ,con.contracts
	                      ,Comp.CompanyName
						  ,U.Email as SiteContact
						  ,su.UserId
                      FROM [dbo].[Sites] S
					 left join (
		                    SELECT STRING_AGG([ContractNumber], '', '') as contracts, [SiteiD] as [SiteiD]  
		                    FROM [dbo].[Contracts]
		                     Group by [SiteiD]
                    )  con  on con.SiteiD = S.Id
                    left join (SELECT distinct CompanyName,  SiteiD   FROM [dbo].[Companies] C  inner join dbo.Contracts con on con.CompanyId =c.Id  inner join dbo.Sites s on s.Id = con.SiteiD) comp  on comp.SiteiD = S.Id
					join  [dbo].[SiteUsers] SU on SU.SiteId = S.Id
                    left join [dbo].[SiteUsers] SU2 on  S.Id = SU2.SiteId and  Su2.siteContact=1
					left join dbo.AspNetUsers U on U.Id=SU2.UserId';

-- View: dbo.user_search -> dbo.UserSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[UserSearch] AS 
                        SELECT  U.[Id]
                              ,[CompanyId]
                              ,[IsDisabled]
                              ,U.[Name]
                              ,[UserName]
							  ,CompanyRole
                              ,[NormalizedUserName]
                              ,[Email]
                              ,[EmailConfirmed]
                              ,U.[ConcurrencyStamp]
                              ,[PhoneNumber]
                              ,[PhoneNumberConfirmed]
                              ,[TwoFactorEnabled]
                              ,[LockoutEnd]
                              ,[LockoutEnabled]
                              ,[AccessFailedCount]
	                          ,R.Name as Role
	                          ,C.CompanyName  
	                          ,ISNULL(SU.NrSites,0) as NrSites
                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[Companies] C on C.Id = U.CompanyId
						  left join (Select Count(*) as NrSites,UserId from  [dbo].[SiteUsers] group by UserId) as SU     on U.Id = SU.UserId';

-- View: dbo.users_for_report_search -> dbo.UsersForReportSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[UsersForReportSearch] AS 
                        SELECT  U.[Id]
                              ,[CompanyId]
                              ,[IsDisabled]
                              ,U.[Name]
                              ,[UserName]
							  ,CompanyRole
                              ,[NormalizedUserName]
                              ,[Email]
                              ,[EmailConfirmed]
                              ,U.[ConcurrencyStamp]
                              ,[PhoneNumber]
                              ,[PhoneNumberConfirmed]
                              ,[TwoFactorEnabled]
                              ,[LockoutEnd]
                              ,[LockoutEnabled]
                              ,[AccessFailedCount]
	                          ,R.Name as Role
	                          ,C.CompanyName  
	                        --   ,ISNULL(SU.NrSites,0) as NrSites
							--   ,SU2.SiteId
							--   ,SU2.SiteContact
                              ,RU.ReportRuleId

                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[Companies] C on C.Id = U.CompanyId
						--   left join (Select Count(*) as NrSites,UserId from  [dbo].[SiteUsers] group by UserId) as SU     on U.Id = SU.UserId
                        --   left join [dbo].[SiteUsers] SU2 on  U.Id = SU2.UserId
						  left join [dbo].[ReportUsers] RU on  U.Id = RU.UserId
                          inner join [dbo].[ReportRules] RR on  RU.ReportRuleId = RR.Id';

-- View: dbo.users_for_site_search -> dbo.UsersForSiteSearch
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW [dbo].[UsersForSiteSearch] AS 
                        SELECT  U.[Id]
                              ,[CompanyId]
                              ,[IsDisabled]
                              ,U.[Name]
                              ,[UserName]
							  ,CompanyRole
                              ,[NormalizedUserName]
                              ,[Email]
                              ,[EmailConfirmed]
                              ,U.[ConcurrencyStamp]
                              ,[PhoneNumber]
                              ,[PhoneNumberConfirmed]
                              ,[TwoFactorEnabled]
                              ,[LockoutEnd]
                              ,[LockoutEnabled]
                              ,[AccessFailedCount]
	                          ,R.Name as Role
	                          ,C.CompanyName  
	                          ,ISNULL(SU.NrSites,0) as NrSites
							  ,SU2.SiteId
							  ,SU2.SiteContact

                          FROM [dbo].[AspNetUsers] U
                          join [dbo].[AspNetUserRoles] RL on RL.UserId = U.Id
                          join [dbo].[AspNetRoles] R on R.Id =RL.RoleId
                          left join [dbo].[Companies] C on C.Id = U.CompanyId
						  left join (Select Count(*) as NrSites,UserId from  [dbo].[SiteUsers] group by UserId) as SU     on U.Id = SU.UserId
						  left join [dbo].[SiteUsers] SU2 on  U.Id = SU2.UserId';

COMMIT TRANSACTION;

# SQL Server Test Schema Inventory

Captured on 2026-06-20 from SQL Server fixture files with:

```bash
rg -n "CREATE TABLE|CREATE INDEX|ALTER TABLE|CREATE TRIGGER" */*Tests/testdata/create.sql
```

Source files:

- `airqmonitor/AirQMonitorTests/testdata/create.sql`
- `myatmmonitor/MyAtmMonitorTests/testdata/create.sql`
- `omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql`
- `svantekmonitor/SvantekMonitorTests/testdata/create.sql`

## Shared Tables

- MonitorsList
- Deployments
- Contracts
- Sites
- SiteUsers
- NotificationSettings
- Notifications
- NotificationsSent
- RvtAlertRules
- AspNetUsers
- ErrorMessages

## Monitor-Specific Tables

- AirQ: AirQMonitorStatus, AirQNoiseLevels, AirQErrorMessages, SiteAverages
- MyAtm: MyAtmDustLevels, MyAtmAccessoryInfo, MyAtmErrorMessages
- Omnidots: OmnidotsMonitorStatus, OmnidotsSensors, OmnidotsPeakLevels, OmnidotsVeffLevels, OmnidotsVdvLevels, OmnidotsTracesIndex, OmnidotsTraces, OmnidotsErrorMessages
- Svantek: SvantekMonitorStatus, SvantekNoiseLevels, SvantekErrorMessages, SiteAverages

Note: The EF migration plan lists AirQNoise8HourAverage, MyAtmDustLevel8hourAvg, and SvantekNoise8HourAverage as monitor-specific targets. Those tables are referenced by the current provider mapping/code paths, but they are not created by the SQL Server fixture `CREATE TABLE` inventory captured here.

## Fixture Column Summary

### Shared Tables

- MonitorsList: Id, FleetNr, SerialId, Manufacturer, Model, FirmwareVersion, TypeOfMonitor, LocationId, Latitude, Longitude, LocationAddress, TimeZone, CustomerId, CustomerDisplayName, ListedAtTime, LastDataTime1Min, LastDataTime15Min, LastDataTime1Hour, LastDataTime24Hour, Offline, BatteryStatus.
- Deployments: Id, StartDate, EndDate, Lng, Lat, What2words or What3Words, PictureLink, ContractId, MonitorId.
- Contracts: Id, ContractNumber, OnHireDate, OffHireDate, CompanyId, SiteId.
- Sites: Id, SiteName, CreateDate, AddressLine1, AddressLine2, Postcode, City, County, StartTime, EndTime, SatStartTime, SatEndTime, SunStartTime, SunEndTime.
- SiteUsers: Id, StartDate, EndDate, UserId, SiteId.
- NotificationSettings: Id, Email, SMS, StartTime, EndTime, SiteUserId.
- Notifications: Id, NotificationTime, LimitOn, AveragingPeriod, Level, ClosedTime, ClosedByUser, ClosedByNote, MonitorId, AlertField, AlertType.
- NotificationsSent: Id, SendTime, Address, ErrorMessage, NotificationId.
- RvtAlertRules: Id, MonitorId, SerialId, AlertField, LimitOn, LimitOff, AlertType, IsActive, AveragingPeriod, Weekdays, Saturdays, Sundays, StartTime, EndTime, IsDeleted, Created, Accessed.
- AspNetUsers: Id, CompanyId, IsDisabled, Name, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, CompanyRole.
- ErrorMessages: Host, Source, Message, Level, StackTrace, Variables, LogTime.

### AirQ

- AirQMonitorStatus: SerialId, UpdateTime, Status, ErrorCount, BatteryVoltage, CalibrationDate, FilterChangeDate, PumpHours.
- AirQNoiseLevels: SerialId, SampleTime, LAeq, LAmax, LA90, LA10, LCeq, LCmax, LC90, LC10.
- AirQErrorMessages: Tag, Error, ErrorTime.
- SiteAverages: Id, SiteId, MonitorId, Field, Level, CollectionTime.

### MyAtm

- MyAtmDustLevels: SerialId, Avrg, SampleTime, Pm1, Pm2_5, Pm10, PmTotal, Weather_t, Weather_p, Weather_rh.
- MyAtmAccessoryInfo: SerialId, SampleTime, OperatingSpanPointDeviation, OperatingTLed, OperatingTHeating, OperatingVolumeFlow, OperatingVolumeFlowSignalLength, OperatingVolumeFlowTimestamp, OperatingPeakPosition15s, OperatingVelocity, OperatingSlaNoiseLevel, OperatingSlaOffsetAdjustmentVoltage, OperatingTMio, OperatingPMio, OperatingRHMio, OperatingAutoCalibrationPeakPosition, OperatingPowerLed, OperatingPowerPmt, OperatingPowerHeating, OperatingPowerVolumeFlowBlower, OperatingPowerHousingBlower, OperatingPowerSeparatorBlower, OperatingFlowCorrectionFactor, DigitalCalibrationEnableStatus, DigitalIadsConnected, DigitalIadsActivated, DigitalAmbientProtectionAttached, DigitalCoincidence, DigitalWeatherStation, DigitalOperatingModus, DigitalVolumeFlow, DigitalSuction, DigitalIads, DigitalCalibration, DigitalSensorLed, DigitalSensorData, DigitalSensorNoise, DigitalCountModus, DigitalLiquidPumps, DigitalCondensationCooling, DigitalDropletSize, DigitalOpticsTemperature, DigitalGlobalWarning, DigitalGlobalError, DigitalEvaporationHeating.
- MyAtmErrorMessages: Tag, Error, ErrorTime.

### Omnidots

- OmnidotsMonitorStatus: Id, SerialId, MeasurementDuration, DataSaveLevel, VdvEnabled, VdvX, VdvY, VdvZ, VdvPeriod, TraceSaveLevel, TracePreTrigger, TracePostTrigger, AlarmValue, FlatLevel, DisableLed, LogFlushInterval, GuideLine, BuildingLevel, VectorEnabled, VtopEnabled, AtopEnabled.
- OmnidotsSensors: Id, SerialId, Name, Lastseen, BatteryCharge, ConnectedUsing, Online.
- OmnidotsPeakLevels: SerialId, SampleTime, XFdom, XVtop, XVtopOverflow, YFdom, YVtop, YVtopOverflow, ZFdom, ZVtop, ZVtopOverflow.
- OmnidotsVeffLevels: SerialId, SampleTime, X, Y, Z.
- OmnidotsVdvLevels: SerialId, SampleTime, X, Y, Z, VdvX, VdvY, VdvZ.
- OmnidotsTracesIndex: Id, SerialId, StartTime, EndTime.
- OmnidotsTraces: TraceId, X, Y, Z.
- OmnidotsErrorMessages: Tag, Error, ErrorTime.

### Svantek

- SvantekMonitorStatus: SerialId, UpdateTime, Status, ErrorCount, BatteryVoltage, CalibrationDate, FilterChangeDate, PumpHours, project_id, point_id, active, lastlogin, lastlogout, isonline, laststatustimestamp, batterycharge, batterytimetoempty, powersource, isbatterycharging, gsmsignalquality, measurementstate.
- SvantekNoiseLevels: SerialId, SampleTime, LAeq, LAmax, LA90, LA10, LCeq, LCmax, LC90, LC10.
- SvantekErrorMessages: Tag, Error, ErrorTime.
- SiteAverages: Id, SiteId, MonitorId, Field, Level, CollectionTime.

## Indexes And Triggers

- AirQ: `ixnoise_serialiid_sampletime` on AirQNoiseLevels(SerialId, SampleTime); `tr_AirQErrorMessages_FromCommon`; `tr_MonitorsList_DefaultDeployment`.
- MyAtm: `ixdust_serialiid_sampletime` on MyAtmDustLevels(SerialId, SampleTime); `tr_MyAtmErrorMessages_FromCommon`; `tr_MonitorsList_DefaultDeployment`.
- Omnidots: `ix_serialiid_sampletime` on OmnidotsPeakLevels(SerialId, SampleTime); `ixveff_serialiid_sampletime` on OmnidotsVeffLevels(SerialId, SampleTime); `ixvdv_serialiid_sampletime` on OmnidotsVdvLevels(SerialId, SampleTime); `ix_traces` on OmnidotsTraces(TraceId); `tr_OmnidotsSensors_FromMonitor`; `tr_OmnidotsErrorMessages_FromCommon`; `tr_MonitorsList_DefaultDeployment`.
- Svantek: `ixnoise_serialiid_sampletime` on SvantekNoiseLevels(SerialId, SampleTime); `tr_SvantekErrorMessages_FromCommon`; `tr_MonitorsList_DefaultDeployment`.

## Raw Inventory

```text
svantekmonitor/SvantekMonitorTests/testdata/create.sql:11:CREATE TABLE dbo.MonitorsList (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:41:CREATE TABLE SvantekMonitorStatus (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:70:CREATE TABLE dbo.SvantekNoiseLevels (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:83:CREATE INDEX ixnoise_serialiid_sampletime ON dbo.SvantekNoiseLevels(SerialId, SampleTime);
svantekmonitor/SvantekMonitorTests/testdata/create.sql:90:CREATE TABLE dbo.SvantekErrorMessages (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:104:CREATE TABLE dbo.ErrorMessages (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:116:CREATE TRIGGER dbo.tr_SvantekErrorMessages_FromCommon
svantekmonitor/SvantekMonitorTests/testdata/create.sql:133:    ALTER TABLE [dbo].[RvtAlertRules] DROP CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
svantekmonitor/SvantekMonitorTests/testdata/create.sql:140:CREATE TABLE dbo.RvtAlertRules (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:160:ALTER TABLE [dbo].[RvtAlertRules]  WITH CHECK ADD  CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
svantekmonitor/SvantekMonitorTests/testdata/create.sql:165:ALTER TABLE [dbo].[RvtAlertRules] CHECK CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
svantekmonitor/SvantekMonitorTests/testdata/create.sql:185:CREATE TABLE AspNetUsers (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:214:CREATE TABLE Deployments (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:230:CREATE TABLE SiteUsers (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:243:CREATE TABLE NotificationSettings (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:258:CREATE TABLE Contracts (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:269:ALTER TABLE Deployments
svantekmonitor/SvantekMonitorTests/testdata/create.sql:279:CREATE TRIGGER dbo.tr_MonitorsList_DefaultDeployment
svantekmonitor/SvantekMonitorTests/testdata/create.sql:314:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
svantekmonitor/SvantekMonitorTests/testdata/create.sql:323:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
svantekmonitor/SvantekMonitorTests/testdata/create.sql:330:CREATE TABLE dbo.Notifications (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:349:CREATE TABLE dbo.NotificationsSent (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:361:CREATE TABLE dbo.Sites (
svantekmonitor/SvantekMonitorTests/testdata/create.sql:384:CREATE TABLE dbo.SiteAverages (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:10:    ALTER TABLE [dbo].[Deployments] DROP CONSTRAINT [FK_Deployments_MonitorsList_MonitorId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:14:    ALTER TABLE [dbo].[Deployments] DROP CONSTRAINT [FK_Deployments_Contracts_ContractId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:20:CREATE TABLE dbo.Deployments (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:38:CREATE TABLE dbo.Contracts (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:47:ALTER TABLE [dbo].[Deployments]  WITH CHECK ADD  CONSTRAINT [FK_Deployments_Contracts_ContractId] FOREIGN KEY([ContractId])
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:52:ALTER TABLE [dbo].[Deployments] CHECK CONSTRAINT [FK_Deployments_Contracts_ContractId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:61:CREATE TABLE dbo.MonitorsList (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:85:ALTER TABLE [dbo].[Deployments]  WITH CHECK ADD  CONSTRAINT [FK_Deployments_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:90:ALTER TABLE [dbo].[Deployments] CHECK CONSTRAINT [FK_Deployments_MonitorsList_MonitorId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:98:CREATE TABLE dbo.OmnidotsMonitorStatus (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:127:CREATE TABLE dbo.OmnidotsSensors (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:141:CREATE TRIGGER dbo.tr_OmnidotsSensors_FromMonitor
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:170:CREATE TABLE dbo.OmnidotsPeakLevels (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:184:CREATE INDEX ix_serialiid_sampletime ON dbo.OmnidotsPeakLevels(SerialId, SampleTime);
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:191:CREATE TABLE dbo.OmnidotsVeffLevels (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:199:CREATE INDEX ixveff_serialiid_sampletime ON dbo.OmnidotsVeffLevels(SerialId, SampleTime);
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:206:CREATE TABLE dbo.OmnidotsVdvLevels (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:217:CREATE INDEX ixvdv_serialiid_sampletime ON dbo.OmnidotsVdvLevels(SerialId, SampleTime);
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:225:CREATE TABLE dbo.OmnidotsErrorMessages (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:239:CREATE TABLE dbo.ErrorMessages (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:251:CREATE TRIGGER dbo.tr_OmnidotsErrorMessages_FromCommon
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:270:    ALTER TABLE [dbo].[RvtAlertRules] DROP CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:277:CREATE TABLE dbo.RvtAlertRules (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:297:ALTER TABLE [dbo].[RvtAlertRules]  WITH CHECK ADD  CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:302:ALTER TABLE [dbo].[RvtAlertRules] CHECK CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:320:CREATE TABLE AspNetUsers (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:347:CREATE TABLE Deployments (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:363:CREATE TABLE SiteUsers (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:376:CREATE TABLE NotificationSettings (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:390:CREATE TABLE Contracts (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:401:ALTER TABLE Deployments
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:411:CREATE TRIGGER dbo.tr_MonitorsList_DefaultDeployment
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:451:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:458:CREATE TABLE dbo.Notifications (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:478:CREATE TABLE dbo.NotificationsSent (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:492:CREATE TABLE dbo.OmnidotsTracesIndex (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:504:CREATE TABLE dbo.OmnidotsTraces (
omnidotsmonitor/OmnidotsMonitorTests/testdata/create.sql:511:CREATE INDEX ix_traces ON dbo.OmnidotsTraces(TraceId);
airqmonitor/AirQMonitorTests/testdata/create.sql:11:CREATE TABLE dbo.MonitorsList (
airqmonitor/AirQMonitorTests/testdata/create.sql:41:CREATE TABLE AirQMonitorStatus (
airqmonitor/AirQMonitorTests/testdata/create.sql:57:CREATE TABLE dbo.AirQNoiseLevels (
airqmonitor/AirQMonitorTests/testdata/create.sql:70:CREATE INDEX ixnoise_serialiid_sampletime ON dbo.AirQNoiseLevels(SerialId, SampleTime);
airqmonitor/AirQMonitorTests/testdata/create.sql:77:CREATE TABLE dbo.AirQErrorMessages (
airqmonitor/AirQMonitorTests/testdata/create.sql:91:CREATE TABLE dbo.ErrorMessages (
airqmonitor/AirQMonitorTests/testdata/create.sql:103:CREATE TRIGGER dbo.tr_AirQErrorMessages_FromCommon
airqmonitor/AirQMonitorTests/testdata/create.sql:120:    ALTER TABLE [dbo].[RvtAlertRules] DROP CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
airqmonitor/AirQMonitorTests/testdata/create.sql:127:CREATE TABLE dbo.RvtAlertRules (
airqmonitor/AirQMonitorTests/testdata/create.sql:147:ALTER TABLE [dbo].[RvtAlertRules]  WITH CHECK ADD  CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
airqmonitor/AirQMonitorTests/testdata/create.sql:152:ALTER TABLE [dbo].[RvtAlertRules] CHECK CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
airqmonitor/AirQMonitorTests/testdata/create.sql:172:CREATE TABLE AspNetUsers (
airqmonitor/AirQMonitorTests/testdata/create.sql:201:CREATE TABLE Deployments (
airqmonitor/AirQMonitorTests/testdata/create.sql:217:CREATE TABLE SiteUsers (
airqmonitor/AirQMonitorTests/testdata/create.sql:230:CREATE TABLE NotificationSettings (
airqmonitor/AirQMonitorTests/testdata/create.sql:245:CREATE TABLE Contracts (
airqmonitor/AirQMonitorTests/testdata/create.sql:256:ALTER TABLE Deployments
airqmonitor/AirQMonitorTests/testdata/create.sql:266:CREATE TRIGGER dbo.tr_MonitorsList_DefaultDeployment
airqmonitor/AirQMonitorTests/testdata/create.sql:301:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
airqmonitor/AirQMonitorTests/testdata/create.sql:310:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
airqmonitor/AirQMonitorTests/testdata/create.sql:317:CREATE TABLE dbo.Notifications (
airqmonitor/AirQMonitorTests/testdata/create.sql:336:CREATE TABLE dbo.NotificationsSent (
airqmonitor/AirQMonitorTests/testdata/create.sql:348:CREATE TABLE dbo.Sites (
airqmonitor/AirQMonitorTests/testdata/create.sql:371:CREATE TABLE dbo.SiteAverages (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:9:    ALTER TABLE [dbo].[Deployments] DROP CONSTRAINT [FK_Deployments_MonitorsList_MonitorId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:13:    ALTER TABLE [dbo].[Deployments] DROP CONSTRAINT [FK_Deployments_Contracts_ContractId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:19:CREATE TABLE dbo.Deployments (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:37:CREATE TABLE dbo.Contracts (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:46:ALTER TABLE [dbo].[Deployments]  WITH CHECK ADD  CONSTRAINT [FK_Deployments_Contracts_ContractId] FOREIGN KEY([ContractId])
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:51:ALTER TABLE [dbo].[Deployments] CHECK CONSTRAINT [FK_Deployments_Contracts_ContractId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:60:CREATE TABLE dbo.MonitorsList (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:84:ALTER TABLE [dbo].[Deployments]  WITH CHECK ADD  CONSTRAINT [FK_Deployments_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:89:ALTER TABLE [dbo].[Deployments] CHECK CONSTRAINT [FK_Deployments_MonitorsList_MonitorId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:98:CREATE TABLE dbo.MyAtmDustLevels (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:111:CREATE INDEX ixdust_serialiid_sampletime ON dbo.MyAtmDustLevels(SerialId, SampleTime);
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:118:CREATE TABLE dbo.MyAtmErrorMessages (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:132:CREATE TABLE dbo.ErrorMessages (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:144:CREATE TRIGGER dbo.tr_MyAtmErrorMessages_FromCommon
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:163:    ALTER TABLE [dbo].[RvtAlertRules] DROP CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:170:CREATE TABLE dbo.RvtAlertRules (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:190:ALTER TABLE [dbo].[RvtAlertRules]  WITH CHECK ADD  CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId] FOREIGN KEY([MonitorId])
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:195:ALTER TABLE [dbo].[RvtAlertRules] CHECK CONSTRAINT [FK_RvtAlertRules_MonitorsList_MonitorId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:213:CREATE TABLE AspNetUsers (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:240:CREATE TABLE Deployments (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:258:CREATE TABLE SiteUsers (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:271:CREATE TABLE NotificationSettings (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:285:CREATE TABLE Contracts (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:296:ALTER TABLE Deployments
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:306:CREATE TRIGGER dbo.tr_MonitorsList_DefaultDeployment
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:341:    ALTER TABLE [dbo].[Notifications] DROP CONSTRAINT [FK_Notifications_RvtAlertRules_AlertlevelId]
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:348:CREATE TABLE dbo.Notifications (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:370:CREATE TABLE dbo.NotificationsSent (
myatmmonitor/MyAtmMonitorTests/testdata/create.sql:384:CREATE TABLE dbo.MyAtmAccessoryInfo (
```

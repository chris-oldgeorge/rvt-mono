-- Roll back only after an application version that does not require fenced leases is active.

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_MyAtmAlertOccurrences_RecentLookup'
      AND object_id = OBJECT_ID(N'[dbo].[MyAtmAlertOccurrences]'))
BEGIN
    DROP INDEX [IX_MyAtmAlertOccurrences_RecentLookup] ON [dbo].[MyAtmAlertOccurrences];
END;

IF COL_LENGTH(N'dbo.MyAtmOutboxMessages', N'LeaseId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[MyAtmOutboxMessages]
        DROP COLUMN [LeaseId];
END;

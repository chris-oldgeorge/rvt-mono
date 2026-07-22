-- MyAtm fenced-outbox hardening. Apply after the durable-outbox migration.

IF COL_LENGTH(N'dbo.MyAtmOutboxMessages', N'LeaseId') IS NULL
BEGIN
    ALTER TABLE [dbo].[MyAtmOutboxMessages]
        ADD [LeaseId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_MyAtmAlertOccurrences_RecentLookup'
      AND object_id = OBJECT_ID(N'[dbo].[MyAtmAlertOccurrences]'))
BEGIN
    CREATE INDEX [IX_MyAtmAlertOccurrences_RecentLookup]
        ON [dbo].[MyAtmAlertOccurrences] ([MonitorId], [Field], [Period], [TriggeredAt], [AlertType], [IsSuppressed]);
END;

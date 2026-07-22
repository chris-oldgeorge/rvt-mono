-- Authoritatively synchronize MyAtm payload-version-1 deliveries back to the retained legacy table.
-- Pause MyAtm imports and DispatchOutbox before applying this script. The shared table is left untouched.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY
    IF OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL
        THROW 51010, 'Prerequisite table dbo.MonitorDeliveryOutbox is missing', 1;

    IF OBJECT_ID(N'[dbo].[MyAtmOutboxMessages]', N'U') IS NULL
       OR OBJECT_ID(N'[dbo].[MyAtmAlertOccurrences]', N'U') IS NULL
        THROW 51011, 'Required MyAtm legacy outbox tables are missing', 1;

    IF EXISTS (
        SELECT 1
        FROM [dbo].[MonitorDeliveryOutbox]
        WHERE [Producer] = N'MyAtm'
          AND [PayloadVersion] = 1
          AND [Status] NOT IN (N'Pending', N'InProgress', N'Completed', N'DeadLetter')
    )
        THROW 51012, 'Shared MyAtm version-1 outbox contains an unsupported status', 1;

    IF EXISTS (
        SELECT 1
        FROM [dbo].[MonitorDeliveryOutbox]
        WHERE [Producer] = N'MyAtm'
          AND [PayloadVersion] = 1
          AND [Kind] NOT IN (N'MqttDataInserted', N'MqttAlert', N'Email', N'Sms')
    )
        THROW 51013, 'Shared MyAtm version-1 outbox contains an unsupported kind', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE [object_id] = OBJECT_ID(N'[dbo].[MyAtmOutboxMessages]')
          AND [name] = N'LastError'
          AND [max_length] > 0
          AND [max_length] < 2048
    )
    BEGIN
        ALTER TABLE [dbo].[MyAtmOutboxMessages]
            ALTER COLUMN [LastError] nvarchar(1024) NULL;
    END;

    MERGE [dbo].[MyAtmOutboxMessages] WITH (HOLDLOCK) AS legacy
    USING (
        SELECT
            shared.[Id],
            occurrence.[OccurrenceKey],
            shared.[DeliveryKey],
            shared.[Kind],
            shared.[Destination],
            shared.[Payload],
            CASE shared.[Status]
                WHEN N'InProgress' THEN N'Leased'
                ELSE shared.[Status]
            END AS [Status],
            shared.[AttemptCount],
            shared.[NextAttemptAt],
            shared.[LeaseId],
            shared.[LeaseUntil],
            shared.[CompletedAt],
            shared.[LastError],
            shared.[CreatedAt]
        FROM [dbo].[MonitorDeliveryOutbox] AS shared
        LEFT JOIN [dbo].[MyAtmAlertOccurrences] AS occurrence
          ON occurrence.[OccurrenceKey] = shared.[CorrelationKey]
        WHERE shared.[Producer] = N'MyAtm'
          AND shared.[PayloadVersion] = 1
    ) AS source
      ON legacy.[DeliveryKey] = source.[DeliveryKey]
    WHEN MATCHED
        THEN UPDATE SET
            [OccurrenceKey] = source.[OccurrenceKey],
            [Kind] = source.[Kind],
            [Destination] = source.[Destination],
            [Payload] = source.[Payload],
            [Status] = source.[Status],
            [AttemptCount] = source.[AttemptCount],
            [NextAttemptAt] = source.[NextAttemptAt],
            [LeaseId] = source.[LeaseId],
            [LeaseUntil] = source.[LeaseUntil],
            [CompletedAt] = source.[CompletedAt],
            [LastError] = source.[LastError],
            [CreatedAt] = source.[CreatedAt]
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (
            [Id], [OccurrenceKey], [DeliveryKey], [Kind], [Destination], [Payload], [Status], [AttemptCount],
            [NextAttemptAt], [LeaseId], [LeaseUntil], [CompletedAt], [LastError], [CreatedAt]
        )
        VALUES (
            source.[Id], source.[OccurrenceKey], source.[DeliveryKey], source.[Kind], source.[Destination],
            source.[Payload], source.[Status], source.[AttemptCount], source.[NextAttemptAt], source.[LeaseId],
            source.[LeaseUntil], source.[CompletedAt], source.[LastError], source.[CreatedAt]
        );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

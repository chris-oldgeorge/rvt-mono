-- Backfill the MyAtm version-1 delivery contract into the shared outbox.
-- Pause MyAtm imports and DispatchOutbox before applying this script.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY
    IF OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL
        THROW 51000, 'Prerequisite table dbo.MonitorDeliveryOutbox is missing', 1;

    IF OBJECT_ID(N'[dbo].[MyAtmOutboxMessages]', N'U') IS NULL
       OR OBJECT_ID(N'[dbo].[MyAtmAlertOccurrences]', N'U') IS NULL
       OR OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
        THROW 51001, 'Required MyAtm legacy outbox tables are missing', 1;

    IF EXISTS (
        SELECT 1
        FROM [dbo].[MyAtmOutboxMessages]
        WHERE [Status] NOT IN (N'Pending', N'Leased', N'Completed', N'DeadLetter')
    )
        THROW 51002, 'MyAtm legacy outbox contains an unsupported status', 1;

    IF EXISTS (
        SELECT 1
        FROM [dbo].[MyAtmOutboxMessages]
        WHERE [Kind] NOT IN (N'MqttDataInserted', N'MqttAlert', N'Email', N'Sms')
    )
        THROW 51003, 'MyAtm legacy outbox contains an unsupported kind', 1;

    -- PayloadVersion remains 1 for the existing MyAtm JSON contract.
    MERGE [dbo].[MonitorDeliveryOutbox] WITH (HOLDLOCK) AS shared
    USING (
        SELECT
            legacy.[Id],
            N'MyAtm' AS [Producer],
            notification.[Id] AS [NotificationId],
            legacy.[OccurrenceKey] AS [CorrelationKey],
            legacy.[DeliveryKey],
            legacy.[Kind],
            legacy.[Destination],
            CAST(1 AS int) AS [PayloadVersion],
            legacy.[Payload],
            CASE legacy.[Status]
                WHEN N'Leased' THEN N'InProgress'
                ELSE legacy.[Status]
            END AS [Status],
            legacy.[AttemptCount],
            legacy.[NextAttemptAt],
            legacy.[LeaseId],
            legacy.[LeaseUntil],
            legacy.[CompletedAt],
            CAST(NULL AS datetime2) AS [DeadLetteredAt],
            legacy.[LastError],
            legacy.[CreatedAt]
        FROM [dbo].[MyAtmOutboxMessages] AS legacy
        LEFT JOIN [dbo].[MyAtmAlertOccurrences] AS occurrence
          ON occurrence.[OccurrenceKey] = legacy.[OccurrenceKey]
        LEFT JOIN [dbo].[Notifications] AS notification
          ON occurrence.[NotificationId] = notification.[Id]
    ) AS source
      ON shared.[Producer] = source.[Producer]
     AND shared.[DeliveryKey] = source.[DeliveryKey]
    WHEN MATCHED
         AND (
             shared.[AttemptCount] < source.[AttemptCount]
             OR (
                 shared.[AttemptCount] = source.[AttemptCount]
                 AND NOT (
                     shared.[Status] = N'Completed'
                     AND source.[Status] = N'Completed'
                     AND shared.[CompletedAt] IS NOT NULL
                     AND (source.[CompletedAt] IS NULL OR shared.[CompletedAt] > source.[CompletedAt])
                 )
             )
         )
         AND NOT (
             shared.[Status] IN (N'Completed', N'DeadLetter')
             AND source.[Status] NOT IN (N'Completed', N'DeadLetter')
         )
        THEN UPDATE SET
            [NotificationId] = source.[NotificationId],
            [CorrelationKey] = source.[CorrelationKey],
            [Kind] = source.[Kind],
            [Destination] = source.[Destination],
            [PayloadVersion] = source.[PayloadVersion],
            [Payload] = source.[Payload],
            [Status] = source.[Status],
            [AttemptCount] = source.[AttemptCount],
            [NextAttemptAt] = source.[NextAttemptAt],
            [LeaseId] = source.[LeaseId],
            [LeaseUntil] = source.[LeaseUntil],
            [CompletedAt] = source.[CompletedAt],
            [DeadLetteredAt] = source.[DeadLetteredAt],
            [LastError] = source.[LastError],
            [CreatedAt] = source.[CreatedAt]
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (
            [Id], [Producer], [NotificationId], [CorrelationKey], [DeliveryKey], [Kind], [Destination],
            [PayloadVersion], [Payload], [Status], [AttemptCount], [NextAttemptAt], [LeaseId], [LeaseUntil],
            [CompletedAt], [DeadLetteredAt], [LastError], [CreatedAt]
        )
        VALUES (
            source.[Id], source.[Producer], source.[NotificationId], source.[CorrelationKey], source.[DeliveryKey],
            source.[Kind], source.[Destination], source.[PayloadVersion], source.[Payload], source.[Status],
            source.[AttemptCount], source.[NextAttemptAt], source.[LeaseId], source.[LeaseUntil], source.[CompletedAt],
            NULL, -- DeadLetteredAt is not represented by the legacy schema.
            source.[LastError], source.[CreatedAt]
        );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

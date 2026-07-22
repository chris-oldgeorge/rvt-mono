-- Shared durable delivery outbox for all monitor producers.
-- Apply before deploying code that writes to dbo.MonitorDeliveryOutbox.

IF OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MonitorDeliveryOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [Producer] nvarchar(64) NOT NULL,
        [NotificationId] uniqueidentifier NULL,
        [CorrelationKey] nvarchar(450) NULL,
        [DeliveryKey] nvarchar(450) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Kind] nvarchar(64) NOT NULL,
        [Destination] nvarchar(512) NOT NULL,
        [PayloadVersion] int NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAt] datetime2 NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [DeadLetteredAt] datetime2 NULL,
        [LastError] nvarchar(1024) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MonitorDeliveryOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_MonitorDeliveryOutbox_Producer_Delivery]
            UNIQUE ([Producer], [DeliveryKey]),
        CONSTRAINT [CK_MonitorDeliveryOutbox_Status]
            CHECK ([Status] IN (N'Pending', N'InProgress', N'Completed', N'DeadLetter')),
        CONSTRAINT [FK_MonitorDeliveryOutbox_Notification]
            FOREIGN KEY ([NotificationId])
            REFERENCES [dbo].[Notifications] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MonitorDeliveryOutbox_Due'
      AND [object_id] = OBJECT_ID(N'[dbo].[MonitorDeliveryOutbox]')
)
BEGIN
    CREATE INDEX [IX_MonitorDeliveryOutbox_Due]
        ON [dbo].[MonitorDeliveryOutbox] ([Producer], [Status], [NextAttemptAt]);
END;

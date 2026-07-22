-- MyAtm durable alert occurrence and per-destination delivery outbox.
-- Apply once to the SQL Server monitor database before deploying the durability remediation.

IF OBJECT_ID(N'[dbo].[MyAtmAlertOccurrences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MyAtmAlertOccurrences] (
        [OccurrenceKey] nvarchar(450) NOT NULL PRIMARY KEY,
        [NotificationId] uniqueidentifier NOT NULL UNIQUE,
        [MonitorId] uniqueidentifier NOT NULL,
        [RuleId] uniqueidentifier NOT NULL,
        [Period] int NOT NULL,
        [AlertType] int NOT NULL,
        [Field] nvarchar(256) NOT NULL,
        [Level] float NOT NULL,
        [TriggeredAt] datetime2 NOT NULL,
        [IsSuppressed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [FK_MyAtmAlertOccurrences_Monitor] FOREIGN KEY ([MonitorId]) REFERENCES [dbo].[MonitorsList]([Id]),
        CONSTRAINT [FK_MyAtmAlertOccurrences_RvtAlertRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[RvtAlertRules]([Id])
    );
END;

IF OBJECT_ID(N'[dbo].[MyAtmOutboxMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MyAtmOutboxMessages] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OccurrenceKey] nvarchar(450) NULL,
        [DeliveryKey] nvarchar(450) NOT NULL UNIQUE,
        [Kind] nvarchar(64) NOT NULL,
        [Destination] nvarchar(512) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAt] datetime2 NOT NULL,
        [LeaseUntil] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [LastError] nvarchar(1023) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [FK_MyAtmOutboxMessages_Occurrence] FOREIGN KEY ([OccurrenceKey])
            REFERENCES [dbo].[MyAtmAlertOccurrences]([OccurrenceKey])
    );

    CREATE INDEX [IX_MyAtmOutboxMessages_Status_NextAttemptAt]
        ON [dbo].[MyAtmOutboxMessages] ([Status], [NextAttemptAt]);
END;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AlertOccurrences', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AlertOccurrences
        (
            Id uniqueidentifier NOT NULL,
            Source nvarchar(128) NOT NULL,
            SourceKeyHash binary(32) NOT NULL,
            NotificationId uniqueidentifier NULL,
            MonitorId uniqueidentifier NOT NULL,
            SerialId nvarchar(128) NOT NULL,
            EventTime datetime2 NOT NULL,
            AlertType int NOT NULL,
            AlertField nvarchar(128) NOT NULL,
            Level float NOT NULL,
            LimitOn float NOT NULL,
            AveragingPeriod int NOT NULL,
            Outcome nvarchar(32) NOT NULL,
            CreatedAt datetime2 NOT NULL,
            CONSTRAINT PK_AlertOccurrences PRIMARY KEY (Id),
            CONSTRAINT CK_AlertOccurrences_SourceKeyHash CHECK (DATALENGTH(SourceKeyHash) = 32),
            CONSTRAINT CK_AlertOccurrences_Outcome CHECK
            (
                ([Outcome] COLLATE Latin1_General_100_BIN2 = N'Accepted' AND DATALENGTH([Outcome]) = DATALENGTH(N'Accepted'))
                OR ([Outcome] COLLATE Latin1_General_100_BIN2 = N'Ignored' AND DATALENGTH([Outcome]) = DATALENGTH(N'Ignored'))
                OR ([Outcome] COLLATE Latin1_General_100_BIN2 = N'Suppressed' AND DATALENGTH([Outcome]) = DATALENGTH(N'Suppressed'))
            ),
            CONSTRAINT FK_AlertOccurrences_Notification FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_AlertOccurrences_Monitor FOREIGN KEY (MonitorId) REFERENCES dbo.MonitorsList(Id) ON DELETE NO ACTION,
            CONSTRAINT UQ_AlertOccurrences_SourceKey UNIQUE (Source, SourceKeyHash)
        );
    END;

    IF OBJECT_ID(N'dbo.AlertDeliveryOutbox', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AlertDeliveryOutbox
        (
            Id uniqueidentifier NOT NULL,
            OccurrenceId uniqueidentifier NOT NULL,
            DeliveryKey nvarchar(64) NOT NULL,
            Kind nvarchar(32) NOT NULL,
            Destination nvarchar(512) NOT NULL,
            Payload nvarchar(max) NOT NULL,
            Status nvarchar(32) NOT NULL,
            AttemptCount int NOT NULL,
            NextAttemptAt datetime2 NOT NULL,
            LeaseId uniqueidentifier NULL,
            LeaseUntil datetime2 NULL,
            CompletedAt datetime2 NULL,
            LastError nvarchar(256) NULL,
            CreatedAt datetime2 NOT NULL,
            CONSTRAINT PK_AlertDeliveryOutbox PRIMARY KEY (Id),
            CONSTRAINT FK_AlertDeliveryOutbox_Occurrence FOREIGN KEY (OccurrenceId) REFERENCES dbo.AlertOccurrences(Id) ON DELETE CASCADE,
            CONSTRAINT CK_AlertDeliveryOutbox_PayloadLength CHECK (DATALENGTH(Payload) <= 16384),
            CONSTRAINT CK_AlertDeliveryOutbox_Kind CHECK
            (
                ([Kind] COLLATE Latin1_General_100_BIN2 = N'MqttAlert' AND DATALENGTH([Kind]) = DATALENGTH(N'MqttAlert'))
                OR ([Kind] COLLATE Latin1_General_100_BIN2 = N'Email' AND DATALENGTH([Kind]) = DATALENGTH(N'Email'))
                OR ([Kind] COLLATE Latin1_General_100_BIN2 = N'Sms' AND DATALENGTH([Kind]) = DATALENGTH(N'Sms'))
            ),
            CONSTRAINT CK_AlertDeliveryOutbox_Status CHECK
            (
                ([Status] COLLATE Latin1_General_100_BIN2 = N'Pending' AND DATALENGTH([Status]) = DATALENGTH(N'Pending'))
                OR ([Status] COLLATE Latin1_General_100_BIN2 = N'Leased' AND DATALENGTH([Status]) = DATALENGTH(N'Leased'))
                OR ([Status] COLLATE Latin1_General_100_BIN2 = N'Completed' AND DATALENGTH([Status]) = DATALENGTH(N'Completed'))
                OR ([Status] COLLATE Latin1_General_100_BIN2 = N'DeadLetter' AND DATALENGTH([Status]) = DATALENGTH(N'DeadLetter'))
            ),
            CONSTRAINT UQ_AlertDeliveryOutbox_DeliveryKey UNIQUE (DeliveryKey)
        );
    END;

    IF OBJECT_ID(N'dbo.AlertDeliveryOutbox', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.AlertDeliveryOutbox', N'U')
             AND name = N'IX_AlertDeliveryOutbox_Due'
       )
    BEGIN
        CREATE INDEX IX_AlertDeliveryOutbox_Due
            ON dbo.AlertDeliveryOutbox (Status, NextAttemptAt, LeaseUntil, CreatedAt);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;
    THROW;
END CATCH;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AlertDeliveryOutbox', N'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.AlertDeliveryOutbox;
    END;

    -- WARNING: Dropping AlertOccurrences removes permanent webhook replay protection.
    IF OBJECT_ID(N'dbo.AlertOccurrences', N'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.AlertOccurrences;
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

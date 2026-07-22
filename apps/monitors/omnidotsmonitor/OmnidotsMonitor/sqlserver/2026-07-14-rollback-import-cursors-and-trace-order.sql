SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'PK_OmnidotsTraces'
      AND type = N'PK'
)
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        DROP CONSTRAINT PK_OmnidotsTraces;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'TraceId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        ALTER COLUMN TraceId uniqueidentifier NULL;
END;

-- WARNING: Dropping SampleIndex permanently discards trace sample ordering metadata.
IF COL_LENGTH(N'dbo.OmnidotsTraces', N'SampleIndex') IS NOT NULL
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        DROP COLUMN SampleIndex;
END;

IF OBJECT_ID(N'dbo.OmnidotsTraces', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'ix_traces'
)
BEGIN
    CREATE INDEX ix_traces ON dbo.OmnidotsTraces (TraceId);
END;

IF OBJECT_ID(N'dbo.OmnidotsImportCursor', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.OmnidotsImportCursor;
END;

COMMIT TRANSACTION;

SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OmnidotsImportCursor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OmnidotsImportCursor
    (
        SerialId nvarchar(128) NOT NULL,
        Series nvarchar(16) NOT NULL,
        LastSampleAt datetime2 NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT PK_OmnidotsImportCursor PRIMARY KEY (SerialId, Series),
        -- Binary comparison enforces case; DATALENGTH defeats SQL Server's trailing-space padding semantics.
        CONSTRAINT CK_OmnidotsImportCursor_Series CHECK
        (
            ([Series] COLLATE Latin1_General_100_BIN2 = N'Peak'
                AND DATALENGTH([Series]) = DATALENGTH(N'Peak'))
            OR ([Series] COLLATE Latin1_General_100_BIN2 = N'Veff'
                AND DATALENGTH([Series]) = DATALENGTH(N'Veff'))
            OR ([Series] COLLATE Latin1_General_100_BIN2 = N'Vdv'
                AND DATALENGTH([Series]) = DATALENGTH(N'Vdv'))
        )
    );
END;

IF COL_LENGTH(N'dbo.OmnidotsTraces', N'SampleIndex') IS NULL
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        ADD SampleIndex int NULL;
END;

-- Legacy rows have no logical ordinal. This one-time migration assigns every physical
-- row a temporary unique key, preserving duplicate samples without claiming vendor order.
IF EXISTS
(
    SELECT 1
    FROM dbo.OmnidotsTraces
    WHERE SampleIndex IS NULL
)
BEGIN
    IF COL_LENGTH(N'dbo.OmnidotsTraces', N'MigrationSampleRowId') IS NULL
    BEGIN
        ALTER TABLE dbo.OmnidotsTraces
            ADD MigrationSampleRowId bigint IDENTITY(1,1) NOT NULL;
    END;

    ;WITH indexed_samples AS
    (
        SELECT
            TraceId,
            SampleIndex,
            ROW_NUMBER() OVER
            (
                PARTITION BY TraceId
                ORDER BY MigrationSampleRowId
            ) - 1 AS assigned_sample_index
        FROM dbo.OmnidotsTraces
        WHERE SampleIndex IS NULL
    )
    UPDATE indexed_samples
    SET SampleIndex = assigned_sample_index;

    IF COL_LENGTH(N'dbo.OmnidotsTraces', N'MigrationSampleRowId') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.OmnidotsTraces
            DROP COLUMN MigrationSampleRowId;
    END;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.OmnidotsTraces
    WHERE TraceId IS NULL
)
BEGIN
    THROW 50001, 'Cannot create PK_OmnidotsTraces while a trace sample has a null TraceId.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'TraceId'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        ALTER COLUMN TraceId uniqueidentifier NOT NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'SampleIndex'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        ALTER COLUMN SampleIndex int NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'PK_OmnidotsTraces'
      AND type = N'PK'
)
BEGIN
    ALTER TABLE dbo.OmnidotsTraces
        ADD CONSTRAINT PK_OmnidotsTraces PRIMARY KEY (TraceId, SampleIndex);
END;

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'PK_OmnidotsTraces'
      AND type = N'PK'
)
AND EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OmnidotsTraces', N'U')
      AND name = N'ix_traces'
)
BEGIN
    DROP INDEX ix_traces ON dbo.OmnidotsTraces;
END;

COMMIT TRANSACTION;

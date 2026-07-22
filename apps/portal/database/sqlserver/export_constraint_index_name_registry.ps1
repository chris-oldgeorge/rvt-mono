param(
    [string]$RepoRoot = "C:\Users\oldgeorge\source\repos\chris-oldgeorge\rvtportal-spa-alpha",
    [string]$OutputPath = "C:\Users\oldgeorge\source\repos\chris-oldgeorge\rvtportal-spa-alpha\docs\database\sqlserver-constraint-index-source.csv",
    [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appsettings = Get-Content -Raw -Path (Join-Path $RepoRoot "RvtPortal.Spa\appsettings.Development.json") | ConvertFrom-Json
    $ConnectionString = $appsettings.ConnectionStrings.DefaultConnection
}

$query = @"
WITH key_constraints AS (
    SELECT
        CASE kc.type WHEN 'PK' THEN 'primary_key' WHEN 'UQ' THEN 'unique_constraint' ELSE 'constraint' END AS object_type,
        s.name AS current_schema,
        t.name AS current_relation,
        kc.name AS current_object,
        STUFF((
            SELECT '|' + c.name
            FROM sys.index_columns ic
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = kc.parent_object_id
              AND ic.index_id = kc.unique_index_id
              AND ic.key_ordinal > 0
            ORDER BY ic.key_ordinal
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS current_columns,
        CAST('' AS nvarchar(max)) AS referenced_relation,
        CAST('' AS nvarchar(max)) AS referenced_columns
    FROM sys.key_constraints kc
    JOIN sys.tables t ON t.object_id = kc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo'
),
foreign_keys AS (
    SELECT
        'foreign_key' AS object_type,
        s.name AS current_schema,
        t.name AS current_relation,
        fk.name AS current_object,
        STUFF((
            SELECT '|' + pc.name
            FROM sys.foreign_key_columns fkc
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            WHERE fkc.constraint_object_id = fk.object_id
            ORDER BY fkc.constraint_column_id
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS current_columns,
        rt.name AS referenced_relation,
        STUFF((
            SELECT '|' + rc.name
            FROM sys.foreign_key_columns fkc
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fkc.constraint_object_id = fk.object_id
            ORDER BY fkc.constraint_column_id
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS referenced_columns
    FROM sys.foreign_keys fk
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
    WHERE s.name = 'dbo'
),
check_constraints AS (
    SELECT
        'check_constraint' AS object_type,
        s.name AS current_schema,
        t.name AS current_relation,
        cc.name AS current_object,
        CAST('' AS nvarchar(max)) AS current_columns,
        CAST('' AS nvarchar(max)) AS referenced_relation,
        CAST('' AS nvarchar(max)) AS referenced_columns
    FROM sys.check_constraints cc
    JOIN sys.tables t ON t.object_id = cc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo'
),
indexes AS (
    SELECT
        'index' AS object_type,
        s.name AS current_schema,
        t.name AS current_relation,
        i.name AS current_object,
        STUFF((
            SELECT '|' + c.name
            FROM sys.index_columns ic
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
            ORDER BY ic.key_ordinal
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS current_columns,
        CAST('' AS nvarchar(max)) AS referenced_relation,
        CAST('' AS nvarchar(max)) AS referenced_columns
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo'
      AND i.name IS NOT NULL
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND i.type_desc <> 'HEAP'
)
SELECT 'sqlserver' AS provider, object_type, current_schema, current_relation, current_object, current_columns, referenced_relation, referenced_columns
FROM key_constraints
UNION ALL
SELECT 'sqlserver', object_type, current_schema, current_relation, current_object, current_columns, referenced_relation, referenced_columns
FROM foreign_keys
UNION ALL
SELECT 'sqlserver', object_type, current_schema, current_relation, current_object, current_columns, referenced_relation, referenced_columns
FROM check_constraints
UNION ALL
SELECT 'sqlserver', object_type, current_schema, current_relation, current_object, current_columns, referenced_relation, referenced_columns
FROM indexes
ORDER BY current_relation, object_type, current_object;
"@

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$command = $connection.CreateCommand()
$command.CommandText = $query
$connection.Open()
try {
    $reader = $command.ExecuteReader()
    $rows = [System.Collections.Generic.List[object]]::new()
    while ($reader.Read()) {
        $rows.Add([pscustomobject]@{
            provider = [string]$reader["provider"]
            object_type = [string]$reader["object_type"]
            current_schema = [string]$reader["current_schema"]
            current_relation = [string]$reader["current_relation"]
            current_object = [string]$reader["current_object"]
            current_columns = if ($reader["current_columns"] -is [DBNull]) { "" } else { [string]$reader["current_columns"] }
            referenced_relation = if ($reader["referenced_relation"] -is [DBNull]) { "" } else { [string]$reader["referenced_relation"] }
            referenced_columns = if ($reader["referenced_columns"] -is [DBNull]) { "" } else { [string]$reader["referenced_columns"] }
        })
    }
} finally {
    $connection.Close()
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$rows | Export-Csv -NoTypeInformation -Path $OutputPath
Write-Output "Exported $($rows.Count) SQL Server constraint/index rows."

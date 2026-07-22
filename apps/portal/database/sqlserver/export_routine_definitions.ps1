param(
    [string]$RepoRoot = $((Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path),
    [string]$OutputPath = "",
    [string]$ConnectionString = ""
)

# File summary: Exports SQL Server stored routine definitions for the DBR PostgreSQL porting gate.
# Major updates:
# - 2026-06-09 pending Added routine export coverage so PostgreSQL function/procedure porting is based on source definitions.

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot "docs\database\sqlserver-routine-definitions-source.csv"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appsettings = Get-Content -Raw -Path (Join-Path $RepoRoot "RvtPortal.Spa\appsettings.Development.json") | ConvertFrom-Json
    $ConnectionString = $appsettings.ConnectionStrings.DefaultConnection
}

# Function summary: Converts legacy SQL Server identifiers into lowercase snake_case candidates.
function Convert-ToSnakeCase {
    param([string]$Identifier)
    if ([string]::IsNullOrWhiteSpace($Identifier)) {
        return ""
    }

    $value = [regex]::Replace($Identifier, '([A-Z]+)([A-Z][a-z])', '$1_$2')
    $value = [regex]::Replace($value, '([a-z0-9])([A-Z])', '$1_$2')
    $value = [regex]::Replace($value, '([A-Za-z])([0-9])', '$1_$2')
    $value = [regex]::Replace($value, '([0-9])([A-Za-z])', '$1_$2')
    $value = [regex]::Replace($value, '[^A-Za-z0-9]+', '_')
    $value = $value.Trim('_').ToLowerInvariant()
    return [regex]::Replace($value, '_+', '_')
}

# Function summary: Applies routine-specific singular and prefix cleanup to canonical routine names.
function Get-CanonicalRoutineName {
    param([string]$Identifier)

    $value = Convert-ToSnakeCase $Identifier
    if ($value.StartsWith("fn_")) {
        $value = $value.Substring(3)
    }

    $value = $value.Replace("offline_date_time", "offline_time")
    return $value
}

# Function summary: Builds semicolon-delimited review notes for each exported routine definition.
function Get-RoutineNotes {
    param(
        [string]$RoutineName,
        [string]$Definition
    )

    $notes = [System.Collections.Generic.List[string]]::new()
    if ($RoutineName -cne $RoutineName.ToLowerInvariant()) {
        $notes.Add("routine_not_lowercase")
    }
    if ($Definition -match 'AspNet') {
        $notes.Add("references_identity_excluded")
    }
    if ($Definition -match '\b(GETDATE|GETUTCDATE|DATEADD|DATEDIFF|ISNULL|TRY_CONVERT|CONVERT|TOP)\b') {
        $notes.Add("sqlserver_dialect_review")
    }
    if ($Definition -match '\[[^\]]+\]') {
        $notes.Add("bracketed_identifier_review")
    }
    if ($Definition -match '\bdbo\.') {
        $notes.Add("schema_reference_review")
    }

    if ($notes.Count -eq 0) {
        $notes.Add("review_for_postgres_port")
    }

    return ($notes -join ";")
}

$query = @"
SELECT
    s.name AS schema_name,
    o.name AS routine_name,
    CASE o.type
        WHEN 'P' THEN 'procedure'
        WHEN 'FN' THEN 'scalar_function'
        WHEN 'IF' THEN 'inline_table_function'
        WHEN 'TF' THEN 'table_function'
        WHEN 'FS' THEN 'clr_scalar_function'
        WHEN 'FT' THEN 'clr_table_function'
        ELSE LOWER(o.type_desc)
    END AS routine_type,
    m.definition,
    m.uses_ansi_nulls,
    m.uses_quoted_identifier,
    USER_NAME(m.execute_as_principal_id) AS execute_as_user
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type IN ('P', 'FN', 'IF', 'TF', 'FS', 'FT')
  AND o.is_ms_shipped = 0
  AND s.name = 'dbo'
ORDER BY s.name, o.name;
"@

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$command = $connection.CreateCommand()
$command.CommandText = $query
$connection.Open()
try {
    $reader = $command.ExecuteReader()
    $rows = [System.Collections.Generic.List[object]]::new()
    while ($reader.Read()) {
        $routineName = [string]$reader["routine_name"]
        $definition = if ($reader["definition"] -is [DBNull]) { "" } else { [string]$reader["definition"] }
        $canonicalName = Get-CanonicalRoutineName $routineName
        $changeType = if ($routineName -ceq $canonicalName) { "compliant" } else { "rename_routine" }

        $rows.Add([pscustomobject]@{
            provider = "sqlserver"
            routine_type = [string]$reader["routine_type"]
            current_schema = [string]$reader["schema_name"]
            current_routine = $routineName
            new_schema = "public"
            new_routine = $canonicalName
            change_type = $changeType
            uses_ansi_nulls = [bool]$reader["uses_ansi_nulls"]
            uses_quoted_identifier = [bool]$reader["uses_quoted_identifier"]
            execute_as_user = if ($reader["execute_as_user"] -is [DBNull]) { "" } else { [string]$reader["execute_as_user"] }
            notes = Get-RoutineNotes $routineName $definition
            definition = $definition
        })
    }
} finally {
    $connection.Close()
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$rows | Export-Csv -NoTypeInformation -Path $OutputPath
Write-Output "Exported $($rows.Count) SQL Server routine definition rows."

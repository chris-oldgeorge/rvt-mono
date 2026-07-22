param(
    [string]$RepoRoot = $((Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path),
    [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appsettings = Get-Content -Raw -Path (Join-Path $RepoRoot "RvtPortal.Spa\appsettings.Development.json") | ConvertFrom-Json
    $ConnectionString = $appsettings.ConnectionStrings.DefaultConnection
}

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
    return [regex]::Replace($value, '_+', '_').Replace('sitei_d', 'site_id')
}

function Get-CanonicalRelation {
    param([string]$Identifier)
    $value = Convert-ToSnakeCase $Identifier
    $replacements = [ordered]@{
        'monitors_list' = 'monitor'
        'reports_' = 'report_'
        'notifications_' = 'notification_'
        '_error_messages' = '_error_message'
        '_noise_levels' = '_noise_level'
        '_peak_levels' = '_peak_level'
        '_vdv_levels' = '_vdv_level'
        '_veff_levels' = '_veff_level'
        '_dust_levels' = '_dust_level'
        '_traces_' = '_trace_'
        '_users' = '_user'
        '_roles' = '_role'
        '_claims' = '_claim'
        '_logins' = '_login'
        '_tokens' = '_token'
        '_sections' = '_section'
        '_articles' = '_article'
        '_assets' = '_asset'
        '_contracts' = '_contract'
        '_deployments' = '_deployment'
        '_companies' = '_company'
        '_sites' = '_site'
        '_averages' = '_average'
        '_hours' = '_hour'
        '_settings' = '_setting'
        '_rules' = '_rule'
        '_sensors' = '_sensor'
        '_actions_' = '_action_'
    }

    foreach ($key in $replacements.Keys) {
        $value = $value.Replace($key, $replacements[$key])
    }

    if ($value.EndsWith('ies')) {
        $value = [regex]::Replace($value, 'ies$', 'y')
    } elseif ($value.EndsWith('s') -and -not $value.EndsWith('status') -and -not $value.EndsWith('class')) {
        $value = [regex]::Replace($value, 's$', '')
    }

    return $value
}

function Get-CanonicalColumn {
    param(
        [string]$Identifier,
        [bool]$IsSinglePrimaryKey
    )

    if ($IsSinglePrimaryKey) {
        return "id"
    }

    $value = Convert-ToSnakeCase $Identifier
    $replacements = [ordered]@{
        'timestamtp' = 'logged_at'
        'nr_users' = 'user_count'
        'nr' = 'row_count'
        'l_aeq' = 'laeq'
        'l_amax' = 'lamax'
        'l_a_90' = 'la90'
        'l_a_10' = 'la10'
        'l_ceq' = 'lceq'
        'l_cmax' = 'lcmax'
        'l_c_90' = 'lc90'
        'l_c_10' = 'lc10'
        'p_m_10' = 'pm10'
        'p_m_2_5' = 'pm2_5'
        't_mio' = 'tmio'
        'p_mio' = 'pmio'
        'operating_volume_flow_timestamp' = 'operating_volume_flow_time'
    }

    foreach ($key in $replacements.Keys) {
        $value = $value.Replace($key, $replacements[$key])
    }

    if ($value -eq 'timestamp') {
        return 'recorded_at'
    }
    if ($value -eq 'text') {
        return 'content'
    }
    if ($value -eq 'date') {
        return 'event_date'
    }

    return $value
}

$query = @"
WITH relations AS (
    SELECT s.name AS current_schema, o.name AS current_relation, o.object_id,
        CASE WHEN o.type = 'U' THEN 'table' WHEN o.type = 'V' THEN 'view' ELSE LOWER(o.type_desc) END AS object_type
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    WHERE o.type IN ('U', 'V')
      AND o.is_ms_shipped = 0
      AND s.name = 'dbo'
),
primary_key_columns AS (
    SELECT kc.parent_object_id AS object_id, c.name AS column_name,
        CASE WHEN COUNT(*) OVER (PARTITION BY kc.parent_object_id, kc.unique_index_id) = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_single_pk
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.type = 'PK'
),
foreign_key_columns AS (
    SELECT fkc.parent_object_id AS object_id, pc.name AS column_name,
        rt.name AS referenced_relation, rc.name AS referenced_column
    FROM sys.foreign_key_columns fkc
    JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
    JOIN sys.objects rt ON rt.object_id = fkc.referenced_object_id
    JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
)
SELECT r.object_type, r.current_schema, r.current_relation, CAST(NULL AS nvarchar(256)) AS current_column,
    CAST(NULL AS nvarchar(256)) AS current_data_type, CAST(NULL AS bit) AS is_single_pk,
    CAST(NULL AS nvarchar(256)) AS referenced_relation, CAST(NULL AS nvarchar(256)) AS referenced_column
FROM relations r
UNION ALL
SELECT r.object_type + '_column', r.current_schema, r.current_relation, c.name,
    t.name, ISNULL(pk.is_single_pk, CAST(0 AS bit)), fk.referenced_relation, fk.referenced_column
FROM relations r
JOIN sys.columns c ON c.object_id = r.object_id
JOIN sys.types t ON t.user_type_id = c.user_type_id
LEFT JOIN primary_key_columns pk ON pk.object_id = r.object_id AND pk.column_name = c.name
LEFT JOIN foreign_key_columns fk ON fk.object_id = r.object_id AND fk.column_name = c.name
ORDER BY current_relation, object_type, current_column;
"@

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$command = $connection.CreateCommand()
$command.CommandText = $query
$connection.Open()
try {
    $reader = $command.ExecuteReader()
    $rows = [System.Collections.Generic.List[object]]::new()
    while ($reader.Read()) {
        $currentRelation = [string]$reader["current_relation"]
        $currentColumn = if ($reader["current_column"] -is [DBNull]) { "" } else { [string]$reader["current_column"] }
        $currentType = if ($reader["current_data_type"] -is [DBNull]) { "" } else { [string]$reader["current_data_type"] }
        $referencedRelation = if ($reader["referenced_relation"] -is [DBNull]) { "" } else { [string]$reader["referenced_relation"] }
        $referencedColumn = if ($reader["referenced_column"] -is [DBNull]) { "" } else { [string]$reader["referenced_column"] }
        $isSinglePrimaryKey = -not ($reader["is_single_pk"] -is [DBNull]) -and [bool]$reader["is_single_pk"]
        $newRelation = Get-CanonicalRelation $currentRelation
        $newColumn = ""
        if ($currentColumn) {
            if ($referencedRelation) {
                $newColumn = "$(Get-CanonicalRelation $referencedRelation)_$(Get-CanonicalColumn $referencedColumn $true)"
            } else {
                $newColumn = Get-CanonicalColumn $currentColumn $isSinglePrimaryKey
            }
        }

        $notes = [System.Collections.Generic.List[string]]::new()
        if ($currentRelation -cne $currentRelation.ToLowerInvariant()) { $notes.Add("relation_not_lowercase") }
        if ($currentColumn -and $currentColumn -cne $currentColumn.ToLowerInvariant()) { $notes.Add("column_not_lowercase") }
        if ($isSinglePrimaryKey) { $notes.Add("single_column_pk") }
        if ($referencedRelation) { $notes.Add("fk_to_$(Get-CanonicalRelation $referencedRelation)") }
        if ($currentColumn -eq "Timestamtp") { $notes.Add("legacy_misspelling") }
        if ($newColumn -eq "operating_volume_flow_time") { $notes.Add("data_type_name_removed") }
        if ($newColumn -in @("recorded_at", "content", "event_date")) { $notes.Add("data_type_name_renamed") }

        $changeType = if ($currentColumn) {
            if ($currentColumn -ceq $newColumn) { "compliant" } else { "rename_column" }
        } else {
            if ($currentRelation -ceq $newRelation) { "compliant" } else { "rename_relation" }
        }

        $rows.Add([pscustomobject]@{
            provider = "sqlserver"
            object_type = [string]$reader["object_type"]
            current_schema = [string]$reader["current_schema"]
            current_relation = $currentRelation
            current_column = $currentColumn
            current_data_type = $currentType
            new_schema = "dbo"
            new_relation = $newRelation
            new_column = $newColumn
            new_data_type = $currentType
            change_type = $changeType
            notes = ($notes -join ";")
        })
    }
} finally {
    $connection.Close()
}

$docs = Join-Path $RepoRoot "docs\database"
New-Item -ItemType Directory -Force -Path $docs | Out-Null
$rows | Export-Csv -NoTypeInformation -Path (Join-Path $docs "sqlserver-name-registry.csv")
$rows | Where-Object { $_.change_type -ne "compliant" } |
    Select-Object current_relation, current_column, new_relation, new_column, change_type, notes |
    Export-Csv -NoTypeInformation -Path (Join-Path $docs "database-name-equivalents-for-migrator-sqlserver.csv")

Write-Output "Exported $($rows.Count) SQL Server registry rows."

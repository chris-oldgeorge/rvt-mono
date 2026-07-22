<#
.SYNOPSIS
Sets RVT Portal SPA development secrets with the .NET user-secrets store.

.DESCRIPTION
Configures the local Development values required by RvtPortal.Spa without
writing credentials to appsettings files or any tracked source file.

By default the script sets the core values needed to boot the API locally:
database provider, database connection string, seed master-admin password,
local email-safe defaults, and storage container names. Optional integrations
(Omnidots, What3Words, report generation, blob storage, outbound email, SMTP,
Redis, and data protection) can be added with the Configure* switches or all at
once with -ConfigureAll.

See docs/operations/portal/dev-secrets-reference.md for what every key does.

.EXAMPLE
.\docs\deploy\set-dev-secrets.ps1

.EXAMPLE
.\docs\deploy\set-dev-secrets.ps1 -DatabaseProvider Postgres -ConfigureOmnidots -ConfigureWhat3Words

.EXAMPLE
.\docs\deploy\set-dev-secrets.ps1 -ConfigureReporting

.EXAMPLE
.\docs\deploy\set-dev-secrets.ps1 -ConfigureAll
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ProjectPath,

    [ValidateSet("SqlServer", "Postgres")]
    [string]$DatabaseProvider,

    [string]$ConnectionString,
    [string]$SeedMasterAdminPassword,
    [switch]$SkipSeedMasterAdmin,
    [string]$PostgresRoutineSchema = "public",

    [string]$DebugEmailAddress = "devnull@rvt.local",

    [switch]$ConfigureAll,
    [switch]$ConfigureOmnidots,
    [string]$OmnidotsAdapterUrl,
    [string]$OmnidotsAdapterSecret,

    [switch]$ConfigureWhat3Words,
    [string]$What3WordsApiKey,

    [switch]$ConfigureReporting,
    [string]$ReportGenerationBaseUrl,
    [string]$ReportGenerationInternalApiKey,
    [string]$ReportContentInternalApiKey,

    [switch]$ConfigureBlobStorage,
    [ValidateSet("ConnectionString", "ServiceUri")]
    [string]$BlobStorageMode = "ConnectionString",
    [string]$BlobConnectionString,
    [string]$BlobServiceUri,

    [switch]$ConfigureOutboundEmail,
    [string]$SendGridApiKey,

    [switch]$ConfigureSmtp,
    [string]$SmtpHost,
    [int]$SmtpPort = 587,
    [bool]$SmtpSsl = $true,
    [string]$SmtpUsername,
    [string]$SmtpPassword,

    [switch]$ConfigureRedis,
    [string]$RedisConnectionString,
    [string]$RedisInstanceName = "RvtPortal:",

    [switch]$ConfigureDataProtection,
    [string]$DataProtectionApplicationName = "RvtMonitoring",
    [string]$DataProtectionBlobUri,
    [string]$DataProtectionKeyIdentifier,

    [switch]$NonInteractive
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $scriptRoot = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
        $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..\..")).Path
}

function ConvertFrom-SecureStringToPlainText {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.SecureString]$SecureValue
    )

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        if ($pointer -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
    }
}

function Read-PlainSetting {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,

        [string]$CurrentValue,
        [string]$DefaultValue,
        [bool]$Required = $true
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue
    }

    if ($NonInteractive) {
        if ($Required -and [string]::IsNullOrWhiteSpace($DefaultValue)) {
            throw "Missing required parameter for '$Prompt'."
        }

        return $DefaultValue
    }

    while ($true) {
        $effectivePrompt = $Prompt
        if (-not [string]::IsNullOrWhiteSpace($DefaultValue)) {
            $effectivePrompt = "$Prompt [$DefaultValue]"
        }

        $value = Read-Host -Prompt $effectivePrompt
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = $DefaultValue
        }

        if (-not $Required -or -not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }

        Write-Warning "A value is required."
    }
}

function Read-SecretSetting {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,

        [string]$CurrentValue,
        [bool]$Required = $true
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue
    }

    if ($NonInteractive) {
        if ($Required) {
            throw "Missing required secret for '$Prompt'."
        }

        return ""
    }

    while ($true) {
        $secureValue = Read-Host -Prompt $Prompt -AsSecureString
        $value = ConvertFrom-SecureStringToPlainText -SecureValue $secureValue

        if (-not $Required -or -not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }

        Write-Warning "A value is required."
    }
}

function Test-IdentityPasswordShape {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    return $Password.Length -ge 6 `
        -and $Password -match "[A-Z]" `
        -and $Password -match "[a-z]" `
        -and $Password -match "\d" `
        -and $Password -match "[^a-zA-Z0-9]"
}

function Read-SeedPassword {
    if ($SkipSeedMasterAdmin) {
        return ""
    }

    while ($true) {
        $password = Read-SecretSetting `
            -Prompt "Seed master-admin password for RVT_PORTAL_SEED_MASTER_ADMIN" `
            -CurrentValue $SeedMasterAdminPassword `
            -Required $true

        if (Test-IdentityPasswordShape -Password $password) {
            return $password
        }

        if ($NonInteractive -or -not [string]::IsNullOrWhiteSpace($SeedMasterAdminPassword)) {
            throw "Seed master-admin password must be at least 6 characters and include uppercase, lowercase, digit, and symbol characters."
        }

        Write-Warning "The seed password must be at least 6 characters and include uppercase, lowercase, digit, and symbol characters."
    }
}

function Invoke-DotNetUserSecrets {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $script:DotNetPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet user-secrets command failed: $($Arguments -join ' ')"
    }
}

function Ensure-UserSecretsInitialized {
    [xml]$projectXml = Get-Content -LiteralPath $script:ProjectPath -Raw
    $hasUserSecretsId = $false
    foreach ($propertyGroup in $projectXml.Project.PropertyGroup) {
        if ($propertyGroup.UserSecretsId -and -not [string]::IsNullOrWhiteSpace($propertyGroup.UserSecretsId)) {
            $hasUserSecretsId = $true
            break
        }
    }

    if ($hasUserSecretsId) {
        return
    }

    if ($PSCmdlet.ShouldProcess($script:ProjectPath, "initialize .NET user secrets")) {
        Invoke-DotNetUserSecrets -Arguments @("user-secrets", "init", "--project", $script:ProjectPath)
    }
}

function Set-DevSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowEmptyString()]
        [string]$Value,

        [switch]$Sensitive
    )

    if ($null -eq $Value) {
        return
    }

    if ($Sensitive -and [string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $displayValue = $Value
    if ($Sensitive) {
        $displayValue = "<redacted>"
    }

    if ($PSCmdlet.ShouldProcess("RvtPortal.Spa user secrets", "set $Name = $displayValue")) {
        Invoke-DotNetUserSecrets -Arguments @("user-secrets", "set", $Name, $Value, "--project", $script:ProjectPath)
        Write-Output "Set $Name"
    }
}

if ($ConfigureAll) {
    $ConfigureOmnidots = $true
    $ConfigureWhat3Words = $true
    $ConfigureReporting = $true
    $ConfigureBlobStorage = $true
    $ConfigureOutboundEmail = $true
    $ConfigureSmtp = $true
    $ConfigureRedis = $true
    $ConfigureDataProtection = $true
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot "RvtPortal.Spa\RvtPortal.Spa.csproj"
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$script:ProjectPath = $ProjectPath

$dotnet = Get-Command dotnet -ErrorAction Stop
$script:DotNetPath = $dotnet.Source

Ensure-UserSecretsInitialized

$DatabaseProvider = Read-PlainSetting `
    -Prompt "Database provider (SqlServer or Postgres)" `
    -CurrentValue $DatabaseProvider `
    -DefaultValue "SqlServer" `
    -Required $true

$ConnectionString = Read-SecretSetting `
    -Prompt "Database connection string for ConnectionStrings:DefaultConnection" `
    -CurrentValue $ConnectionString `
    -Required $true

$SeedMasterAdminPassword = Read-SeedPassword

$skipPasswordResetEmail = "true"
if ($ConfigureOutboundEmail) {
    $skipPasswordResetEmail = "false"
}

Set-DevSecret -Name "Database:Provider" -Value $DatabaseProvider
Set-DevSecret -Name "Database:ConnectionStringName" -Value "DefaultConnection"
Set-DevSecret -Name "Database:ConnectionString" -Value $ConnectionString -Sensitive
Set-DevSecret -Name "Database:PostgresRoutineSchema" -Value $PostgresRoutineSchema
Set-DevSecret -Name "ConnectionStrings:DefaultConnection" -Value $ConnectionString -Sensitive
Set-DevSecret -Name "Auth:SkipPasswordResetEmail" -Value $skipPasswordResetEmail

if (-not $SkipSeedMasterAdmin) {
    Set-DevSecret -Name "RVT_PORTAL_SEED_MASTER_ADMIN" -Value $SeedMasterAdminPassword -Sensitive
}

Set-DevSecret -Name "EmailConfiguration:UseDebugEmail" -Value "true"
Set-DevSecret -Name "EmailConfiguration:DebugEmailAddress" -Value $DebugEmailAddress
Set-DevSecret -Name "EmailConfiguration:CopyEmailAddress" -Value $DebugEmailAddress
Set-DevSecret -Name "EmailConfiguration:Sending_Email_Address" -Value "NoReply@rvtgroup.co.uk"

Set-DevSecret -Name "BlobStorage:MonitorImagesContainer" -Value "monitor-pictures"
Set-DevSecret -Name "BlobStorage:ArchiveContainer" -Value "archive"
Set-DevSecret -Name "BlobStorage:ReportContainer" -Value "pdfreports"
Set-DevSecret -Name "BlobStorage:ReportFolder" -Value "rvtreports"
Set-DevSecret -Name "BlobStorage:AudioFolder" -Value "audiofiles"

if ($ConfigureOmnidots) {
    $OmnidotsAdapterUrl = Read-PlainSetting `
        -Prompt "Omnidots adapter URL" `
        -CurrentValue $OmnidotsAdapterUrl `
        -Required $true

    $OmnidotsAdapterSecret = Read-SecretSetting `
        -Prompt "Omnidots adapter secret" `
        -CurrentValue $OmnidotsAdapterSecret `
        -Required $true

    Set-DevSecret -Name "ExternalUrls:OmnidotsAdapterUrl" -Value $OmnidotsAdapterUrl
    Set-DevSecret -Name "ExternalUrls:OmnidotsAdapterSecret" -Value $OmnidotsAdapterSecret -Sensitive
}

if ($ConfigureWhat3Words) {
    $What3WordsApiKey = Read-SecretSetting `
        -Prompt "What3Words API key" `
        -CurrentValue $What3WordsApiKey `
        -Required $true

    Set-DevSecret -Name "What3Words:ApiKey" -Value $What3WordsApiKey -Sensitive
}

if ($ConfigureReporting) {
    $ReportGenerationBaseUrl = Read-PlainSetting `
        -Prompt "Report generation service base URL" `
        -CurrentValue $ReportGenerationBaseUrl `
        -Required $true

    $ReportGenerationInternalApiKey = Read-SecretSetting `
        -Prompt "Report generation internal API key (portal -> report service)" `
        -CurrentValue $ReportGenerationInternalApiKey `
        -Required $true

    $ReportContentInternalApiKey = Read-SecretSetting `
        -Prompt "Report content internal API key (report service -> portal callback)" `
        -CurrentValue $ReportContentInternalApiKey `
        -Required $true

    Set-DevSecret -Name "ReportGenerationService:BaseUrl" -Value $ReportGenerationBaseUrl
    Set-DevSecret -Name "ReportGenerationService:InternalApiKey" -Value $ReportGenerationInternalApiKey -Sensitive
    Set-DevSecret -Name "ReportContent:InternalApiKey" -Value $ReportContentInternalApiKey -Sensitive
}

if ($ConfigureBlobStorage) {
    if ($BlobStorageMode -eq "ConnectionString") {
        $BlobConnectionString = Read-SecretSetting `
            -Prompt "Azure Blob Storage connection string" `
            -CurrentValue $BlobConnectionString `
            -Required $true

        Set-DevSecret -Name "BlobStorage:blobConnectionString" -Value $BlobConnectionString -Sensitive
    }
    else {
        $BlobServiceUri = Read-PlainSetting `
            -Prompt "Azure Blob service URI" `
            -CurrentValue $BlobServiceUri `
            -Required $true

        Set-DevSecret -Name "BlobStorage:blobServiceUri" -Value $BlobServiceUri
    }
}

if ($ConfigureOutboundEmail) {
    $SendGridApiKey = Read-SecretSetting `
        -Prompt "SendGrid API key" `
        -CurrentValue $SendGridApiKey `
        -Required $true

    Set-DevSecret -Name "EmailConfiguration:SENDGRID_API_KEY" -Value $SendGridApiKey -Sensitive
}

if ($ConfigureSmtp) {
    $SmtpHost = Read-PlainSetting `
        -Prompt "SMTP host" `
        -CurrentValue $SmtpHost `
        -Required $true

    $SmtpUsername = Read-PlainSetting `
        -Prompt "SMTP username" `
        -CurrentValue $SmtpUsername `
        -Required $false

    $SmtpPassword = Read-SecretSetting `
        -Prompt "SMTP password" `
        -CurrentValue $SmtpPassword `
        -Required $false

    Set-DevSecret -Name "SmtpServer:Host" -Value $SmtpHost
    Set-DevSecret -Name "SmtpServer:Port" -Value ([string]$SmtpPort)
    Set-DevSecret -Name "SmtpServer:Ssl" -Value ([string]$SmtpSsl).ToLowerInvariant()
    Set-DevSecret -Name "SmtpServer:Username" -Value $SmtpUsername -Sensitive
    Set-DevSecret -Name "SmtpServer:Password" -Value $SmtpPassword -Sensitive
}

if ($ConfigureRedis) {
    $RedisConnectionString = Read-SecretSetting `
        -Prompt "Redis connection string" `
        -CurrentValue $RedisConnectionString `
        -Required $true

    Set-DevSecret -Name "RvtProduction:RedisConnectionString" -Value $RedisConnectionString -Sensitive
    Set-DevSecret -Name "RvtProduction:RedisInstanceName" -Value $RedisInstanceName
}

if ($ConfigureDataProtection) {
    $DataProtectionBlobUri = Read-PlainSetting `
        -Prompt "Data-protection Azure Blob URI" `
        -CurrentValue $DataProtectionBlobUri `
        -Required $true

    $DataProtectionKeyIdentifier = Read-PlainSetting `
        -Prompt "Data-protection Key Vault key identifier" `
        -CurrentValue $DataProtectionKeyIdentifier `
        -Required $false

    Set-DevSecret -Name "RvtProduction:DataProtectionApplicationName" -Value $DataProtectionApplicationName
    Set-DevSecret -Name "RvtProduction:DataProtectionBlobUri" -Value $DataProtectionBlobUri
    Set-DevSecret -Name "RvtProduction:DataProtectionKeyIdentifier" -Value $DataProtectionKeyIdentifier
}

Write-Host ""
if ($WhatIfPreference) {
    Write-Host "Dry run completed for:"
}
else {
    Write-Host "Development secrets are configured for:"
}
Write-Host "  $ProjectPath"
Write-Host ""
Write-Host "To inspect keys without exposing values, run:"
Write-Host "  dotnet user-secrets list --project `"$ProjectPath`" | ForEach-Object { (`$_ -split ' = ', 2)[0] }"

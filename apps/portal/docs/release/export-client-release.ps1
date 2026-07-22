<#
.SYNOPSIS
Exports a sanitized client milestone folder from the current Git worktree.

.DESCRIPTION
The exporter copies Git-tracked files into a clean milestone folder, applies the
client release exclusion list, verifies blocked internal artifacts are absent,
and confirms frontend source asset folders are preserved when present. It does
not push to Git or modify the client repository.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$Milestone,

    [string]$OutputRoot,

    [switch]$DryRun,

    [switch]$CreateZip,

    [switch]$AllowDirty,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function ConvertTo-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return ($Path -replace '\\', '/').TrimStart('./')
}

function ConvertTo-RegexPattern {
    param([Parameter(Mandatory = $true)][string]$Pattern)

    $normalized = ConvertTo-RepoPath $Pattern
    $escaped = [regex]::Escape($normalized)
    $escaped = $escaped -replace '\\\*\\\*/', '(.*/)?'
    $escaped = $escaped -replace '\\\*\\\*', '.*'
    $escaped = $escaped -replace '\\\*', '[^/]*'
    return '^' + $escaped + '$'
}

function Test-DirectoryPatternExcluded {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $directory = $Pattern.TrimEnd('/')
    if ($directory.StartsWith('**/')) {
        $segment = $directory.Substring(3)
        return $Path -eq $segment -or
            $Path.StartsWith($segment + '/') -or
            $Path.Contains('/' + $segment + '/')
    }

    return $Path -eq $directory -or $Path.StartsWith($directory + '/')
}

function Test-FilePatternExcluded {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    if ($Pattern.Contains('*')) {
        return $Path -match (ConvertTo-RegexPattern $Pattern)
    }

    return $Path -eq $Pattern
}

function Test-ReleasePathExcluded {
    param(
        [Parameter(Mandatory = $true)][string]$RepoPath,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    $path = ConvertTo-RepoPath $RepoPath

    foreach ($rawPattern in $Patterns) {
        $pattern = (ConvertTo-RepoPath $rawPattern).Trim()
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        if ($pattern.EndsWith('/')) {
            if (Test-DirectoryPatternExcluded -Path $path -Pattern $pattern) {
                return $true
            }

            continue
        }

        if (Test-FilePatternExcluded -Path $path -Pattern $pattern) {
            return $true
        }
    }

    return $false
}

function Get-ReleaseExclusionPatterns {
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        throw "Release exclusion config was not found: $ConfigPath"
    }

    return Get-Content -LiteralPath $ConfigPath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') }
}

function Assert-GitAvailable {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    & git -c "safe.directory=$RepoRoot" -C $RepoRoot rev-parse --is-inside-work-tree | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The release exporter must run inside a Git worktree."
    }
}

function Get-TrackedFiles {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $files = & git -c "safe.directory=$RepoRoot" -C $RepoRoot ls-files
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed."
    }

    return @($files | Where-Object { $_ })
}

function Get-WorkingTreeChanges {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $changes = & git -c "safe.directory=$RepoRoot" -C $RepoRoot status --short
    if ($LASTEXITCODE -ne 0) {
        throw "git status failed."
    }

    return @($changes | Where-Object { $_ })
}

function Copy-ReleaseFile {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$RepoPath
    )

    $source = Join-Path $RepoRoot ($RepoPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $target = Join-Path $OutputPath ($RepoPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $targetDirectory = Split-Path -Parent $target

    if (-not (Test-Path -LiteralPath $targetDirectory)) {
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $source -Destination $target -Force
}

function Get-ExportedFiles {
    param([Parameter(Mandatory = $true)][string]$OutputPath)

    if (-not (Test-Path -LiteralPath $OutputPath)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $OutputPath -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($OutputPath.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        ConvertTo-RepoPath $relative
    })
}

function Assert-NoBlockedArtifacts {
    param(
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    $blockedFiles = Get-ExportedFiles $OutputPath | Where-Object {
        Test-ReleasePathExcluded -RepoPath $_ -Patterns $Patterns
    }

    if ($blockedFiles.Count -gt 0) {
        $sample = $blockedFiles | Select-Object -First 25
        throw "Client release export contains blocked artifacts:`n$($sample -join [Environment]::NewLine)"
    }
}

function Assert-SourceAssetsPreserved {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $assetRoots = @(
        'RvtPortal.Client/public',
        'RvtPortal.Client/src/assets'
    )

    foreach ($assetRoot in $assetRoots) {
        $sourceAssetRoot = Join-Path $RepoRoot ($assetRoot -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $sourceAssetRoot)) {
            continue
        }

        $trackedAssets = & git -c "safe.directory=$RepoRoot" -C $RepoRoot ls-files -- $assetRoot
        if ($LASTEXITCODE -ne 0) {
            throw "git ls-files failed for source asset root: $assetRoot"
        }

        $trackedAssets = @($trackedAssets | Where-Object { $_ })
        if ($trackedAssets.Count -eq 0) {
            continue
        }

        foreach ($asset in $trackedAssets) {
            $exportedAsset = Join-Path $OutputPath ($asset -replace '/', [System.IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $exportedAsset)) {
                throw "Tracked frontend source asset was not exported: $asset"
            }
        }
    }
}

function New-ReleaseZip {
    param(
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$Milestone
    )

    $zipPath = Join-Path (Split-Path -Parent $OutputPath) ($Milestone + '.zip')
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $OutputPath '*') -DestinationPath $zipPath -Force
    return $zipPath
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$exclusionConfig = Join-Path $scriptRoot 'client-release-exclusions.txt'

Assert-GitAvailable $repoRoot

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $repoParent = Split-Path -Parent $repoRoot
    $OutputRoot = Join-Path $repoParent 'rvtportal-client-releases'
}

$OutputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputRoot)
$outputPath = Join-Path $OutputRoot $Milestone
$patterns = Get-ReleaseExclusionPatterns $exclusionConfig
$trackedFiles = Get-TrackedFiles $repoRoot
$includedFiles = @()
$excludedFiles = @()

foreach ($file in $trackedFiles) {
    if (Test-ReleasePathExcluded -RepoPath $file -Patterns $patterns) {
        $excludedFiles += $file
    }
    else {
        $includedFiles += $file
    }
}

$changes = Get-WorkingTreeChanges $repoRoot
if ($changes.Count -gt 0) {
    Write-Warning "The working tree has uncommitted changes. Export uses Git-tracked file contents from the working tree."
    $changes | Select-Object -First 20 | ForEach-Object { Write-Warning "  $_" }

    if (-not $DryRun -and -not $AllowDirty) {
        throw "Refusing to create a client release export from a dirty working tree. Commit or stash changes, or pass -AllowDirty for a deliberate local preview export."
    }
}

Write-Host "Client release export"
Write-Host "  Repository : $repoRoot"
Write-Host "  Milestone  : $Milestone"
Write-Host "  Output     : $outputPath"
Write-Host "  Included   : $($includedFiles.Count) tracked files"
Write-Host "  Excluded   : $($excludedFiles.Count) tracked files"

if ($DryRun) {
    Write-Host "Dry run only. No files were copied."
    if ($excludedFiles.Count -gt 0) {
        Write-Host ""
        Write-Host "Excluded sample:"
        $excludedFiles | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
    }
    exit 0
}

if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) {
        throw "Output folder already exists: $outputPath. Use -Force to replace it."
    }

    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

foreach ($file in $includedFiles) {
    Copy-ReleaseFile -RepoRoot $repoRoot -OutputPath $outputPath -RepoPath $file
}

Assert-NoBlockedArtifacts -OutputPath $outputPath -Patterns $patterns
Assert-SourceAssetsPreserved -RepoRoot $repoRoot -OutputPath $outputPath

$zipPath = $null
if ($CreateZip) {
    $zipPath = New-ReleaseZip -OutputPath $outputPath -Milestone $Milestone
}

Write-Host ""
Write-Host "Client release export completed."
Write-Host "  Output folder: $outputPath"
if ($zipPath) {
    Write-Host "  Zip archive  : $zipPath"
}

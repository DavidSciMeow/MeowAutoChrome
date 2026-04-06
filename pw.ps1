<#
.SYNOPSIS
    Publish MeowAutoChrome.WebAPI as a self-contained executable (optional).
.DESCRIPTION
    Simple helper to publish the WebAPI project. Defaults to a Windows x64 self-contained publish
    and outputs to `publish\<rid>` under the repository root.

    Usage examples:
      .\pw.ps1                # publish win-x64 self-contained (Release)
      .\pw.ps1 -Rid linux-x64 # publish for linux-x64
      .\pw.ps1 -NoSelfContained # publish framework-dependent
#>

param(
    [string]$Rid = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$NoSelfContained
)

function Ensure-Command($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "Command '$cmd' not found. $hint" -ForegroundColor Yellow
        exit 1
    }
}

$RepoRoot = $PSScriptRoot
$Project = 'MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj'
$outDir = Join-Path $RepoRoot "publish\$Rid"

Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'

Write-Host "Publishing WebAPI project ($Project) -> $outDir" -ForegroundColor Cyan

# Build dotnet publish args
$dotnetArgs = @('publish', '-c', $Configuration, '-r', $Rid, '-o', $outDir, $Project)

if ($NoSelfContained) {
    $dotnetArgs += '--self-contained'
    $dotnetArgs += 'false'
} else {
    $dotnetArgs += '--self-contained'
    $dotnetArgs += 'true'
}

if ($NoRestore) { $dotnetArgs += '--no-restore' }

# Run publish
$rc = 0
try {
    & dotnet @dotnetArgs
    $rc = $LASTEXITCODE
} catch {
    Write-Host "dotnet publish failed: $_" -ForegroundColor Red
    exit 1
}

if ($rc -ne 0) {
    Write-Host "dotnet publish exited with code $rc" -ForegroundColor Red
    exit $rc
}

Write-Host "Publish finished. Output: $outDir" -ForegroundColor Green

# If Windows self-contained, verify exe exists
if (-not $NoSelfContained -and $Rid -match 'win') {
    $exe = Join-Path $outDir 'MeowAutoChrome.WebAPI.exe'
    if (-not (Test-Path $exe)) {
        Write-Host "Warning: expected executable not found: $exe" -ForegroundColor Yellow
    } else {
        Write-Host "Executable produced: $exe" -ForegroundColor Green
    }
}

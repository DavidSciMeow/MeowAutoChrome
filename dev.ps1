<#
.SYNOPSIS
    Start Electron in development mode (runs WebAPI via dotnet watch + Electron concurrently).
.DESCRIPTION
    This script runs `npm run dev` inside `MeowAutoChrome.Electron` which (per package.json)
    runs `concurrently "dotnet watch --project ../MeowAutoChrome.WebAPI/..." "electron ."`.

    Usage:
      .\dev.ps1            # ensure deps and run dev
      .\dev.ps1 -SkipInstall # skip npm install even if node_modules missing
#>

param(
    [switch]$SkipInstall
)

function Ensure-Command($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "Command '$cmd' not found. $hint" -ForegroundColor Yellow
        exit 1
    }
}

$RepoRoot = $PSScriptRoot
$ElectronDir = Join-Path $RepoRoot 'MeowAutoChrome.Electron'

Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'
Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'

if (-not (Test-Path (Join-Path $ElectronDir 'package.json'))) {
    Write-Host "Electron project not found at: $ElectronDir" -ForegroundColor Red
    exit 1
}

if (-not $SkipInstall -and -not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
    Write-Host 'Installing npm dependencies for Electron...' -ForegroundColor Cyan
    npm --prefix $ElectronDir install
}

Write-Host 'Starting Electron (dev): running `npm run dev` in MeowAutoChrome.Electron' -ForegroundColor Cyan
Push-Location $ElectronDir
try {
    # This runs the `dev` script from package.json and will block here showing output.
    & npm run dev
} finally {
    Pop-Location
}

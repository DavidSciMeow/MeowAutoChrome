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

Write-Host 'Starting Electron (dev-managed): running `npm run dev:managed` in MeowAutoChrome.Electron' -ForegroundColor Cyan

Push-Location $ElectronDir
try {
    # Simply start Electron; Electron main process will search for a packaged
    # backend under its resources/webapi paths and use it if present.
    & npm run dev:managed
} finally {
    Pop-Location
}

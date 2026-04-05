<#
.SYNOPSIS
    Publish WebAPI and build Electron distribution.
.DESCRIPTION
    Publishes the WebAPI as a self-contained executable and then runs Electron Builder to produce platform artifacts.
    Usage: .\pack.ps1 [-Rid win-x64]
#>

param(
    [string]$Rid = 'win-x64'
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

Write-Host "Publishing WebAPI for runtime $Rid..." -ForegroundColor Cyan
$outDir = Join-Path $RepoRoot "publish\$Rid"
dotnet publish -c Release -r $Rid --self-contained true -p:PublishSingleFile=false -o $outDir MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj

$exe = Join-Path $outDir 'MeowAutoChrome.WebAPI.exe'
if (-not (Test-Path $exe)) {
    Write-Host "Publish failed, executable not found: $exe" -ForegroundColor Red
    exit 1
}

# Set environment variable so Electron build can include the bundled backend
$env:MEOW_WEBAPI_EXEC = $exe

if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
    Write-Host 'Installing npm dependencies for Electron...' -ForegroundColor Cyan
    npm --prefix $ElectronDir install
}

Write-Host 'Running Electron pack (electron-builder)...' -ForegroundColor Cyan
npm --prefix $ElectronDir run dist

Write-Host 'Pack completed.' -ForegroundColor Green

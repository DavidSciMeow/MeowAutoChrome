<#
.SYNOPSIS
    Start MeowAutoChrome development helpers.
.DESCRIPTION
    Run from repository root to simplify starting the backend (dotnet watch)
    and the Electron shell. Messages are in English to avoid console encoding
    issues on systems that cannot display non-ASCII characters.
.PARAMETER Mode
    Optional mode:
        - dev      : Run `npm run dev` in MeowAutoChrome.Electron (starts dotnet watch + electron).
        - backend  : Start backend only (dotnet watch).
        - electron : Start Electron only (main.js will attempt to start/connect to backend).
        - publish  : Publish backend as self-contained exe (win-x64) and start Electron with MEOW_WEBAPI_EXEC.
.EXAMPLE
    .\start-dev.ps1 -Mode dev
#>

param(
    [ValidateSet("dev","backend","electron","publish")]
    [string]$Mode = "dev"
)

# Ensure console output uses UTF-8 to avoid garbled characters on some systems
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
    chcp 65001 | Out-Null
} catch {
    # ignore if setting encoding fails
}

function Ensure-Command($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "Command '$cmd' not found. $hint" -ForegroundColor Yellow
        exit 1
    }
}

 $RepoRoot = $PSScriptRoot
 $ElectronDir = Join-Path $RepoRoot 'MeowAutoChrome.Electron'

switch ($Mode) {
    'dev' {
        # Electron will spawn or connect to the WebAPI itself; prefer a single command to start Electron.
        Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'
        if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
            Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
            npm --prefix $ElectronDir install
        }
        Write-Host 'Starting development mode (Electron will spawn/connect to WebAPI)...' -ForegroundColor Green
        & npm --prefix $ElectronDir start
    }

    'backend' {
        Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'
        Write-Host 'Starting backend (dotnet watch)...' -ForegroundColor Green
        dotnet watch --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj run --urls http://127.0.0.1:5000
    }

    'electron' {
        Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'
        if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
            Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
            npm --prefix $ElectronDir install
        }
        Write-Host 'Starting Electron (main.js will try to start/connect to WebAPI)...' -ForegroundColor Green
        & npm --prefix $ElectronDir start
    }

    'publish' {
        Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'
        Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'
        $rid = 'win-x64'
        $outDir = Join-Path $RepoRoot "publish\$rid"
        Write-Host "Publishing backend to $outDir ..." -ForegroundColor Cyan
        dotnet publish -c Release -r $rid --self-contained true -p:PublishSingleFile=false -o $outDir MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj
        $exe = Join-Path $outDir 'MeowAutoChrome.WebAPI.exe'
        if (-not (Test-Path $exe)) {
            Write-Host "Publish failed, executable not found: $exe" -ForegroundColor Red
            exit 1
        }
        $env:MEOW_WEBAPI_EXEC = $exe
        if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
            Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
            npm --prefix $ElectronDir install
        }
        Write-Host "Starting Electron with published backend (MEOW_WEBAPI_EXEC=$exe) ..." -ForegroundColor Green
        & npm --prefix $ElectronDir start
    }

    default {
        Write-Host "Unknown mode: $Mode" -ForegroundColor Red
    }
}

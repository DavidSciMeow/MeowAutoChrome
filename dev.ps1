<#
.SYNOPSIS
    Start development mode: runs WebAPI (dotnet run) and Electron.
.DESCRIPTION
    A simplified development starter script. Modes:
      - dev      : Publish/run the WebAPI using `dotnet run` in background, then start Electron UI.
      - backend  : Run WebAPI only (foreground).
      - electron : Start Electron only.
.EXAMPLE
    .\dev.ps1 -Mode dev
#>

param(
    [ValidateSet("dev","backend","electron")]
    [string]$Mode = "dev"
)

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
        Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'
        Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'

        if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
            Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
            npm --prefix $ElectronDir install
        }

        Write-Host 'Starting backend (dotnet run)...' -ForegroundColor Green
        # Start backend in background so we can run Electron in foreground.
        $backendArgs = @('run','--project','MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj','--urls','http://127.0.0.1:5000')
        Start-Process -FilePath 'dotnet' -ArgumentList $backendArgs -WorkingDirectory $RepoRoot -NoNewWindow -PassThru | Out-Null

        Start-Sleep -Seconds 1
        Write-Host 'Starting Electron...' -ForegroundColor Green
        npm --prefix $ElectronDir start
    }

    'backend' {
        Ensure-Command dotnet 'Please install .NET SDK: https://dotnet.microsoft.com'
        Write-Host 'Starting backend (dotnet run)...' -ForegroundColor Green
        dotnet run --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj --urls http://127.0.0.1:5000
    }

    'electron' {
        Ensure-Command npm 'Please install Node.js + npm: https://nodejs.org'
        if (-not (Test-Path (Join-Path $ElectronDir 'node_modules'))) {
            Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
            npm --prefix $ElectronDir install
        }
        Write-Host 'Starting Electron...' -ForegroundColor Green
        npm --prefix $ElectronDir start
    }

    default {
        Write-Host "Unknown mode: $Mode" -ForegroundColor Red
    }
}

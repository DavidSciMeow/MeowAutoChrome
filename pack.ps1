[CmdletBinding()]
param(
    [ValidateSet('offline', 'online')]
    [string]$Mode = 'offline',

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [ValidateSet('installer', 'dir')]
    [string]$PackageTarget = 'installer',

    [switch]$Clean,

    [switch]$SkipDotnetRestore,

    [switch]$SkipNpmInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = $PSScriptRoot
$solutionPath = Join-Path $repoRoot 'MeowAutoChrome.slnx'
$webApiProjectPath = Join-Path $repoRoot 'MeowAutoChrome.WebAPI\MeowAutoChrome.WebAPI.csproj'
$electronDir = Join-Path $repoRoot 'MeowAutoChrome.Electron'
$webApiPublishDir = Join-Path $electronDir 'webapi'
$artifactDir = Join-Path $repoRoot 'Artifact\Electron'
$offlineArchiveSource = Join-Path $repoRoot 'chrome-win64.zip'
$offlineArchiveTarget = Join-Path $webApiPublishDir 'chrome-win64.zip'
$electronBuilderCli = Join-Path $electronDir 'node_modules\electron-builder\cli.js'
$packageLockPath = Join-Path $electronDir 'package-lock.json'

function Write-Step {
    param([string]$Message)

    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Assert-CommandAvailable {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Command not found: $Name"
    }
}

function Invoke-ExternalCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot
    )

    Push-Location $WorkingDirectory
    try {
        $commandLine = $FilePath
        if ($Arguments.Count -gt 0) {
            $commandLine = "$FilePath $($Arguments -join ' ')"
        }

        Write-Host $commandLine -ForegroundColor DarkGray
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code: $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

Assert-PathExists -Path $solutionPath -Description '解决方案文件'
Assert-PathExists -Path $webApiProjectPath -Description 'WebAPI 项目文件'
Assert-PathExists -Path $electronDir -Description 'Electron 项目目录'

Assert-CommandAvailable -Name 'dotnet'
Assert-CommandAvailable -Name 'npm'
Assert-CommandAvailable -Name 'node'

if ($Clean) {
    Write-Step 'Cleaning previous WebAPI publish output and Electron artifacts'

    if (Test-Path -LiteralPath $webApiPublishDir) {
        Remove-Item -LiteralPath $webApiPublishDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $artifactDir) {
        Remove-Item -LiteralPath $artifactDir -Recurse -Force
    }
}

if (-not $SkipDotnetRestore) {
    Write-Step 'Restoring .NET dependencies'
    Invoke-ExternalCommand -FilePath 'dotnet' -Arguments @('restore', $solutionPath)
}

Write-Step 'Publishing WebAPI into Electron webapi folder'
Invoke-ExternalCommand -FilePath 'dotnet' -Arguments @(
    'publish',
    $webApiProjectPath,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'false',
    '-o', $webApiPublishDir
)

if ($Mode -eq 'offline') {
    Write-Step 'Copying offline Chromium archive'

    if (-not (Test-Path -LiteralPath $offlineArchiveSource)) {
        throw "Offline mode requires chrome-win64.zip at repo root: $offlineArchiveSource"
    }

    Copy-Item -LiteralPath $offlineArchiveSource -Destination $offlineArchiveTarget -Force
}
elseif (Test-Path -LiteralPath $offlineArchiveTarget) {
    Write-Step 'Removing stale offline Chromium archive'
    Remove-Item -LiteralPath $offlineArchiveTarget -Force
}

if (-not (Test-Path -LiteralPath $electronBuilderCli)) {
    if ($SkipNpmInstall) {
        throw 'electron-builder is missing and -SkipNpmInstall was specified.'
    }

    Write-Step 'Installing Electron dependencies'
    if (Test-Path -LiteralPath $packageLockPath) {
        Invoke-ExternalCommand -FilePath 'npm' -Arguments @('ci') -WorkingDirectory $electronDir
    }
    else {
        Invoke-ExternalCommand -FilePath 'npm' -Arguments @('install') -WorkingDirectory $electronDir
    }
}
else {
    Write-Step 'Electron dependencies already present, skipping npm install'
}

if ($PackageTarget -eq 'dir') {
    Write-Step 'Running Electron directory packaging'
    Invoke-ExternalCommand -FilePath 'npm' -Arguments @('run', 'pack') -WorkingDirectory $electronDir
}
else {
    Write-Step 'Running Electron installer packaging'
    Invoke-ExternalCommand -FilePath 'npm' -Arguments @('run', 'dist') -WorkingDirectory $electronDir
}

Write-Step 'Packaging completed'
Write-Host "Mode: $Mode"
Write-Host "PackageTarget: $PackageTarget"
Write-Host "Artifacts: $artifactDir"

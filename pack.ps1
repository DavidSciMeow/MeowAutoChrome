[CmdletBinding()]
param(
    [ValidateSet('offline', 'online')]
    [string]$Mode = 'offline',

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [ValidateSet('installer', 'dir', 'zip')]
    [string]$PackageTarget = 'dir',

    [string]$BuilderCacheDir = '.\Artifact\ElectronBuilderCache',

    [switch]$PrepareInstallerCacheOnly,

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
$resolvedBuilderCacheDir = if ([System.IO.Path]::IsPathRooted($BuilderCacheDir)) { $BuilderCacheDir } else { Join-Path $repoRoot $BuilderCacheDir }
$offlineArchiveSource = Join-Path $repoRoot 'chrome-win64.zip'
$offlineArchiveTarget = Join-Path $webApiPublishDir 'chrome-win64.zip'
$electronBuilderCli = Join-Path $electronDir 'node_modules\electron-builder\cli.js'
$appBuilderPath = Join-Path $electronDir 'node_modules\app-builder-bin\win\x64\app-builder.exe'
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
        [string]$WorkingDirectory = $repoRoot,
        [hashtable]$Environment = @{}
    )

    Push-Location $WorkingDirectory
    try {
        $commandLine = $FilePath
        if ($Arguments.Count -gt 0) {
            $commandLine = "$FilePath $($Arguments -join ' ')"
        }

        Write-Host $commandLine -ForegroundColor DarkGray
        $previousValues = @{}
        foreach ($key in $Environment.Keys) {
            $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key], 'Process')
        }

        try {
            & $FilePath @Arguments
        }
        finally {
            foreach ($key in $Environment.Keys) {
                [Environment]::SetEnvironmentVariable($key, $previousValues[$key], 'Process')
            }
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code: $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Ensure-ElectronBuilderInstallerTools {
    param([string]$CacheDir)

    Ensure-Directory -Path $CacheDir

    $envMap = @{ 'ELECTRON_BUILDER_CACHE' = $CacheDir }
    $artifacts = @(
        @(
            'nsis-3.0.4.1',
            'https://github.com/electron-userland/electron-builder-binaries/releases/download/nsis-3.0.4.1/nsis-3.0.4.1.7z',
            'VKMiizYdmNdJOWpRGz4trl4lD++BvYP2irAXpMilheUP0pc93iKlWAoP843Vlraj8YG19CVn0j+dCo/hURz9+Q=='
        ),
        @(
            'nsis-resources-3.4.1',
            'https://github.com/electron-userland/electron-builder-binaries/releases/download/nsis-resources-3.4.1/nsis-resources-3.4.1.7z',
            'Dqd6g+2buwwvoG1Vyf6BHR1b+25QMmPcwZx40atOT57gH27rkjOei1L0JTldxZu4NFoEmW4kJgZ3DlSWVON3+Q=='
        )
    )

    foreach ($artifact in $artifacts) {
        $name = $artifact[0]
        $url = $artifact[1]
        $sha512 = $artifact[2]
        $artifactPath = Join-Path $CacheDir $name

        if (Test-Path -LiteralPath $artifactPath) {
            Write-Step "Using cached installer tool $name"
            continue
        }

        Write-Step "Downloading installer tool $name"
        Invoke-ExternalCommand -FilePath $appBuilderPath -Arguments @(
            'download-artifact',
            '--name', $name,
            '--url', $url,
            '--sha512', $sha512
        ) -WorkingDirectory $electronDir -Environment $envMap
    }
}

Assert-PathExists -Path $solutionPath -Description '解决方案文件'
Assert-PathExists -Path $webApiProjectPath -Description 'WebAPI 项目文件'
Assert-PathExists -Path $electronDir -Description 'Electron 项目目录'

Assert-CommandAvailable -Name 'dotnet'
Assert-CommandAvailable -Name 'npm'
Assert-CommandAvailable -Name 'node'

Ensure-Directory -Path $resolvedBuilderCacheDir

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

    Write-Step 'Restoring WebAPI runtime-specific assets'
    Invoke-ExternalCommand -FilePath 'dotnet' -Arguments @(
        'restore',
        $webApiProjectPath,
        '-r', $Runtime
    )
}

Write-Step 'Publishing WebAPI into Electron webapi folder'
$publishArguments = @(
    'publish',
    $webApiProjectPath,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'false',
    '-o', $webApiPublishDir
)

if (-not $SkipDotnetRestore) {
    $publishArguments += '--no-restore'
}

Invoke-ExternalCommand -FilePath 'dotnet' -Arguments $publishArguments

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

if ($PackageTarget -eq 'installer') {
    Ensure-ElectronBuilderInstallerTools -CacheDir $resolvedBuilderCacheDir
    if ($PrepareInstallerCacheOnly) {
        Write-Step 'Installer tool cache is ready'
        Write-Host "BuilderCacheDir: $resolvedBuilderCacheDir"
        return
    }
}

if ($PackageTarget -eq 'dir') {
    Write-Step 'Running Electron directory packaging'
    Invoke-ExternalCommand -FilePath 'npm' -Arguments @('run', 'pack') -WorkingDirectory $electronDir -Environment @{ 'ELECTRON_BUILDER_CACHE' = $resolvedBuilderCacheDir }
}
elseif ($PackageTarget -eq 'zip') {
    Write-Step 'Running Electron zip packaging'
    Invoke-ExternalCommand -FilePath 'node' -Arguments @(
        $electronBuilderCli,
        '--win',
        'zip',
        '--x64'
    ) -WorkingDirectory $electronDir -Environment @{ 'ELECTRON_BUILDER_CACHE' = $resolvedBuilderCacheDir }
}
else {
    Write-Step 'Running Electron installer packaging'
    Invoke-ExternalCommand -FilePath 'npm' -Arguments @('run', 'dist') -WorkingDirectory $electronDir -Environment @{ 'ELECTRON_BUILDER_CACHE' = $resolvedBuilderCacheDir }
}

Write-Step 'Packaging completed'
Write-Host "Mode: $Mode"
Write-Host "PackageTarget: $PackageTarget"
Write-Host "BuilderCacheDir: $resolvedBuilderCacheDir"
Write-Host "Artifacts: $artifactDir"

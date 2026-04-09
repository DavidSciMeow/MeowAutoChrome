<#
.SYNOPSIS
打包 MeowAutoChrome Electron 安装包，支持在线安装包和离线安装包两种模式。

.DESCRIPTION
先发布 MeowAutoChrome.WebAPI，再按打包模式决定是否把根目录的 chrome-win64.zip 复制进发布目录，
最后执行 electron-builder 生成 Windows 安装包。online 包不附带离线浏览器压缩包，offline 包会附带它。

.PARAMETER HttpsProxy
HTTPS 代理地址。会同时传给 Playwright、npm 和 electron-builder 下载流程。

.PARAMETER HttpProxy
HTTP 代理地址。通常可与 HttpsProxy 保持一致。

.PARAMETER PackageMode
打包模式：online、offline 或 both。

.PARAMETER OfflineChromiumZip
离线 Chromium 压缩包路径，默认使用仓库根目录下的 chrome-win64.zip。

.PARAMETER NodeExtraCaCerts
自定义根证书路径。适用于公司代理拦截 HTTPS 的场景。

.EXAMPLE
./package-app.ps1 -HttpsProxy http://127.0.0.1:7890 -HttpProxy http://127.0.0.1:7890

.EXAMPLE
./package-app.ps1 -PackageMode both
#>
[CmdletBinding()]
param(
    [string]$HttpsProxy,
    [string]$HttpProxy,
    [string]$NodeExtraCaCerts,
    [ValidateSet('online', 'offline', 'both')]
    [string]$PackageMode = 'both',
    [string]$OfflineChromiumZip
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$electronDir = Join-Path $root 'MeowAutoChrome.Electron'
$webApiProject = Join-Path $root 'MeowAutoChrome.WebAPI\MeowAutoChrome.WebAPI.csproj'
$webApiPublishDir = Join-Path $electronDir 'webapi'
$artifactDir = Join-Path $root 'Artifact\Electron'
$offlineChromiumZipTarget = Join-Path $webApiPublishDir 'chrome-win64.zip'

if ([string]::IsNullOrWhiteSpace($OfflineChromiumZip)) {
    $OfflineChromiumZip = Join-Path $root 'chrome-win64.zip'
}

function Set-ScopedEnvVar {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [hashtable]$Store
    )

    if (-not $Store.ContainsKey($Name)) {
        $Store[$Name] = [Environment]::GetEnvironmentVariable($Name, 'Process')
    }

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item "Env:$Name" -ErrorAction SilentlyContinue
    }
    else {
        [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    }
}

$scopedEnv = @{}

if ([string]::IsNullOrWhiteSpace($HttpProxy) -and -not [string]::IsNullOrWhiteSpace($HttpsProxy)) {
    $HttpProxy = $HttpsProxy
}

if (-not [string]::IsNullOrWhiteSpace($HttpsProxy)) {
    Set-ScopedEnvVar -Name 'HTTPS_PROXY' -Value $HttpsProxy -Store $scopedEnv
    Set-ScopedEnvVar -Name 'https_proxy' -Value $HttpsProxy -Store $scopedEnv
    Set-ScopedEnvVar -Name 'NPM_CONFIG_HTTPS_PROXY' -Value $HttpsProxy -Store $scopedEnv
}

if (-not [string]::IsNullOrWhiteSpace($HttpProxy)) {
    Set-ScopedEnvVar -Name 'HTTP_PROXY' -Value $HttpProxy -Store $scopedEnv
    Set-ScopedEnvVar -Name 'http_proxy' -Value $HttpProxy -Store $scopedEnv
    Set-ScopedEnvVar -Name 'NPM_CONFIG_PROXY' -Value $HttpProxy -Store $scopedEnv
}

if (-not [string]::IsNullOrWhiteSpace($NodeExtraCaCerts)) {
    Set-ScopedEnvVar -Name 'NODE_EXTRA_CA_CERTS' -Value $NodeExtraCaCerts -Store $scopedEnv
}

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }
}

function Clear-ArtifactStagingRoot {
    param([string]$RootPath)

    if (-not (Test-Path $RootPath)) {
        New-Item -ItemType Directory -Path $RootPath | Out-Null
        return
    }

    Get-ChildItem $RootPath -Force | Where-Object { $_.Name -notin @('online', 'offline') } | Remove-Item -Recurse -Force
}

function Sync-OfflineArchive {
    param(
        [ValidateSet('online', 'offline')]
        [string]$Variant
    )

    if (Test-Path $offlineChromiumZipTarget) {
        Remove-Item $offlineChromiumZipTarget -Force
    }

    if ($Variant -eq 'offline') {
        if (-not (Test-Path $OfflineChromiumZip)) {
            throw "Offline Chromium archive not found: $OfflineChromiumZip"
        }

        Copy-Item $OfflineChromiumZip -Destination $offlineChromiumZipTarget -Force
    }
}

function Move-BuildArtifactsToVariantDir {
    param(
        [ValidateSet('online', 'offline')]
        [string]$Variant
    )

    $variantDir = Join-Path $artifactDir $Variant
    Remove-PathIfExists $variantDir
    New-Item -ItemType Directory -Path $variantDir -Force | Out-Null

    Get-ChildItem $artifactDir -Force | Where-Object { $_.Name -notin @('online', 'offline') } | ForEach-Object {
        Move-Item $_.FullName -Destination (Join-Path $variantDir $_.Name)
    }
}

function Build-PackageVariant {
    param(
        [ValidateSet('online', 'offline')]
        [string]$Variant
    )

    Write-Host "Building $Variant package ..."

    Remove-PathIfExists $webApiPublishDir

    Write-Host 'Publishing WebAPI (Release, win-x64, self-contained)...'
    dotnet publish $webApiProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $webApiPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Sync-OfflineArchive -Variant $Variant

    Push-Location $electronDir
    try {
        Set-ScopedEnvVar -Name 'CSC_IDENTITY_AUTO_DISCOVERY' -Value 'false' -Store $scopedEnv
        Set-ScopedEnvVar -Name 'WIN_CSC_LINK' -Value '' -Store $scopedEnv

        if (-not (Test-Path (Join-Path $electronDir 'node_modules'))) {
            Write-Host 'Installing Electron dependencies with npm ci...'
            npm ci
            if ($LASTEXITCODE -ne 0) {
                throw "npm ci failed with exit code $LASTEXITCODE"
            }
        }

        Clear-ArtifactStagingRoot -RootPath $artifactDir

        Write-Host 'Building Electron distributable...'
        npm run dist
        if ($LASTEXITCODE -ne 0) {
            throw "electron-builder failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Move-BuildArtifactsToVariantDir -Variant $Variant
}

try {
    switch ($PackageMode) {
        'online' { Build-PackageVariant -Variant 'online' }
        'offline' { Build-PackageVariant -Variant 'offline' }
        'both' {
            Build-PackageVariant -Variant 'online'
            Build-PackageVariant -Variant 'offline'
        }
    }
}
finally {
    foreach ($entry in $scopedEnv.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

Write-Host "Package build completed. Output directory: $artifactDir"
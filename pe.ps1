<#
.SYNOPSIS
    Publish WebAPI and package Electron app (local builds).
.DESCRIPTION
    - Publishes `MeowAutoChrome.WebAPI` as self-contained single-file per target RID.
    - Copies the published WebAPI into `MeowAutoChrome.Electron/webapi`.
    - Runs `electron-builder` (via `npx` / `npm run dist`) to create platform installer(s).

USAGE
    .\pe.ps1                      # try all targets (will only package current host's platform)
    .\pe.ps1 -Targets win         # only build for Windows
    .\pe.ps1 -Targets win,linux   # build WebAPI for win and linux; only package what host supports

NOTES
    - Self-contained publish produces a platform-specific executable; include those in the Electron app.
    - macOS signed/notarized builds must be done on macOS (or CI macOS runner).
#>

[CmdletBinding()]
param(
    [string[]]$Targets = @('win','linux','mac'),
    [string]$Configuration = 'Release',
    [ValidateSet('x64','arm64','all')][string]$Arch = 'x64',
    [switch]$SkipNpmInstall
)

function Is-OsPlatform($plat) {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($plat)
}


$Root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ElectronDir = Join-Path $Root 'MeowAutoChrome.Electron'
$WebApiProj = Join-Path $Root 'MeowAutoChrome.WebAPI\MeowAutoChrome.WebAPI.csproj'
$Artifact = Join-Path $Root 'Artifact'

Write-Host "Repository root: $Root"
New-Item -ItemType Directory -Force -Path $Artifact | Out-Null

$IsWindows = Is-OsPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
$IsLinux = Is-OsPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
$IsMac = Is-OsPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)

function Command-Exists($cmd) {
    return (Get-Command $cmd -ErrorAction SilentlyContinue) -ne $null
}

# Determine arch list: allow building both x64 and arm64 when 'all' is requested
if ($Arch -eq 'all') { $ArchList = @('x64','arm64') } else { $ArchList = @($Arch) }

# Check for Docker (used to build Linux packages on non-Linux hosts)
$HasDocker = Command-Exists 'docker'

# Ensure Node deps for electron
if (-not $SkipNpmInstall) {
    if (-not (Test-Path $ElectronDir)) { throw "Electron dir not found: $ElectronDir" }
    Push-Location $ElectronDir
    if (Test-Path 'package-lock.json') { Write-Host 'npm ci (using package-lock.json)...'; npm ci } else { Write-Host 'npm install...'; npm install }
    Pop-Location
}

# Base RID prefix map
$baseMap = @{ win = 'win'; linux = 'linux'; mac = 'osx' }

foreach ($t in $Targets) {
    $target = $t.ToLower().Trim()
    if (-not $baseMap.ContainsKey($target)) { Write-Warning "Unknown target: $t — skip"; continue }
    $baseRidPrefix = $baseMap[$target]

    foreach ($arch in $ArchList) {
        $rid = "$baseRidPrefix-$arch"

        $publishOut = Join-Path $Artifact (Join-Path 'WebAPI' $rid)
        if (Test-Path $publishOut) { Remove-Item -Recurse -Force $publishOut }
        New-Item -ItemType Directory -Force -Path $publishOut | Out-Null

        Write-Host "Publishing WebAPI for RID=$rid to $publishOut"
        try {
            & dotnet publish $WebApiProj -c $Configuration -r $rid --self-contained true "/p:PublishSingleFile=true" "/p:PublishTrimmed=false" "-o" $publishOut
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish returned exit code $LASTEXITCODE" }
        } catch {
            Write-Warning ("dotnet publish failed for {0}: {1}" -f $rid, $_)
            continue
        }

        # Copy published output into electron webapi folder (will be included by extraResources config)
        $ElectronWebApi = Join-Path $ElectronDir 'webapi'
        if (Test-Path $ElectronWebApi) { Remove-Item -Recurse -Force $ElectronWebApi }
        New-Item -ItemType Directory -Force -Path $ElectronWebApi | Out-Null
        Write-Host "Copying published WebAPI into $ElectronWebApi"
        Copy-Item -Path (Join-Path $publishOut '*') -Destination $ElectronWebApi -Recurse -Force

        # Decide whether we can package natively on this host
        $canPackageNative = $false
        if ($IsWindows -and $target -eq 'win') { $canPackageNative = $true }
        elseif ($IsLinux -and $target -eq 'linux') { $canPackageNative = $true }
        elseif ($IsMac -and $target -eq 'mac') { $canPackageNative = $true }

        if ($canPackageNative) {
            Push-Location $ElectronDir
            try {
                Write-Host ("Packaging Electron natively for target: {0} (arch: {1})" -f $target, $arch)
                switch ($target) {
                    'win' { $targetArg = "--win --$arch" }
                    'linux' { $targetArg = "--linux --$arch" }
                    'mac' { $targetArg = "--mac --$arch" }
                }
                $cmd = "npx --yes electron-builder -c electron-builder.json $targetArg --publish never"
                Write-Host ("Running: {0}" -f $cmd)
                iex $cmd
                if ($LASTEXITCODE -ne 0) { throw "electron-builder failed with exit code $LASTEXITCODE" }
            } catch {
                Write-Warning ("Packaging failed for {0}: {1}" -f $target, $_)
            } finally {
                Pop-Location
            }
        } else {
            # Attempt Docker-based packaging for Linux when host cannot build it natively
            if ($target -eq 'linux' -and $HasDocker) {
                Write-Host "Attempting Linux packaging inside Docker (electronuserland/builder)."
                Push-Location $ElectronDir
                try {
                    $dockerArgs = @("run","--rm","-v","${ElectronDir}:/project","-w","/project","electronuserland/builder:latest","/bin/sh","-c","chmod +x webapi/MeowAutoChrome.WebAPI || true && npm ci --no-audit --no-fund && npx --yes electron-builder -c electron-builder.json --linux --$arch --publish never")
                    Write-Host "docker $($dockerArgs -join ' ')"
                    $proc = Start-Process -FilePath "docker" -ArgumentList $dockerArgs -NoNewWindow -Wait -PassThru
                    if ($proc.ExitCode -ne 0) { throw "docker-based packaging failed with exit code $($proc.ExitCode)" }
                } catch {
                    Write-Warning ("Docker packaging failed for linux: {0}" -f $_)
                } finally { Pop-Location }
            } else {
                Write-Host "Published WebAPI for $target (RID=$rid) and copied into Electron. Packaging skipped on this host; run on matching OS or use CI."
            }
        }

        # Move build outputs (if any) into per-rid folder to avoid overwriting when building multiple targets
        $builderOutRoot = Join-Path $Artifact 'Electron'
        if (Test-Path $builderOutRoot) {
            $items = Get-ChildItem -Path $builderOutRoot -Force
            if ($items -and $items.Count -gt 0) {
                $targetArtifact = Join-Path $Artifact ("Electron\$rid")
                if (Test-Path $targetArtifact) { Remove-Item -Recurse -Force $targetArtifact }
                New-Item -ItemType Directory -Force -Path $targetArtifact | Out-Null
                foreach ($it in $items) {
                    try {
                        $dest = Join-Path $targetArtifact $it.Name
                        Move-Item -Path $it.FullName -Destination $dest -Force
                    } catch {
                        try { Copy-Item -Path $it.FullName -Destination $targetArtifact -Recurse -Force } catch { }
                        try { Remove-Item -Path $it.FullName -Recurse -Force -ErrorAction SilentlyContinue } catch { }
                    }
                }
            }
        }

    } # end arch loop
}

Write-Host "All done. Artifacts (WebAPI publish outputs and Electron outputs) are under: $Artifact"
Write-Host "Note: macOS signed/notarized builds must be run on macOS (or CI with macOS runner)."

# End of script

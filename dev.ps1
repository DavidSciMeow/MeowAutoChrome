dotnet publish .\MeowAutoChrome.WebAPI\MeowAutoChrome.WebAPI.csproj -c Release -r win-x64 --self-contained false -o .\MeowAutoChrome.Electron\webapi;

$offlineChromiumZip = Join-Path $PSScriptRoot 'chrome-win64.zip'
$offlineChromiumZipTarget = Join-Path $PSScriptRoot 'MeowAutoChrome.Electron\webapi\chrome-win64.zip'
if (Test-Path $offlineChromiumZip) {
    Copy-Item $offlineChromiumZip -Destination $offlineChromiumZipTarget -Force
} elseif (Test-Path $offlineChromiumZipTarget) {
    Remove-Item $offlineChromiumZipTarget -Force
}

Push-Location 'MeowAutoChrome.Electron'
try {
    # Simply start Electron; Electron main process will search for a packaged
    # backend under its resources/webapi paths and use it if present.
    & npm run dev:managed
} finally {
    Pop-Location
}

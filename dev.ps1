dotnet publish .\MeowAutoChrome.WebAPI\MeowAutoChrome.WebAPI.csproj -c Release -r win-x64 --self-contained false -o .\MeowAutoChrome.Electron\webapi;

Push-Location 'MeowAutoChrome.Electron'
try {
    # Simply start Electron; Electron main process will search for a packaged
    # backend under its resources/webapi paths and use it if present.
    & npm run dev:managed
} finally {
    Pop-Location
}

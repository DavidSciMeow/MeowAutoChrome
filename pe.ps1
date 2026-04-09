$outputDir = Join-Path $env:LOCALAPPDATA 'MeowAutoChrome\Plugins\ExamplePlugin'

if (Test-Path $outputDir) {
	Remove-Item $outputDir -Recurse -Force
}

dotnet publish .\MeowAutoChrome.ExamplePlugin\MeowAutoChrome.ExamplePlugin.csproj -c Release -o $outputDir


param(
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition))
)
$src = Join-Path $Root 'MeowAutoChrome.Electron\webapi'
$dst = Join-Path $Root 'Artifact\Standalone\win-x64\resources\app\webapi'
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Force
Write-Host 'webapi copied:'
Get-ChildItem -Path $dst | Select-Object Name,Length,Mode | Format-Table -AutoSize

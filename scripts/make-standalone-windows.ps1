param(
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition))
)
$ElectronDir = Join-Path $Root 'MeowAutoChrome.Electron'
$ElectronDist = Join-Path $ElectronDir 'node_modules\electron\dist'
$dest = Join-Path $Root 'Artifact\Standalone\win-x64'

if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Write-Host 'Copying electron runtime...'
robocopy $ElectronDist $dest * /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null

Write-Host 'Copying app files...'
New-Item -ItemType Directory -Force -Path (Join-Path $dest 'resources\app') | Out-Null
robocopy $ElectronDir (Join-Path $dest 'resources\app') * /E /XD node_modules Artifact .git /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null

Write-Host 'Copying published WebAPI into app/webapi...'
Remove-Item -Recurse -Force (Join-Path $dest 'resources\app\webapi') -ErrorAction SilentlyContinue
robocopy (Join-Path $Root 'Artifact\WebAPI\win-x64') (Join-Path $dest 'resources\app\webapi') * /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null

Write-Host 'Done. Listing output:'
Get-ChildItem -Path $dest -Recurse -Depth 3 | Select-Object -First 400 | Format-Table -AutoSize

if (Test-Path (Join-Path $dest 'resources\app\webapi')) {
    Write-Host 'webapi present:'
    Get-ChildItem -Path (Join-Path $dest 'resources\app\webapi') | Select-Object -First 200 | Format-Table -AutoSize
} else {
    Write-Host 'webapi folder not found in portable app; please ensure Artifact\WebAPI\win-x64 exists and try again.'
}

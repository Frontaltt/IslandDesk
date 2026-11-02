$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Output = Join-Path $Root 'dist'
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
New-Item -ItemType Directory -Path $Output | Out-Null

Write-Host 'Publishing Island Desk overlay...' -ForegroundColor Cyan
dotnet publish (Join-Path $Root 'IslandDesk.csproj') -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Island Desk publish failed.' }

Write-Host 'Publishing Island Desk Studio...' -ForegroundColor Cyan
dotnet publish (Join-Path $Root 'Studio\IslandDesk.Studio.csproj') -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Island Desk Studio publish failed.' }


Write-Host 'Done. Both executables and their runtime files are in .\dist' -ForegroundColor Green
Write-Host 'Run IslandDesk.exe for the island and IslandDesk.Studio.exe for the editor.'

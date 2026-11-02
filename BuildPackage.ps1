$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Publish = Join-Path $Root 'publish-overlay'
$StudioPublish = Join-Path $Root 'publish-studio'
$Stage = Join-Path $Root 'msix-stage'
$Output = Join-Path $Root 'IslandDesk.msix'
$Pfx = Join-Path $Root 'IslandDesk-dev-signing.pfx'
$Cer = Join-Path $Root 'IslandDesk-dev-signing.cer'
$PasswordText = 'IslandDesk-local-signing'

if (Test-Path $Publish) { Remove-Item $Publish -Recurse -Force }
if (Test-Path $StudioPublish) { Remove-Item $StudioPublish -Recurse -Force }

Write-Host '1/6  Publishing Island Desk overlay...' -ForegroundColor Cyan
dotnet publish (Join-Path $Root 'IslandDesk.csproj') -c Release -r win-x64 --self-contained true -o $Publish
if ($LASTEXITCODE -ne 0) { throw 'Island Desk publish failed.' }

Write-Host '2/6  Publishing Island Desk Studio...' -ForegroundColor Cyan
dotnet publish (Join-Path $Root 'Studio\IslandDesk.Studio.csproj') -c Release -r win-x64 --self-contained true -o $StudioPublish
if ($LASTEXITCODE -ne 0) { throw 'Studio publish failed.' }

Write-Host '3/6  Preparing MSIX folder...' -ForegroundColor Cyan
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Path $Stage | Out-Null
Copy-Item (Join-Path $Publish '*') $Stage -Recurse -Force
Copy-Item (Join-Path $StudioPublish '*') $Stage -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $Stage 'Assets') -Force | Out-Null
Copy-Item (Join-Path $Root 'Assets\*') (Join-Path $Stage 'Assets') -Recurse -Force
Copy-Item (Join-Path $Root 'Package.appxmanifest') (Join-Path $Stage 'AppxManifest.xml') -Force

$KitBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$MakeAppx = Get-ChildItem $KitBin -Filter MakeAppx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\MakeAppx.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
$SignTool = Get-ChildItem $KitBin -Filter SignTool.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\SignTool.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $MakeAppx -or -not $SignTool) {
    throw 'Windows SDK tools not found. Install the Windows 11 SDK from Visual Studio Installer.'
}

Write-Host '4/6  Creating MSIX...' -ForegroundColor Cyan
if (Test-Path $Output) { Remove-Item $Output -Force }
& $MakeAppx.FullName pack /d $Stage /p $Output /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed.' }

Write-Host '5/6  Creating a local signing certificate...' -ForegroundColor Cyan
$Cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=Frontalt' } | Select-Object -First 1
if (-not $Cert) {
    $Cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=Frontalt' `
        -KeyUsage DigitalSignature -FriendlyName 'Island Desk local package certificate' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
}
if (Test-Path $Pfx) { Remove-Item $Pfx -Force }
if (Test-Path $Cer) { Remove-Item $Cer -Force }
$SecurePassword = ConvertTo-SecureString -String $PasswordText -Force -AsPlainText
Export-PfxCertificate -Cert $Cert -FilePath $Pfx -Password $SecurePassword | Out-Null
Export-Certificate -Cert $Cert -FilePath $Cer -Force | Out-Null
Import-Certificate -FilePath $Cer -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople' -ErrorAction SilentlyContinue | Out-Null

Write-Host '6/6  Signing and installing Island Desk + Studio...' -ForegroundColor Cyan
& $SignTool.FullName sign /fd SHA256 /f $Pfx /p $PasswordText $Output
if ($LASTEXITCODE -ne 0) { throw 'Package signing failed.' }
& $SignTool.FullName verify /pa /v $Output
if ($LASTEXITCODE -ne 0) { throw 'MSIX signature verification failed.' }
# The private key is needed only for signing. Keep the public .cer for local trust, remove the PFX.
if (Test-Path $Pfx) { Remove-Item $Pfx -Force }
# Add the public development certificate to both local-machine trust stores.
# The package must be signed by a certificate Windows can chain to a trusted root.
$MachineRoot = Get-ChildItem 'Cert:\LocalMachine\Root' -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $Cert.Thumbprint } | Select-Object -First 1
$MachineTrusted = Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $Cert.Thumbprint } | Select-Object -First 1
if (-not $MachineRoot -or -not $MachineTrusted) {
    Write-Host 'Adding the local development certificate to LocalMachine\Root and TrustedPeople (UAC may appear)...' -ForegroundColor Yellow
    $escapedCer = $Cer.Replace("'", "''")
    $trustScript = Join-Path $env:TEMP 'IslandDesk-Import-DevCertificate.ps1'
    @(
        "`$ErrorActionPreference = 'Stop'"
        "`$certificatePath = '$escapedCer'"
        'try {'
        "    Import-Certificate -FilePath `$certificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null"
        "    Import-Certificate -FilePath `$certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null"
        '    exit 0'
        '} catch {'
        '    Write-Error $_'
        '    exit 1'
        '}'
    ) | Set-Content -LiteralPath $trustScript -Encoding UTF8

    try {
        $elevated = Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -PassThru -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$trustScript`""
        )
    }
    finally {
        if (Test-Path $trustScript) { Remove-Item $trustScript -Force -ErrorAction SilentlyContinue }
    }
    if ($elevated.ExitCode -ne 0) {
        throw "Certificate trust import failed. Open PowerShell as Administrator and run: Import-Certificate -FilePath `"$Cer`" -CertStoreLocation 'Cert:\LocalMachine\Root'; then import it to 'Cert:\LocalMachine\TrustedPeople'."
    }
}

# Confirm the trust stores contain the exact certificate that signed this package.
$rootConfirmed = Get-ChildItem 'Cert:\LocalMachine\Root' -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $Cert.Thumbprint } | Select-Object -First 1
$peopleConfirmed = Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $Cert.Thumbprint } | Select-Object -First 1
if (-not $rootConfirmed -or -not $peopleConfirmed) {
    throw 'The development certificate is not trusted by LocalMachine. Run BuildPackage.ps1 again as Administrator.'
}

Add-AppxPackage -Path $Output
Write-Host 'Island Desk and Island Desk Studio installed. Both are available from Start.' -ForegroundColor Green
Write-Host "MSIX: $Output"

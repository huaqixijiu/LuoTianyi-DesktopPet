[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('NetFramework48')]
    [string]$Framework = 'NetFramework48',
    [ValidateSet('Development', 'Production')]
    [string]$SigningMode = 'Development',
    [string]$ProductionCertificatePath,
    [string]$ProductionCertificatePasswordPath,
    [string]$DevelopmentIdentityName = 'LuoTianyiPet.Dev',
    [string]$ProductionIdentityName = 'LuoTianyiPet',
    [string]$ProductionPublisherDisplayName = '洛天依桌宠'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$versionPropsPath = Join-Path $repoRoot 'config\version.props'
if (!(Test-Path -LiteralPath $versionPropsPath -PathType Leaf)) {
    throw "Version source was not found: $versionPropsPath"
}
[xml]$versionProps = Get-Content -LiteralPath $versionPropsPath -Raw
$configuredVersion = [string]$versionProps.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $configuredVersion
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Invalid four-part version: $Version"
}
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\external-location'))
$stagingRoot = Join-Path $artifactRoot 'staging'
$releaseRoot = Join-Path $artifactRoot 'release'
$privateRoot = Join-Path $artifactRoot 'private'
$bundleName = "LuoTianyiPet-Installer-$Version-win-x64"
$bundleRoot = Join-Path $stagingRoot $bundleName
$payloadRoot = Join-Path $bundleRoot 'payload'
$manifestPath = Join-Path $stagingRoot 'Package.appxmanifest'
$identityPath = Join-Path $bundleRoot 'LuoTianyiPet.Identity.msix'
$bundlePath = Join-Path $releaseRoot "$bundleName.zip"

function Assert-ArtifactPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $prefix = $artifactRoot.TrimEnd('\') + '\'
    if (!$resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the external-location artifact directory: $resolved"
    }
}

function Reset-ArtifactDirectory([string]$Path) {
    Assert-ArtifactPath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Find-Executable([string[]]$Candidates, [string]$Name) {
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }
    throw "Required tool was not found: $Name"
}

function Assert-NoRawSourceDirectories([string]$Root) {
    foreach ($relativePath in @('原素材', '候选素材_官方', '.local-tools', 'assets\source')) {
        $forbiddenPath = Join-Path $Root $relativePath
        if (Test-Path -LiteralPath $forbiddenPath) {
            throw "External-location bundle contains forbidden source directory: $relativePath"
        }
    }
}

function Set-ManifestToken([string]$Text, [string]$Token, [string]$Value) {
    return $Text.Replace($Token, [System.Security.SecurityElement]::Escape($Value))
}

$dotnet = Find-Executable @(
    "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
    'C:\Program Files\dotnet\dotnet.exe'
) 'dotnet'
$winapp = Find-Executable @(
    "$env:LOCALAPPDATA\Microsoft\WindowsApps\winapp.exe"
) 'winapp'
$python = Find-Executable @(
    (Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python313\python.exe')
) 'python'

Reset-ArtifactDirectory $stagingRoot
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

$applicationProject = Join-Path $repoRoot 'src\LuoTianyiPet.App\LuoTianyiPet.App.csproj'
& $dotnet publish $applicationProject `
    -c Release `
    -f net48 `
    -r $Runtime `
    --self-contained false `
    -p:PlatformTarget=x64 `
    -p:Prefer32Bit=false `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $payloadRoot
if ($LASTEXITCODE -ne 0) {
    throw '.NET Framework 4.8 external-location publish failed.'
}

& $python (Join-Path $repoRoot 'tools\packaging\generate_package_assets.py') `
    --source (Join-Path $repoRoot 'assets\app\luotianyi-pet.png') `
    --output (Join-Path $payloadRoot 'assets\package') `
    --frame-width 512 `
    --frame-height 512 `
    --frame-index 0
if ($LASTEXITCODE -ne 0) {
    throw 'Package asset generation failed.'
}

Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\卸载洛天依桌宠.cmd') `
    -Destination $payloadRoot -Force
Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\uninstall-luotianyi-pet.ps1') `
    -Destination $payloadRoot -Force
Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\LUOTIANYI_PET_EXTERNAL_LOCATION_BUNDLE.marker') `
    -Destination (Join-Path $payloadRoot 'LUOTIANYI_PET_INSTALLED.marker') -Force

$expectedPublisher = 'CN=LuoTianyiPet Development'
$publisherDisplayName = '洛天依桌宠开发版'
$certificatePassword = $null
$certificatePath = $null
$publicCertificatePath = $null
if ($SigningMode -eq 'Production') {
    if ([string]::IsNullOrWhiteSpace($ProductionCertificatePath) -or
        !(Test-Path -LiteralPath $ProductionCertificatePath -PathType Leaf)) {
        throw 'Production signing requires -ProductionCertificatePath pointing to a trusted code-signing PFX.'
    }
    if ([string]::IsNullOrWhiteSpace($ProductionCertificatePasswordPath) -or
        !(Test-Path -LiteralPath $ProductionCertificatePasswordPath -PathType Leaf)) {
        throw 'Production signing requires -ProductionCertificatePasswordPath.'
    }
    if ([string]::IsNullOrWhiteSpace($ProductionIdentityName)) {
        throw 'Production identity name cannot be empty.'
    }

    $certificatePath = [System.IO.Path]::GetFullPath($ProductionCertificatePath)
    $certificatePassword = [System.IO.File]::ReadAllText(
        [System.IO.Path]::GetFullPath($ProductionCertificatePasswordPath)).Trim()
    if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
        throw 'Production certificate password file is empty.'
    }

    $productionCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath,
        $certificatePassword)
    try {
        if (!$productionCertificate.HasPrivateKey) {
            throw 'Production signing certificate does not contain a private key.'
        }
        $expectedPublisher = $productionCertificate.Subject
    }
    finally {
        $productionCertificate.Dispose()
    }
    $publisherDisplayName = $ProductionPublisherDisplayName
}
else {
    if ([string]::IsNullOrWhiteSpace($DevelopmentIdentityName)) {
        throw 'Development identity name cannot be empty.'
    }
    $passwordPath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.password.txt'
    $certificatePath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.pfx'
    $publicCertificatePath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.cer'
    if (!(Test-Path -LiteralPath $passwordPath)) {
        $randomBytes = [byte[]]::new(32)
        $randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $randomNumberGenerator.GetBytes($randomBytes)
        }
        finally {
            $randomNumberGenerator.Dispose()
        }
        [System.IO.File]::WriteAllText(
            $passwordPath,
            [Convert]::ToBase64String($randomBytes),
            [Text.UTF8Encoding]::new($false))
    }
    $certificatePassword = [System.IO.File]::ReadAllText($passwordPath).Trim()
}

$manifestTemplate = Join-Path $repoRoot 'packaging\external-location\Package.appxmanifest.template'
$manifestText = [System.IO.File]::ReadAllText(
    $manifestTemplate,
    [Text.UTF8Encoding]::new($false))
$identityName = if ($SigningMode -eq 'Production') { $ProductionIdentityName } else { $DevelopmentIdentityName }
$manifestText = Set-ManifestToken $manifestText '__IDENTITY_NAME__' $identityName
$manifestText = Set-ManifestToken $manifestText '__PUBLISHER__' $expectedPublisher
$manifestText = Set-ManifestToken $manifestText '__VERSION__' $Version
$manifestText = Set-ManifestToken $manifestText '__PUBLISHER_DISPLAY_NAME__' $publisherDisplayName
[System.IO.File]::WriteAllText($manifestPath, $manifestText, [Text.UTF8Encoding]::new($false))

if ($SigningMode -eq 'Development') {
    $passwordPath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.password.txt'
    $certificatePath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.pfx'
    $publicCertificatePath = Join-Path $privateRoot 'LuoTianyiPet.ExternalLocation.cer'
    if (!(Test-Path -LiteralPath $certificatePath)) {
        & $winapp cert generate `
            --manifest $manifestPath `
            --publisher $expectedPublisher `
            --output $certificatePath `
            --password $certificatePassword `
            --valid-days 365 `
            --export-cer `
            --if-exists Error `
            --quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'Development certificate generation failed.'
        }
    }
}

if (!(Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
    throw "Signing certificate was not found: $certificatePath"
}

$payloadExecutable = Join-Path $payloadRoot 'LuoTianyiPet.exe'
& $winapp embed-identity $payloadExecutable --manifest $manifestPath --quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Embedding sparse package identity into the executable failed.'
}
& $winapp sign $payloadExecutable $certificatePath --password $certificatePassword --quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Signing the external-location executable failed.'
}

Assert-NoRawSourceDirectories $payloadRoot

Assert-ArtifactPath $identityPath
if (Test-Path -LiteralPath $identityPath) {
    Remove-Item -LiteralPath $identityPath -Force
}
& $winapp package $manifestPath `
    --output $identityPath `
    --cert $certificatePath `
    --cert-password $certificatePassword `
    --quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Sparse identity package creation failed.'
}

$releaseCertificatePath = $null
if ($SigningMode -eq 'Development') {
    $releaseCertificatePath = Join-Path $bundleRoot 'LuoTianyiPet.Dev.cer'
    Copy-Item -LiteralPath $publicCertificatePath -Destination $releaseCertificatePath -Force
}

Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\安装洛天依桌宠.cmd') `
    -Destination $bundleRoot -Force
Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\install-luotianyi-pet.ps1') `
    -Destination $bundleRoot -Force
Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\安装说明.txt') `
    -Destination $bundleRoot -Force
Copy-Item -Path (Join-Path $repoRoot 'packaging\external-location\LUOTIANYI_PET_EXTERNAL_LOCATION_BUNDLE.marker') `
    -Destination $bundleRoot -Force

$hashLines = @()
$identityHash = (Get-FileHash -LiteralPath $identityPath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashLines += "$identityHash  $([System.IO.Path]::GetFileName($identityPath))"
if ($null -ne $releaseCertificatePath) {
    $publicHash = (Get-FileHash -LiteralPath $releaseCertificatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashLines += "$publicHash  $([System.IO.Path]::GetFileName($releaseCertificatePath))"
}
[System.IO.File]::WriteAllLines(
    (Join-Path $bundleRoot 'SHA256SUMS.txt'),
    $hashLines,
    [Text.UTF8Encoding]::new($false))

Assert-NoRawSourceDirectories $bundleRoot
Assert-ArtifactPath $bundlePath
if (Test-Path -LiteralPath $bundlePath) {
    Remove-Item -LiteralPath $bundlePath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $bundleRoot,
    $bundlePath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

$bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    "$bundlePath.sha256.txt",
    "$bundleHash  $([System.IO.Path]::GetFileName($bundlePath))`r`n",
    [Text.UTF8Encoding]::new($false))

# Publish uses the x64 runtime graph for the distributable payload. Restore the
# repository's normal x86 development graph before returning so a subsequent
# solution build is not affected by the packaging command's intermediate state.
& $dotnet restore (Join-Path $repoRoot 'LuoTianyiPet.sln') -p:RuntimeIdentifier=win-x86
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to restore the repository development build assets after packaging.'
}

Write-Host "Built external-location installer bundle: $bundlePath"
Write-Host 'The bundle keeps the application files in the selected external directory and contains no private signing key.'

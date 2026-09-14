[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$InstallDirectory,
    [switch]$SkipDesktopShortcut,
    [switch]$SkipLaunch,
    [switch]$TrustCertificateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bundleRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$markerPath = Join-Path $bundleRoot 'LUOTIANYI_PET_EXTERNAL_LOCATION_BUNDLE.marker'
$identityPath = Join-Path $bundleRoot 'LuoTianyiPet.Identity.msix'
$certificatePath = Join-Path $bundleRoot 'LuoTianyiPet.Dev.cer'
$hashPath = Join-Path $bundleRoot 'SHA256SUMS.txt'
$payloadPath = Join-Path $bundleRoot 'payload'
$payloadExecutablePath = Join-Path $payloadPath 'LuoTianyiPet.exe'

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Test-IsPathInside([string]$Path, [string]$Parent) {
    $fullPath = (Get-FullPath $Path).TrimEnd('\') + '\'
    $fullParent = (Get-FullPath $Parent).TrimEnd('\') + '\'
    return $fullPath.StartsWith($fullParent, [StringComparison]::OrdinalIgnoreCase)
}

function Read-PackageManifest([string]$Path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry('AppxManifest.xml')
        if ($null -eq $entry) {
            throw '身份包缺少 AppxManifest.xml。'
        }
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try {
            [xml]$document = $reader.ReadToEnd()
            return $document
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-IdentityName([xml]$Manifest) {
    $namespace = [System.Xml.XmlNamespaceManager]::new($Manifest.NameTable)
    $namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $Manifest.SelectSingleNode('/f:Package/f:Identity', $namespace)
    $identityName = if ($null -eq $identity) { $null } else { $identity.GetAttribute('Name') }
    if ($null -eq $identity -or [string]::IsNullOrWhiteSpace($identityName)) {
        throw '身份包清单缺少有效的 Identity.Name。'
    }
    return $identityName
}

function Get-SelectedInstallDirectory {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = [System.Windows.Forms.FolderBrowserDialog]::new()
    try {
        $dialog.Description = '选择洛天依桌宠的安装目录'
        $dialog.ShowNewFolderButton = $true
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            throw '用户取消了安装目录选择。'
        }
        return Get-FullPath $dialog.SelectedPath
    }
    finally {
        $dialog.Dispose()
    }
}

function Stop-InstalledProcess([string]$ExecutablePath) {
    $fullExecutablePath = Get-FullPath $ExecutablePath
    $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'LuoTianyiPet.exe'" |
        Where-Object {
            $_.ExecutablePath -and
            (Get-FullPath $_.ExecutablePath).Equals(
                $fullExecutablePath,
                [StringComparison]::OrdinalIgnoreCase)
        })
    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
    }
}

function Ensure-TestCertificateTrust([string]$Path) {
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($Path)
    try {
        $thumbprint = $certificate.Thumbprint
        $trusted = Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue |
            Where-Object Thumbprint -CEQ $thumbprint |
            Select-Object -First 1
        if ($null -eq $trusted) {
            $principal = [Security.Principal.WindowsPrincipal]::new(
                [Security.Principal.WindowsIdentity]::GetCurrent())
            $isAdministrator = $principal.IsInRole(
                [Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($isAdministrator) {
                Import-Certificate -FilePath $Path -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
            }
            else {
                $arguments = @(
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-File', ('"{0}"' -f $PSCommandPath),
                    '-TrustCertificateOnly') -join ' '
                $process = Start-Process -FilePath 'powershell.exe' `
                    -Verb RunAs `
                    -ArgumentList $arguments `
                    -Wait `
                    -PassThru
                if ($process.ExitCode -ne 0) {
                    throw "Windows 没有完成测试证书信任，退出代码为 $($process.ExitCode)。"
                }
            }
        }
        if ($certificate.NotAfter -le [DateTime]::Now) {
            throw '安装包测试证书已经过期，请获取新版安装包。'
        }
        return $thumbprint
    }
    finally {
        $certificate.Dispose()
    }
}

if ($TrustCertificateOnly) {
    if (!(Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
        throw '测试证书文件不存在。'
    }
    $null = Ensure-TestCertificateTrust $certificatePath
    exit 0
}

if (!(Test-Path -LiteralPath $markerPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $identityPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $payloadPath -PathType Container) -or
    !(Test-Path -LiteralPath $payloadExecutablePath -PathType Leaf)) {
    throw '安装包不完整，请重新解压完整安装包后再试。'
}

$manifest = Read-PackageManifest $identityPath
$identityName = Get-IdentityName $manifest
$packageFileName = [System.IO.Path]::GetFileName($identityPath)

if (!(Test-Path -LiteralPath $hashPath -PathType Leaf)) {
    throw '安装包缺少 SHA256SUMS.txt。'
}
$expectedHashes = @{}
foreach ($line in [System.IO.File]::ReadAllLines($hashPath)) {
    if ($line -match '^([0-9a-fA-F]{64})\s{2}(.+)$') {
        $expectedHashes[$matches[2]] = $matches[1].ToLowerInvariant()
    }
}
if (!$expectedHashes.ContainsKey($packageFileName)) {
    throw "校验文件中没有 $packageFileName。"
}
$actualHash = (Get-FileHash -LiteralPath $identityPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -cne $expectedHashes[$packageFileName]) {
    throw '身份包 SHA-256 不匹配，安装已停止。'
}

if (Test-Path -LiteralPath $certificatePath -PathType Leaf) {
    if (!$expectedHashes.ContainsKey([System.IO.Path]::GetFileName($certificatePath))) {
        throw '校验文件中没有公开测试证书。'
    }
    $certificateHash = (Get-FileHash -LiteralPath $certificatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($certificateHash -cne $expectedHashes[[System.IO.Path]::GetFileName($certificatePath)]) {
        throw '公开测试证书 SHA-256 不匹配，安装已停止。'
    }
    $null = Ensure-TestCertificateTrust $certificatePath
}

$packageSignature = Get-AuthenticodeSignature -LiteralPath $identityPath
$payloadSignature = Get-AuthenticodeSignature -LiteralPath $payloadExecutablePath
if ($null -eq $packageSignature.SignerCertificate -or
    $null -eq $payloadSignature.SignerCertificate) {
    throw '身份包或桌宠 EXE 缺少签名，安装已停止。'
}
if ($packageSignature.SignerCertificate.Thumbprint -cne
    $payloadSignature.SignerCertificate.Thumbprint) {
    throw '身份包与桌宠 EXE 的签名者不一致，安装已停止。'
}

$targetRoot = if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    Get-SelectedInstallDirectory
}
else {
    Get-FullPath $InstallDirectory
}
if ($targetRoot.TrimEnd('\').Equals(
        [System.IO.Path]::GetPathRoot($targetRoot).TrimEnd('\'),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw '不能把磁盘根目录直接作为安装目录，请先创建一个专用文件夹。'
}
if ((Test-IsPathInside $targetRoot $bundleRoot) -or
    (Get-FullPath $targetRoot).Equals((Get-FullPath $bundleRoot), [StringComparison]::OrdinalIgnoreCase)) {
    throw '安装目录不能位于当前安装包解压目录内。'
}

$targetExecutable = Join-Path $targetRoot 'LuoTianyiPet.exe'
$targetMarker = Join-Path $targetRoot 'LUOTIANYI_PET_INSTALLED.marker'
if (!(Test-Path -LiteralPath $targetRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
}
$existingEntries = @()
if (Test-Path -LiteralPath $targetRoot -PathType Container) {
    $existingEntries = @(Get-ChildItem -LiteralPath $targetRoot -Force)
}
if ($existingEntries.Count -gt 0 -and !(Test-Path -LiteralPath $targetMarker -PathType Leaf)) {
    throw '安装目录不是空目录，也不是已有的洛天依桌宠安装目录。请另选目录。'
}

Stop-InstalledProcess $targetExecutable

$existingPackages = @(Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue)
foreach ($existingPackage in $existingPackages) {
    Remove-AppxPackage -Package $existingPackage.PackageFullName -ErrorAction Stop
}

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
Copy-Item -Path (Join-Path $payloadPath '*') -Destination $targetRoot -Recurse -Force
[System.IO.File]::WriteAllText(
    (Join-Path $targetRoot '.LuoTianyiPet.IdentityName.txt'),
    $identityName,
    [Text.UTF8Encoding]::new($false))

Add-AppxPackage -Path $identityPath -ExternalLocation $targetRoot -ErrorAction Stop

$desktopDirectory = [Environment]::GetFolderPath('DesktopDirectory')
if (!$SkipDesktopShortcut -and
    ![string]::IsNullOrWhiteSpace($desktopDirectory) -and
    (Test-Path -LiteralPath $desktopDirectory -PathType Container)) {
    $shortcutPath = Join-Path $desktopDirectory '洛天依桌宠.lnk'
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $targetExecutable
        # An existing shortcut may still carry shell:AppsFolder arguments from
        # an older MSIX install. External-location installs launch the selected
        # executable directly, so clear stale arguments explicitly.
        $shortcut.Arguments = ''
        $shortcut.WorkingDirectory = $targetRoot
        $shortcut.IconLocation = "$targetExecutable,0"
        $shortcut.Description = '启动洛天依桌宠'
        $shortcut.Save()
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
    }
    finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
}

if (!$Quiet) {
    $successMessage = [string]::Format(
        '洛天依桌宠已安装到：{0}{1}{0}{0}首次使用 QQ / 微信提醒时，请在桌宠“设置 → 通知”中点击“授权访问”。',
        [Environment]::NewLine,
        $targetRoot)
    [System.Windows.Forms.MessageBox]::Show(
        $successMessage,
        '洛天依桌宠安装完成',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
if (!$SkipLaunch) {
    Start-Process -FilePath $targetExecutable -WorkingDirectory $targetRoot
}

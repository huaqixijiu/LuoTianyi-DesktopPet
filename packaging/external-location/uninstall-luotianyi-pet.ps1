[CmdletBinding()]
param(
    [ValidateSet('Prompt', 'Keep', 'Delete')]
    [string]$DataPolicy = 'Prompt',
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$executablePath = Join-Path $installRoot 'LuoTianyiPet.exe'
$markerPath = Join-Path $installRoot 'LUOTIANYI_PET_INSTALLED.marker'
$identityFilePath = Join-Path $installRoot '.LuoTianyiPet.IdentityName.txt'
$dataPath = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'LuoTianyiPet'))

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

if (!(Test-Path -LiteralPath $markerPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    !(Test-Path -LiteralPath $identityFilePath -PathType Leaf)) {
    throw '这不是完整的洛天依桌宠安装目录，卸载已停止。'
}

$identityName = ([System.IO.File]::ReadAllText($identityFilePath)).Trim()
if ([string]::IsNullOrWhiteSpace($identityName)) {
    throw '安装目录缺少有效的包身份名称，卸载已停止。'
}

Add-Type -AssemblyName System.Windows.Forms
$choiceMessage = [string]::Format(
    '是否保留用户数据？{0}{0}选择“是”：删除程序但保留 {1}{0}选择“否”：删除程序并删除该用户数据目录{0}选择“取消”：终止卸载。',
    [Environment]::NewLine,
    $dataPath)
$choice = if ($DataPolicy -eq 'Prompt') {
    [System.Windows.Forms.MessageBox]::Show(
        $choiceMessage,
        '卸载洛天依桌宠',
        [System.Windows.Forms.MessageBoxButtons]::YesNoCancel,
        [System.Windows.Forms.MessageBoxIcon]::Question,
        [System.Windows.Forms.MessageBoxDefaultButton]::Button1)
}
elseif ($DataPolicy -eq 'Keep') {
    [System.Windows.Forms.DialogResult]::Yes
}
else {
    [System.Windows.Forms.DialogResult]::No
}
if ($choice -eq [System.Windows.Forms.DialogResult]::Cancel) {
    exit 2
}
$removeUserData = $choice -eq [System.Windows.Forms.DialogResult]::No

$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'LuoTianyiPet.exe'" |
    Where-Object {
        $_.ExecutablePath -and
        (Get-FullPath $_.ExecutablePath).Equals(
            (Get-FullPath $executablePath),
            [StringComparison]::OrdinalIgnoreCase)
    })
foreach ($process in $processes) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
}

$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -LiteralPath $runKeyPath -Name 'LuoTianyiPet' -ErrorAction SilentlyContinue

$registeredPackages = @(Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue)
foreach ($registeredPackage in $registeredPackages) {
    Remove-AppxPackage -Package $registeredPackage.PackageFullName -ErrorAction Stop
}

$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '洛天依桌宠.lnk'
if (Test-Path -LiteralPath $desktopShortcutPath -PathType Leaf) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($desktopShortcutPath)
        $shortcutTarget = $shortcut.TargetPath
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
    }
    finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
    if (![string]::IsNullOrWhiteSpace($shortcutTarget) -and
        (Get-FullPath $shortcutTarget).Equals(
            (Get-FullPath $executablePath),
            [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $desktopShortcutPath -Force
    }
}

if ($removeUserData -and (Test-Path -LiteralPath $dataPath)) {
    Remove-Item -LiteralPath $dataPath -Recurse -Force
}

$cleanupScriptPath = Join-Path $env:TEMP ("LuoTianyiPet-uninstall-{0}.ps1" -f [Guid]::NewGuid().ToString('N'))
$cleanupScript = @'
param(
    [Parameter(Mandatory = $true)] [int]$ParentProcessId,
    [Parameter(Mandatory = $true)] [string]$InstallRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'SilentlyContinue'
while (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue) {
    Start-Sleep -Milliseconds 250
}
Start-Sleep -Milliseconds 250
if (Test-Path -LiteralPath $InstallRoot) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
'@
[System.IO.File]::WriteAllText($cleanupScriptPath, $cleanupScript, [Text.UTF8Encoding]::new($false))
$cleanupArguments = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', ('"{0}"' -f $cleanupScriptPath),
    '-ParentProcessId', "$PID",
    '-InstallRoot', ('"{0}"' -f $installRoot)) -join ' '
Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList $cleanupArguments

$dataMessage = if ($removeUserData) { '用户数据已选择删除。' } else { "用户数据已保留在：$dataPath" }
if (!$Quiet) {
    [System.Windows.Forms.MessageBox]::Show(
        "洛天依桌宠已卸载。`n$dataMessage",
        '卸载完成',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}

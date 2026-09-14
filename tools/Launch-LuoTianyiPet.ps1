[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'

function Show-LauncherMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [string]$Title = 'LuoTianyi Pet'
    )

    try {
        $popupShell = New-Object -ComObject WScript.Shell
        $popupShell.Popup($Message, 0, $Title, 16) | Out-Null
    }
    catch {
        Write-Error $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $projectRoot 'src\LuoTianyiPet.App\LuoTianyiPet.App.csproj'
$outputDirectory = Join-Path $projectRoot "src\LuoTianyiPet.App\bin\$Configuration\net48"
$appExecutable = Join-Path $outputDirectory 'LuoTianyiPet.exe'

if (-not (Test-Path -LiteralPath $appProject -PathType Leaf)) {
    Show-LauncherMessage "Project file was not found:`n$appProject"
    exit 1
}

$dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    Show-LauncherMessage 'dotnet.exe was not found. Install the .NET SDK before launching the pet.'
    exit 1
}

Push-Location $projectRoot
try {
    & $dotnetCommand.Source build $appProject `
        --configuration $Configuration `
        --framework net48 `
        --no-restore `
        --nologo `
        --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Show-LauncherMessage "The current source failed to build (exit code $LASTEXITCODE). The pet was not started, so an old executable cannot be launched.`n`nOpen a terminal in the project directory to inspect the build error."
        exit $LASTEXITCODE
    }

    if ($BuildOnly) {
        exit 0
    }

    if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
        Show-LauncherMessage "The build completed but the output executable was not found:`n$appExecutable"
        exit 1
    }

    Start-Process -FilePath $appExecutable -WorkingDirectory $outputDirectory
}
catch {
    Show-LauncherMessage "The pet could not be started:`n$($_.Exception.Message)"
    exit 1
}
finally {
    Pop-Location
}

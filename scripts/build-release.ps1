[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'PrintShopImageConverter.sln'
$projectPath = Join-Path $repositoryRoot 'src\PrintShopImageConverter\PrintShopImageConverter.csproj'
$nugetConfig = Join-Path $repositoryRoot 'NuGet.Config'
$installerScript = Join-Path $repositoryRoot 'installer\PrintShopImageConverter.iss'

dotnet restore $solutionPath --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }

dotnet test $solutionPath --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }

dotnet restore $projectPath --runtime win-x64 --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { throw 'Windows runtime restore failed.' }

dotnet publish $projectPath --configuration Release --no-restore --property:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

if ($SkipInstaller) {
    Write-Host 'Self-contained publish completed; installer compilation was skipped.'
    exit 0
}

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    throw 'Inno Setup 6 was not found. Install it or rerun with -SkipInstaller.'
}

& $iscc.Source $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

Write-Host 'Release build completed successfully.'

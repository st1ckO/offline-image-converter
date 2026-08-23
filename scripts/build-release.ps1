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

$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$isccPath = $isccCommand?.Source
if (-not $isccPath) {
    $knownLocations = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    )
    $isccPath = $knownLocations | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $isccPath) {
    throw 'Inno Setup 6 was not found. Install it or rerun with -SkipInstaller.'
}

& $isccPath $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

Write-Host 'Release build completed successfully.'

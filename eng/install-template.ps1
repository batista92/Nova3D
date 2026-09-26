[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$packages = Join-Path $repository 'artifacts\packages'
$templateProject = Join-Path $repository 'templates\Nova3D.Templates\Nova3D.Templates.csproj'
$novaProject = Join-Path $repository 'Nova3D\Nova3D.csproj'
$physicsProject = Join-Path $repository 'Nova3D.Physics.Bepu\Nova3D.Physics.Bepu.csproj'
$uiProject = Join-Path $repository 'Nova3D.UI.Gum\Nova3D.UI.Gum.csproj'

New-Item -ItemType Directory -Force -Path $packages | Out-Null
dotnet pack $novaProject -c Release -o $packages
dotnet pack $physicsProject -c Release -o $packages
dotnet pack $uiProject -c Release -o $packages
dotnet pack $templateProject -c Release -o $packages

$existing = (dotnet nuget list source --format short) -join [Environment]::NewLine
if ($existing -notmatch [regex]::Escape($packages)) {
    dotnet nuget add source $packages --name Nova3D-Local
}

$templatePackage = Join-Path $packages 'Nova3D.Templates.0.1.0.nupkg'
# A first-time installation has nothing to uninstall. PowerShell may promote
# the CLI's stderr message to a terminating error, so this best-effort cleanup
# must not abort the installer.
try {
    dotnet new uninstall Nova3D.Templates 2>$null | Out-Null
}
catch {
    # Template was not installed yet.
}
dotnet new install $templatePackage

Write-Host ''
Write-Host 'Nova3D template installed.' -ForegroundColor Green
Write-Host '  dotnet new nova3d -n MyGame'
Write-Host '  cd MyGame'
Write-Host '  dotnet run'
Write-Host ''
Write-Host 'Optional physics:'
Write-Host '  dotnet new nova3d -n MyPhysicsGame --physics'
Write-Host ''
Write-Host 'Optional UI:'
Write-Host '  dotnet new nova3d -n MyUiGame --ui'
Write-Host ''
Write-Host 'Physics + UI:'
Write-Host '  dotnet new nova3d -n MyFullGame --physics --ui'

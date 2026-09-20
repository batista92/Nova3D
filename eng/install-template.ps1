[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$packages = Join-Path $repository 'artifacts\packages'
$templateProject = Join-Path $repository 'templates\Nova3D.Templates\Nova3D.Templates.csproj'
$novaProject = Join-Path $repository 'Nova3D\Nova3D.csproj'

New-Item -ItemType Directory -Force -Path $packages | Out-Null
dotnet pack $novaProject -c Release -o $packages
dotnet pack $templateProject -c Release -o $packages

$existing = dotnet nuget list source --format short
if ($existing -notmatch [regex]::Escape($packages)) {
    dotnet nuget add source $packages --name Nova3D-Local
}

$templatePackage = Join-Path $packages 'Nova3D.Templates.0.1.0.nupkg'
dotnet new uninstall Nova3D.Templates 2>$null | Out-Null
dotnet new install $templatePackage

Write-Host ''
Write-Host 'Nova3D template installed.' -ForegroundColor Green
Write-Host '  dotnet new nova3d -n MyGame'
Write-Host '  cd MyGame'
Write-Host '  dotnet run'

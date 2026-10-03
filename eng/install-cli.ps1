[CmdletBinding()]
param(
    [string]$ToolPath
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$packages = Join-Path $repository 'artifacts\packages'
$coreProject = Join-Path $repository 'Nova3D\Nova3D.csproj'
$cliProject = Join-Path $repository 'Nova3D.Cli\Nova3D.Cli.csproj'
if ([string]::IsNullOrWhiteSpace($ToolPath)) {
    $ToolPath = Join-Path $repository 'artifacts\tools\nova3d-cli'
}
$ToolPath = [IO.Path]::GetFullPath($ToolPath)

New-Item -ItemType Directory -Force -Path $packages, $ToolPath | Out-Null
dotnet pack $coreProject -c Release -o $packages
dotnet pack $cliProject -c Release -o $packages

[xml]$project = Get-Content -Raw -LiteralPath $cliProject
$version = $project.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Nova3D.Cli package version was not found.'
}

$command = Join-Path $ToolPath $(if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    'nova3d.exe'
} else {
    'nova3d'
})

if (Test-Path -LiteralPath $command) {
    dotnet tool uninstall --tool-path $ToolPath Nova3D.Cli
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool uninstall failed with exit code $LASTEXITCODE."
    }
}

$arguments = @(
    'tool', 'install',
    '--tool-path', $ToolPath,
    '--add-source', $packages,
    '--ignore-failed-sources',
    '--version', $version,
    'Nova3D.Cli'
)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "dotnet tool failed with exit code $LASTEXITCODE." }

Write-Host ''
Write-Host 'Nova3D CLI installed locally.' -ForegroundColor Green
Write-Host "  $command --help"

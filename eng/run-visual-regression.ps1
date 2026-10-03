[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$BaselineRoot
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repository 'artifacts\visual-captures\suite'
}
if ([string]::IsNullOrWhiteSpace($BaselineRoot)) {
    $BaselineRoot = Join-Path $repository 'Benchmarks\VisualBaselines'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$BaselineRoot = [IO.Path]::GetFullPath($BaselineRoot)
$benchmark = Join-Path $repository 'Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj'
$cli = Join-Path $repository 'Nova3D.Cli\Nova3D.Cli.csproj'
$diffRoot = Join-Path $repository 'artifacts\visual-diffs\suite'

& dotnet build $benchmark -c Release
if ($LASTEXITCODE -ne 0) { throw 'Visual benchmark build failed.' }
& dotnet build $cli -c Release
if ($LASTEXITCODE -ne 0) { throw 'Nova3D CLI build failed.' }

$scenes = @(
    @{ Argument = 'City'; Name = 'city-benchmark' },
    @{ Argument = 'Pbr'; Name = 'pbr-material-csm' },
    @{ Argument = 'Gltf'; Name = 'gltf-static-animated' }
)

foreach ($scene in $scenes) {
    $captureDirectory = Join-Path $OutputRoot $scene.Name
    & dotnet run --project $benchmark -c Release --no-build -- `
        --capture $captureDirectory --scene $scene.Argument
    if ($LASTEXITCODE -ne 0) {
        throw "Visual capture failed for $($scene.Name)."
    }

    $capture = Join-Path $captureDirectory ($scene.Name + '.png')
    $diff = Join-Path $diffRoot ($scene.Name + '.png')
    & dotnet run --project $cli -c Release --no-build -- visual compare `
        $capture --baseline-root $BaselineRoot --diff $diff
    if ($LASTEXITCODE -ne 0) {
        throw "Visual comparison failed for $($scene.Name). Diff: $diff"
    }
}

Write-Host "VISUAL SUITE PASS | scenes $($scenes.Count) | captures $OutputRoot | diffs $diffRoot"

[CmdletBinding()]
param(
    [string]$ReportPath,
    [string]$BudgetPath,
    [switch]$SkipPhysics
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $repository 'artifacts\performance\city-benchmark.json'
}
if ([string]::IsNullOrWhiteSpace($BudgetPath)) {
    $BudgetPath = Join-Path $repository 'Benchmarks\PerformanceBudgets\city-benchmark.json'
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$BudgetPath = [IO.Path]::GetFullPath($BudgetPath)
$benchmark = Join-Path $repository 'Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj'
$physics = Join-Path $repository 'Benchmarks\PhysicsBenchmark\PhysicsBenchmark.csproj'
$cli = Join-Path $repository 'Nova3D.Cli\Nova3D.Cli.csproj'

if (-not $SkipPhysics) {
    & dotnet run --project $physics -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Physics regression failed.' }
}

& dotnet build $benchmark -c Release
if ($LASTEXITCODE -ne 0) { throw 'Performance benchmark build failed.' }
& dotnet build $cli -c Release
if ($LASTEXITCODE -ne 0) { throw 'Nova3D CLI build failed.' }

& dotnet run --project $benchmark -c Release --no-build -- --performance-report $ReportPath
if ($LASTEXITCODE -ne 0) { throw 'Performance capture failed.' }

& dotnet run --project $cli -c Release --no-build -- performance check $ReportPath --budget $BudgetPath
if ($LASTEXITCODE -ne 0) { throw "Performance budget failed. Report: $ReportPath" }

Write-Host "PERFORMANCE SUITE PASS | report $ReportPath | budget $BudgetPath"

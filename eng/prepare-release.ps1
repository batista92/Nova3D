[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$packageDirectory = Join-Path $repository 'artifacts\packages'
$version = '0.3.0'
$packageIds = @('Nova3D', 'Nova3D.Physics.Bepu', 'Nova3D.UI.Gum',
    'Nova3D.Cli', 'Nova3D.Templates')

function Assert-CleanSource {
    $root = (& git -C $repository rev-parse --show-toplevel 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or
        -not [string]::Equals([IO.Path]::GetFullPath($root),
            [IO.Path]::GetFullPath($repository),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Run release preparation from the Nova3D Git checkout.'
    }
    $changes = @(& git -C $repository -c core.excludesFile= status `
        --porcelain=v1 --untracked-files=all 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Git working tree.' }
    if ($changes.Count -gt 0) {
        throw 'Release preparation requires a clean committed working tree. Review and commit or resolve the listed changes first.'
    }
    return (& git -C $repository rev-parse HEAD).Trim()
}

function Assert-PackageHashes {
    $manifestPath = Join-Path $packageDirectory 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Missing package manifest: $manifestPath"
    }
    $lines = @(Get-Content -LiteralPath $manifestPath)
    if ($lines.Count -ne $packageIds.Count) {
        throw "Expected $($packageIds.Count) package hashes; found $($lines.Count)."
    }
    for ($index = 0; $index -lt $packageIds.Count; $index++) {
        $expectedName = '{0}.{1}.nupkg' -f $packageIds[$index], $version
        $expectedPattern = '^([0-9a-f]{64})  ' + [regex]::Escape($expectedName) + '$'
        if ($lines[$index] -cnotmatch $expectedPattern) {
            throw "Invalid or out-of-order package hash for $expectedName."
        }
        $expectedHash = $Matches[1]
        $actualHash = (Get-FileHash -LiteralPath (
            Join-Path $packageDirectory $expectedName) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $expectedHash) {
            throw "Package hash mismatch: $expectedName"
        }
    }
    return $manifestPath
}

$sourceCommit = Assert-CleanSource
if (@(& git -C $repository tag --list "v$version").Count -gt 0) {
    throw "Tag v$version already exists; inspect it before preparing another release."
}

[xml]$coreProject = Get-Content -LiteralPath (Join-Path $repository 'Nova3D\Nova3D.csproj')
if ([string]$coreProject.Project.PropertyGroup.Version -ne $version) {
    throw "Core package version must be $version."
}

& (Join-Path $repository 'eng\validate.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Repository validation failed.' }
& (Join-Path $repository 'eng\run-visual-regression.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Visual regression failed.' }
& (Join-Path $repository 'eng\run-performance-regression.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Performance regression failed.' }

$manifestPath = Assert-PackageHashes
if ((Assert-CleanSource) -cne $sourceCommit) {
    throw 'Source commit changed during release preparation.'
}

$provenance = [ordered]@{
    version = $version
    sourceCommit = $sourceCommit
    preparedAtUtc = [DateTime]::UtcNow.ToString('O')
    sha256Manifest = [IO.Path]::GetFileName($manifestPath)
    visualSuite = 'PASS (3 scenes)'
    performanceSuite = 'PASS (CityBenchmark and physics)'
}
$provenancePath = Join-Path $packageDirectory 'RELEASE_PROVENANCE.json'
[IO.File]::WriteAllText($provenancePath,
    ($provenance | ConvertTo-Json -Depth 3) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))
Write-Host "RELEASE CANDIDATE PASS | v$version | commit $sourceCommit"
Write-Host "Packages, hashes and provenance: $packageDirectory"
Write-Host 'No tag was created and nothing was published.'

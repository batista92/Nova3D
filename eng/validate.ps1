[CmdletBinding()]
param(
    [string]$GameProject,
    [switch]$NoRestore,
    [switch]$SkipPhysicsBenchmark,
    [switch]$SkipTemplateSmoke,
    [switch]$KeepTemporary
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$repository = Split-Path -Parent $PSScriptRoot
$totalStopwatch = [Diagnostics.Stopwatch]::StartNew()
$script:stepCount = 0
$temporaryRoot = $null
$succeeded = $false

function Invoke-DotNetStep {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [switch]$ShowResult
    )

    $script:stepCount++
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host ("RUN  {0}" -f $Name)
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Native stderr belongs to the captured diagnostic output. PowerShell
        # must not promote it to a terminating ErrorRecord before we inspect
        # the process exit code.
        $ErrorActionPreference = 'Continue'
        $output = @(& dotnet @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $stopwatch.Stop()

    if ($exitCode -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        throw "'$Name' failed with exit code $exitCode."
    }

    if ($ShowResult) {
        $result = $output | Where-Object { $_.ToString().Trim().Length -gt 0 } |
            Select-Object -Last 1
        if ($null -ne $result) { Write-Host ("     {0}" -f $result) }
    }
    Write-Host ("PASS {0} ({1:N2}s)" -f $Name, $stopwatch.Elapsed.TotalSeconds)
}

function Get-RestoreArguments {
    if ($NoRestore) { return @('--no-restore') }
    return @()
}

function Test-GameProject {
    param([Parameter(Mandatory)] [string]$Project)

    $resolvedProject = (Resolve-Path -LiteralPath $Project).Path
    if ([IO.Path]::GetExtension($resolvedProject) -ne '.csproj') {
        throw "GameProject must point to a .csproj file: $resolvedProject"
    }

    if (-not $NoRestore) {
        Invoke-DotNetStep 'game restore' @('restore', $resolvedProject)
    }
    Invoke-DotNetStep 'game Release build + MGCB' (
        @('build', $resolvedProject, '-c', 'Release') + (Get-RestoreArguments))
}

function Test-TemplateVariants {
    param(
        [Parameter(Mandatory)] [string]$TemplatePackage,
        [Parameter(Mandatory)] [string]$PackageSource
    )

    $script:temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
        'nova3d-validation-' + [Guid]::NewGuid().ToString('N'))
    $hive = Join-Path $temporaryRoot 'template-hive'
    $games = Join-Path $temporaryRoot 'games'
    New-Item -ItemType Directory -Force -Path $hive, $games | Out-Null

    Invoke-DotNetStep 'isolated template install' @(
        'new', 'install', $TemplatePackage, '--debug:custom-hive', $hive)

    $variants = @(
        @{ Name = 'PlainGame'; Options = @() },
        @{ Name = 'PhysicsGame'; Options = @('--physics') },
        @{ Name = 'UiGame'; Options = @('--ui') },
        @{ Name = 'FullGame'; Options = @('--physics', '--ui') }
    )

    foreach ($variant in $variants) {
        $output = Join-Path $games $variant.Name
        $newArguments = @(
            'new', 'nova3d', '-n', $variant.Name, '-o', $output,
            '--debug:custom-hive', $hive
        ) + $variant.Options
        Invoke-DotNetStep ("generate {0}" -f $variant.Name) $newArguments

        $project = Join-Path $output ($variant.Name + '.csproj')
        $skill = Join-Path $output '.agents\skills\nova3d-game-development\SKILL.md'
        if (-not (Test-Path -LiteralPath $skill)) {
            throw "Generated project is missing the Nova3D agent skill: $skill"
        }
        $gitIgnore = Join-Path $output '.gitignore'
        if (-not (Test-Path -LiteralPath $gitIgnore)) {
            throw "Generated project is missing repository ignore rules: $gitIgnore"
        }
        Invoke-DotNetStep ("restore {0}" -f $variant.Name) @(
            'restore', $project,
            '--source', $PackageSource,
            '--source', 'https://api.nuget.org/v3/index.json')
        Invoke-DotNetStep ("build {0}" -f $variant.Name) @(
            'build', $project, '-c', 'Release', '--no-restore')
    }
}

try {
    if (-not [string]::IsNullOrWhiteSpace($GameProject)) {
        Test-GameProject $GameProject
        $scope = 'game'
    }
    else {
        $scope = 'repository'
        $rootProject = Join-Path $repository 'CityBuilder.csproj'
        $physicsBenchmark = Join-Path $repository 'Benchmarks\PhysicsBenchmark\PhysicsBenchmark.csproj'
        $packages = Join-Path $repository 'artifacts\packages'
        $sampleProjects = @(
            (Join-Path $repository 'Samples\Minimal3D\Minimal3D.csproj'),
            (Join-Path $repository 'Samples\PhysicsPlayground\PhysicsPlayground.csproj'),
            (Join-Path $repository 'Samples\GumMenus\GumMenus.csproj'),
            (Join-Path $repository 'Samples\RollingBall\RollingBall.csproj')
        )
        $projectsToPack = @(
            (Join-Path $repository 'Nova3D\Nova3D.csproj'),
            (Join-Path $repository 'Nova3D.Physics.Bepu\Nova3D.Physics.Bepu.csproj'),
            (Join-Path $repository 'Nova3D.UI.Gum\Nova3D.UI.Gum.csproj'),
            (Join-Path $repository 'templates\Nova3D.Templates\Nova3D.Templates.csproj')
        )

        New-Item -ItemType Directory -Force -Path $packages | Out-Null
        if (-not $NoRestore) {
            Invoke-DotNetStep 'repository restore' @('restore', $rootProject)
        }
        Invoke-DotNetStep 'repository Release build + MGCB' (
            @('build', $rootProject, '-c', 'Release') + (Get-RestoreArguments))

        if (-not $SkipPhysicsBenchmark) {
            Invoke-DotNetStep 'physics regression benchmark' (
                @('run', '--project', $physicsBenchmark, '-c', 'Release') +
                (Get-RestoreArguments)) -ShowResult
        }

        foreach ($project in $sampleProjects) {
            $sampleName = [IO.Path]::GetFileNameWithoutExtension($project)
            Invoke-DotNetStep ("sample {0}" -f $sampleName) (
                @('build', $project, '-c', 'Release') + (Get-RestoreArguments))
        }

        foreach ($project in $projectsToPack) {
            $packageName = [IO.Path]::GetFileNameWithoutExtension($project)
            Invoke-DotNetStep ("pack {0}" -f $packageName) (
                @('pack', $project, '-c', 'Release', '-o', $packages) +
                (Get-RestoreArguments))
        }

        if (-not $SkipTemplateSmoke) {
            $templatePackage = Get-ChildItem -LiteralPath $packages -Filter 'Nova3D.Templates.*.nupkg' |
                Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            if ($null -eq $templatePackage) {
                throw 'The template package was not produced.'
            }
            Test-TemplateVariants $templatePackage.FullName $packages
        }
    }

    $succeeded = $true
    $totalStopwatch.Stop()
    Write-Host ("VALIDATION PASS | scope {0} | steps {1} | {2:N2}s" -f
        $scope, $script:stepCount, $totalStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}
catch {
    $totalStopwatch.Stop()
    Write-Host ("VALIDATION FAIL | {0}" -f $_.Exception.Message) -ForegroundColor Red
    if ($null -ne $temporaryRoot) {
        Write-Host ("Temporary evidence: {0}" -f $temporaryRoot)
    }
    exit 1
}
finally {
    if ($succeeded -and -not $KeepTemporary -and $null -ne $temporaryRoot -and
        (Test-Path -LiteralPath $temporaryRoot)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

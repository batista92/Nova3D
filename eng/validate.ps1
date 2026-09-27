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
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Get-ZipEntryText {
    param(
        [Parameter(Mandatory)] $Archive,
        [Parameter(Mandatory)] [string]$EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "Package entry is missing: $EntryName"
    }

    $stream = $entry.Open()
    $reader = [IO.StreamReader]::new($stream)
    try {
        return $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Test-PackageArchives {
    param(
        [Parameter(Mandatory)] [string]$PackageDirectory,
        [Parameter(Mandatory)] [string]$ExpectedVersion
    )

    $script:stepCount++
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host 'RUN  package archive audit'

    $specifications = @(
        @{
            Id = 'Nova3D'
            Readme = 'PACKAGE.md'
            Required = @(
                'lib/net8.0/Nova3D.dll',
                'contentFiles/any/any/Shaders/PBR.fx',
                'contentFiles/any/any/Shaders/Terrain.fx',
                'contentFiles/any/any/Shaders/Water.fx'
            )
            Libraries = @('lib/net8.0/Nova3D.dll')
            Dependencies = @{
                'MonoGame.Framework.DesktopGL' = '3.8.4.1'
            }
        },
        @{
            Id = 'Nova3D.Physics.Bepu'
            Readme = 'PACKAGE.md'
            Required = @('lib/net8.0/Nova3D.Physics.Bepu.dll')
            Libraries = @('lib/net8.0/Nova3D.Physics.Bepu.dll')
            Dependencies = @{
                'Nova3D' = $ExpectedVersion
                'BepuPhysics' = '2.4.0'
            }
        },
        @{
            Id = 'Nova3D.UI.Gum'
            Readme = 'PACKAGE.md'
            Required = @('lib/net8.0/Nova3D.UI.Gum.dll')
            Libraries = @('lib/net8.0/Nova3D.UI.Gum.dll')
            Dependencies = @{
                'Nova3D' = $ExpectedVersion
                'Gum.MonoGame' = '2026.9.2.1'
            }
        },
        @{
            Id = 'Nova3D.Templates'
            Readme = 'README.md'
            Required = @(
                'content/.gitignore',
                'content/.template.config/template.json',
                'content/.agents/skills/nova3d-game-development/SKILL.md',
                'content/Nova3DGame.csproj'
            )
            Libraries = @()
            Dependencies = @{}
        }
    )

    foreach ($specification in $specifications) {
        $packagePath = Join-Path $PackageDirectory (
            '{0}.{1}.nupkg' -f $specification.Id, $ExpectedVersion)
        if (-not (Test-Path -LiteralPath $packagePath)) {
            throw "Expected package was not produced: $packagePath"
        }

        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $entries = @($archive.Entries | ForEach-Object { $_.FullName })
            foreach ($entry in $entries) {
                if ($entry -match '(^|/)(bin|obj)/' -or
                    $entry -match '(^|/)\.\.?(/|$)' -or
                    $entry.StartsWith('/') -or
                    $entry.Contains('\')) {
                    throw "Unsafe or unintended package entry in $($specification.Id): $entry"
                }

                $isPackageMetadata =
                    $entry -eq '_rels/.rels' -or
                    $entry -eq '[Content_Types].xml' -or
                    $entry -eq 'package/services/metadata/core-properties/nuget.psmdcp' -or
                    $entry -like '*.nuspec'
                $isAllowedPayload = if ($specification.Id -eq 'Nova3D.Templates') {
                    $entry -eq 'README.md' -or $entry.StartsWith('content/')
                }
                else {
                    $entry -eq 'PACKAGE.md' -or
                    $entry.StartsWith('lib/net8.0/') -or
                    ($specification.Id -eq 'Nova3D' -and
                        $entry.StartsWith('contentFiles/any/any/Shaders/'))
                }
                if (-not $isPackageMetadata -and -not $isAllowedPayload) {
                    throw "Unexpected package payload in $($specification.Id): $entry"
                }
            }

            foreach ($requiredEntry in @($specification.Readme) + $specification.Required) {
                if ($entries -notcontains $requiredEntry) {
                    throw "Required entry missing from $($specification.Id): $requiredEntry"
                }
            }

            $libraries = @($entries | Where-Object { $_ -match '^lib/.+\.dll$' })
            if ((Compare-Object $specification.Libraries $libraries).Count -ne 0) {
                throw "Unexpected library payload in $($specification.Id): $($libraries -join ', ')"
            }

            $nuspecEntries = @($entries | Where-Object { $_ -like '*.nuspec' })
            if ($nuspecEntries.Count -ne 1) {
                throw "Package $($specification.Id) must contain exactly one nuspec."
            }

            [xml]$nuspec = Get-ZipEntryText $archive $nuspecEntries[0]
            $metadata = $nuspec.SelectSingleNode(
                "/*[local-name()='package']/*[local-name()='metadata']")
            $id = $metadata.SelectSingleNode("*[local-name()='id']").InnerText
            $version = $metadata.SelectSingleNode("*[local-name()='version']").InnerText
            $authors = $metadata.SelectSingleNode("*[local-name()='authors']").InnerText
            $license = $metadata.SelectSingleNode("*[local-name()='license']").InnerText
            $licenseType = $metadata.SelectSingleNode(
                "*[local-name()='license']").type
            $projectUrl = $metadata.SelectSingleNode(
                "*[local-name()='projectUrl']").InnerText
            $repository = $metadata.SelectSingleNode("*[local-name()='repository']")
            $readme = $metadata.SelectSingleNode("*[local-name()='readme']").InnerText

            if ($id -ne $specification.Id -or $version -ne $ExpectedVersion) {
                throw "Package identity mismatch: $id $version"
            }
            if ($authors -ne 'Bruno Batista' -or $license -ne 'MIT' -or
                $licenseType -ne 'expression') {
                throw "Package authors/license mismatch in $($specification.Id)."
            }
            if ($readme -ne $specification.Readme -or
                $projectUrl -ne 'https://github.com/batista92/Nova3D' -or
                $repository.type -ne 'git' -or
                $repository.url -ne 'https://github.com/batista92/Nova3D.git' -or
                $repository.commit -notmatch '^[0-9a-f]{40}$') {
                throw "Package readme/repository metadata mismatch in $($specification.Id)."
            }

            $dependencyNodes = @($metadata.SelectNodes(
                ".//*[local-name()='dependency']"))
            $actualDependencies = @{}
            foreach ($dependency in $dependencyNodes) {
                $actualDependencies[$dependency.id] = $dependency.version
            }
            if ($actualDependencies.Count -ne $specification.Dependencies.Count) {
                throw "Dependency count mismatch in $($specification.Id)."
            }
            foreach ($expectedDependency in $specification.Dependencies.GetEnumerator()) {
                if ($actualDependencies[$expectedDependency.Key] -ne $expectedDependency.Value) {
                    throw "Dependency mismatch in $($specification.Id): $($expectedDependency.Key)"
                }
            }

            if ($specification.Id -eq 'Nova3D.Templates') {
                $packageType = $metadata.SelectSingleNode(
                    "*[local-name()='packageTypes']/*[local-name()='packageType']")
                if ($null -eq $packageType -or $packageType.name -ne 'Template') {
                    throw 'Nova3D.Templates is missing the Template package type.'
                }
                $templateProject = Get-ZipEntryText $archive 'content/Nova3DGame.csproj'
                foreach ($packageId in 'Nova3D', 'Nova3D.Physics.Bepu', 'Nova3D.UI.Gum') {
                    $reference = 'Include="{0}" Version="{1}"' -f $packageId, $ExpectedVersion
                    if (-not $templateProject.Contains($reference)) {
                        throw "Template reference mismatch: $reference"
                    }
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    $stopwatch.Stop()
    Write-Host ("PASS package archive audit ({0:N2}s)" -f
        $stopwatch.Elapsed.TotalSeconds)
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

    # Keep restore sources deterministic and avoid native command-line parsing
    # changing an HTTPS source into a Windows path on some SDK/shell versions.
    $nugetConfig = Join-Path $temporaryRoot 'NuGet.Config'
    $escapedPackageSource = [Security.SecurityElement]::Escape($PackageSource)
    $nugetConfigContents = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="Nova3D-Local" value="$escapedPackageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    [IO.File]::WriteAllText(
        $nugetConfig,
        $nugetConfigContents,
        [Text.UTF8Encoding]::new($false))

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
        $gitIgnoreContents = Get-Content -Raw -LiteralPath $gitIgnore
        if ($gitIgnoreContents -notmatch '(?m)^dist/$') {
            throw "Generated project does not ignore publish output: $gitIgnore"
        }
        Invoke-DotNetStep ("restore {0}" -f $variant.Name) @(
            'restore', $project, '--configfile', $nugetConfig)
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
        $benchmarkProject = Join-Path $repository 'Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj'
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
            Invoke-DotNetStep 'repository restore' @('restore', $benchmarkProject)
        }
        Invoke-DotNetStep 'repository Release build + MGCB' (
            @('build', $benchmarkProject, '-c', 'Release') + (Get-RestoreArguments))

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

        [xml]$novaProjectDefinition = Get-Content -LiteralPath (
            Join-Path $repository 'Nova3D\Nova3D.csproj')
        $packageVersion = [string]$novaProjectDefinition.Project.PropertyGroup.Version
        if ([string]::IsNullOrWhiteSpace($packageVersion)) {
            throw 'Nova3D package version could not be read.'
        }
        Test-PackageArchives $packages $packageVersion

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

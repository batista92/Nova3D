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

function Test-ReleaseVersions {
    param(
        [Parameter(Mandatory)] [string[]]$PackageProjects,
        [Parameter(Mandatory)] [string]$TemplateProject
    )

    [xml]$coreProject = Get-Content -LiteralPath $PackageProjects[0]
    $expectedVersion = [string]$coreProject.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($expectedVersion)) {
        throw 'Nova3D package version could not be read.'
    }
    foreach ($project in $PackageProjects) {
        [xml]$definition = Get-Content -LiteralPath $project
        $actualVersion = [string]$definition.Project.PropertyGroup.Version
        if ($actualVersion -ne $expectedVersion) {
            throw "Package version mismatch in '$project': $actualVersion; expected $expectedVersion."
        }
    }
    $templateContents = Get-Content -Raw -LiteralPath $TemplateProject
    foreach ($id in @('Nova3D', 'Nova3D.Physics.Bepu', 'Nova3D.UI.Gum')) {
        $reference = 'Include="{0}" Version="{1}"' -f $id, $expectedVersion
        if (-not $templateContents.Contains($reference)) {
            throw "Template reference for $id must use package version $expectedVersion."
        }
    }
    Write-Host "PASS coordinated package/template version $expectedVersion"
}

function Write-PackageHashManifest {
    param(
        [Parameter(Mandatory)] [string]$PackageDirectory,
        [Parameter(Mandatory)] [string]$Version
    )

    $ids = @('Nova3D', 'Nova3D.Physics.Bepu', 'Nova3D.UI.Gum',
        'Nova3D.Cli', 'Nova3D.Templates')
    $lines = foreach ($id in $ids) {
        $name = '{0}.{1}.nupkg' -f $id, $Version
        $path = Join-Path $PackageDirectory $name
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Cannot hash missing release package: $path"
        }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        '{0}  {1}' -f $hash, $name
    }
    $manifest = Join-Path $PackageDirectory 'SHA256SUMS.txt'
    [IO.File]::WriteAllLines($manifest, $lines, [Text.UTF8Encoding]::new($false))
    Write-Host "PASS package SHA-256 manifest $manifest"
}

function Test-GeneratedGitIgnore {
    param(
        [Parameter(Mandatory)] [string]$Contents,
        [Parameter(Mandatory)] [string]$Path
    )

    # dotnet new may normalize the generated file to the host's line endings.
    # Check an entire rule line without assuming LF or CRLF.
    $rules = @($Contents -split '\r\n|\n|\r')
    if ($rules -notcontains 'dist/') {
        throw "Generated project does not ignore publish output: $Path"
    }
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

function Test-CorePackageBoundary {
    param([Parameter(Mandatory)] [string]$Project)

    $script:stepCount++
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host 'RUN  core package boundary'

    [xml]$projectDocument = Get-Content -Raw -LiteralPath $Project
    $projectReferences = @($projectDocument.SelectNodes(
        "/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='ProjectReference']"))
    if ($projectReferences.Count -ne 0) {
        throw 'Core Nova3D must not contain ProjectReference dependencies.'
    }

    $assemblyReferences = @($projectDocument.SelectNodes(
        "/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='Reference']"))
    if ($assemblyReferences.Count -ne 0) {
        throw 'Core Nova3D must not contain explicit assembly Reference dependencies.'
    }

    $packageReferences = @($projectDocument.SelectNodes(
        "/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='PackageReference']"))
    $packageIds = @($packageReferences | ForEach-Object { $_.Include })
    if ($packageIds.Count -ne 1 -or $packageIds[0] -ne 'MonoGame.Framework.DesktopGL') {
        throw "Core package dependency changed: $($packageIds -join ', ')"
    }

    $externalCompile = @($projectDocument.SelectNodes(
        "/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='Compile']") |
        Where-Object {
            $_.Include -and ([IO.Path]::IsPathRooted($_.Include) -or
                $_.Include -match '(^|[\\/])\.\.([\\/]|$)')
        })
    if ($externalCompile.Count -ne 0) {
        throw 'Core Nova3D must not compile source from outside its project directory.'
    }

    $stopwatch.Stop()
    Write-Host ("PASS core package boundary ({0:N2}s)" -f $stopwatch.Elapsed.TotalSeconds)
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
                'contentFiles/any/any/Shaders/Water.fx',
                'contentFiles/any/any/Schemas/nova3d.scene.1.schema.json',
                'contentFiles/any/any/Schemas/nova3d.visual-capture.1.schema.json'
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
            Id = 'Nova3D.Cli'
            Readme = 'PACKAGE.md'
            Required = @(
                'tools/net8.0/any/DotnetToolSettings.xml',
                'tools/net8.0/any/Nova3D.Cli.dll',
                'tools/net8.0/any/Nova3D.Cli.deps.json',
                'tools/net8.0/any/Nova3D.Cli.runtimeconfig.json',
                'tools/net8.0/any/Nova3D.dll',
                'tools/net8.0/any/SixLabors.ImageSharp.dll',
                'tools/net8.0/any/Schemas/nova3d.visual-baseline.1.schema.json',
                'tools/net8.0/any/Schemas/nova3d.performance-report.1.schema.json',
                'tools/net8.0/any/Schemas/nova3d.performance-budget.1.schema.json'
            )
            Libraries = @()
            Dependencies = @{}
        },
        @{
            Id = 'Nova3D.Templates'
            Readme = 'README.md'
            Required = @(
                'content/.gitignore',
                'content/.template.config/template.json',
                'content/.agents/skills/nova3d-game-development/SKILL.md',
                'content/Docs/Schemas/nova3d.scene.1.schema.json',
                'content/Docs/Recipes/scene-authoring.md',
                'content/Docs/Recipes/game-presentation.md',
                'content/GAME_DESIGN.md',
                'content/PRESENTATION_REVIEW.md',
                'content/Docs/input.md',
                'content/Docs/audio.md',
                'content/Docs/persistence.md',
                'content/Docs/skinning.md',
                'content/Docs/package-boundaries.md',
                'content/Docs/cli.md',
                'content/Assets/Scenes/starter.scene.json',
                'content/Assets/Prefabs/marker.scene.json',
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
                elseif ($specification.Id -eq 'Nova3D.Cli') {
                    $entry -eq 'PACKAGE.md' -or $entry.StartsWith('tools/net8.0/any/')
                }
                else {
                    $entry -eq 'PACKAGE.md' -or
                    $entry.StartsWith('lib/net8.0/') -or
                    ($specification.Id -eq 'Nova3D' -and
                        ($entry.StartsWith('contentFiles/any/any/Shaders/') -or
                         $entry.StartsWith('contentFiles/any/any/Schemas/')))
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

            if ($specification.Id -in @('Nova3D.Templates', 'Nova3D.Cli')) {
                $packageType = $metadata.SelectSingleNode(
                    "*[local-name()='packageTypes']/*[local-name()='packageType']")
                $expectedPackageType = if ($specification.Id -eq 'Nova3D.Templates') {
                    'Template'
                }
                else {
                    'DotnetTool'
                }
                if ($null -eq $packageType -or $packageType.name -ne $expectedPackageType) {
                    throw "$($specification.Id) is missing the $expectedPackageType package type."
                }
            }
            if ($specification.Id -eq 'Nova3D.Templates') {
                $templateProject = Get-ZipEntryText $archive 'content/Nova3DGame.csproj'
                foreach ($packageId in 'Nova3D', 'Nova3D.Physics.Bepu', 'Nova3D.UI.Gum') {
                    $reference = 'Include="{0}" Version="{1}"' -f $packageId, $ExpectedVersion
                    if (-not $templateProject.Contains($reference)) {
                        throw "Template reference mismatch: $reference"
                    }
                }
            }
            elseif ($specification.Id -eq 'Nova3D.Cli') {
                [xml]$toolSettings = Get-ZipEntryText $archive `
                    'tools/net8.0/any/DotnetToolSettings.xml'
                $command = $toolSettings.SelectSingleNode(
                    "/*[local-name()='DotNetCliTool']/*[local-name()='Commands']/*[local-name()='Command']")
                if ($null -eq $command -or $command.Name -ne 'nova3d' -or
                    $command.EntryPoint -ne 'Nova3D.Cli.dll') {
                    throw 'Nova3D.Cli tool command metadata is invalid.'
                }
                $depsJson = Get-ZipEntryText $archive 'tools/net8.0/any/Nova3D.Cli.deps.json'
                if (-not $depsJson.Contains(('Nova3D/{0}' -f $ExpectedVersion))) {
                    throw 'Nova3D.Cli does not embed the aligned Nova3D core version.'
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

function Test-CliToolPackage {
    param(
        [Parameter(Mandatory)] [string]$PackageDirectory,
        [Parameter(Mandatory)] [string]$ExpectedVersion
    )

    $script:stepCount++
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host 'RUN  isolated CLI tool install'
    $toolRoot = Join-Path ([IO.Path]::GetTempPath()) (
        'nova3d-cli-validation-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $installOutput = @(& dotnet tool install --tool-path $toolRoot `
                --add-source $PackageDirectory --ignore-failed-sources `
                --version $ExpectedVersion Nova3D.Cli 2>&1)
            $installExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        if ($installExitCode -ne 0) {
            $installOutput | ForEach-Object { Write-Host $_ }
            throw "CLI tool install failed with exit code $installExitCode."
        }

        $command = Join-Path $toolRoot $(if ($env:OS -eq 'Windows_NT') {
            'nova3d.exe'
        } else {
            'nova3d'
        })
        $versionOutput = (& $command --version 2>&1).ToString().Trim()
        if ($LASTEXITCODE -ne 0 -or $versionOutput -ne $ExpectedVersion) {
            throw "Installed CLI returned '$versionOutput'; expected '$ExpectedVersion'."
        }

        $doctorProject = Join-Path $repository 'Samples\DataDrivenScene\DataDrivenScene.csproj'
        $doctorOutput = @(& $command doctor $doctorProject 2>&1)
        $doctorExitCode = $LASTEXITCODE
        if ($doctorExitCode -ne 0 -or
            -not ($doctorOutput -match '^DOCTOR PASS \|')) {
            $doctorOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI doctor failed with exit code $doctorExitCode."
        }

        $jsonDoctorOutput = @(& $command doctor $doctorProject --format json 2>&1)
        $jsonDoctorExitCode = $LASTEXITCODE
        try {
            $jsonDoctor = ($jsonDoctorOutput -join [Environment]::NewLine) | ConvertFrom-Json
        }
        catch {
            $jsonDoctorOutput | ForEach-Object { Write-Host $_ }
            throw 'Installed CLI doctor did not produce one valid JSON document.'
        }
        if ($jsonDoctorExitCode -ne 0 -or
            $jsonDoctor.schemaVersion -ne 1 -or
            $jsonDoctor.command -ne 'doctor' -or
            $jsonDoctor.status -ne 'pass' -or
            $jsonDoctor.exitCode -ne 0 -or
            $null -eq $jsonDoctor.messages -or
            $null -eq $jsonDoctor.recommendedActions) {
            $jsonDoctorOutput | ForEach-Object { Write-Host $_ }
            throw 'Installed CLI doctor JSON contract is invalid.'
        }

        $nonInteractiveStart = [Diagnostics.ProcessStartInfo]::new()
        $nonInteractiveStart.FileName = $command
        $quotedDoctorProject = $doctorProject.Replace('"', '\"')
        $nonInteractiveStart.Arguments = "doctor `"$quotedDoctorProject`" --format json"
        $nonInteractiveStart.UseShellExecute = $false
        $nonInteractiveStart.CreateNoWindow = $true
        $nonInteractiveStart.RedirectStandardInput = $true
        $nonInteractiveStart.RedirectStandardOutput = $true
        $nonInteractiveStart.RedirectStandardError = $true
        $nonInteractiveProcess = [Diagnostics.Process]::Start($nonInteractiveStart)
        try {
            $nonInteractiveProcess.StandardInput.Close()
            $nonInteractiveStdout = $nonInteractiveProcess.StandardOutput.ReadToEndAsync()
            $nonInteractiveStderr = $nonInteractiveProcess.StandardError.ReadToEndAsync()
            if (-not $nonInteractiveProcess.WaitForExit(30000)) {
                $nonInteractiveProcess.Kill()
                throw 'Installed CLI waited for input after stdin was closed.'
            }
            [Threading.Tasks.Task]::WaitAll(@($nonInteractiveStdout, $nonInteractiveStderr))
            if ($nonInteractiveProcess.ExitCode -ne 0 -or
                -not [string]::IsNullOrWhiteSpace($nonInteractiveStderr.Result)) {
                throw "Installed CLI non-interactive doctor failed: $($nonInteractiveStderr.Result)"
            }
            $null = $nonInteractiveStdout.Result | ConvertFrom-Json
        }
        finally {
            $nonInteractiveProcess.Dispose()
        }

        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $jsonFailureOutput = @(& $command inspect `
                (Join-Path $toolRoot 'missing.glb') --format json 2>&1)
            $jsonFailureExitCode = $LASTEXITCODE
            $jsonUsageOutput = @(& $command inspect --format json 2>&1)
            $jsonUsageExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        $jsonFailure = ($jsonFailureOutput -join [Environment]::NewLine) | ConvertFrom-Json
        $jsonUsage = ($jsonUsageOutput -join [Environment]::NewLine) | ConvertFrom-Json
        if ($jsonFailureExitCode -ne 1 -or
            $jsonFailure.status -ne 'fail' -or
            $jsonFailure.exitCode -ne 1 -or
            $jsonFailure.recommendedActions.Count -lt 1 -or
            $jsonFailure.messages[0].code -ne 'file') {
            throw 'Installed CLI command-failure JSON/exit-code contract is invalid.'
        }
        if ($jsonUsageExitCode -ne 2 -or
            $jsonUsage.status -ne 'usage_error' -or
            $jsonUsage.exitCode -ne 2 -or
            $jsonUsage.recommendedActions.Count -lt 1) {
            throw 'Installed CLI usage-error JSON/exit-code contract is invalid.'
        }

        $validateOutput = @(& $command validate $doctorProject --no-restore 2>&1)
        $validateExitCode = $LASTEXITCODE
        if ($validateExitCode -ne 0 -or
            -not ($validateOutput -match '^VALIDATION PASS \|')) {
            $validateOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI validate failed with exit code $validateExitCode."
        }

        $glbFixture = Join-Path $toolRoot 'minimal.glb'
        $jsonText = '{"asset":{"version":"2.0","generator":"Nova3D validation"},"scene":0,"scenes":[{}]}'
        while ([Text.Encoding]::UTF8.GetByteCount($jsonText) % 4 -ne 0) {
            $jsonText += ' '
        }
        $jsonBytes = [Text.Encoding]::UTF8.GetBytes($jsonText)
        $stream = [IO.File]::Create($glbFixture)
        try {
            $writer = [IO.BinaryWriter]::new($stream)
            try {
                $writer.Write([uint32]0x46546C67)
                $writer.Write([uint32]2)
                $writer.Write([uint32](12 + 8 + $jsonBytes.Length))
                $writer.Write([uint32]$jsonBytes.Length)
                $writer.Write([uint32]0x4E4F534A)
                $writer.Write($jsonBytes)
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        $inspectOutput = @(& $command inspect $glbFixture 2>&1)
        $inspectExitCode = $LASTEXITCODE
        if ($inspectExitCode -ne 0 -or
            -not ($inspectOutput -match '^INSPECTION PASS \|')) {
            $inspectOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI GLB inspection failed with exit code $inspectExitCode."
        }

        $sceneFixture = Join-Path $repository `
            'templates\Nova3D.Templates\content\Assets\Scenes\starter.scene.json'
        $sceneInspectOutput = @(& $command inspect $sceneFixture 2>&1)
        $sceneInspectExitCode = $LASTEXITCODE
        if ($sceneInspectExitCode -ne 0 -or
            -not ($sceneInspectOutput -match '^INSPECTION PASS \|')) {
            $sceneInspectOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI scene inspection failed with exit code $sceneInspectExitCode."
        }

        $publishProjectRoot = Join-Path $toolRoot 'publish-game'
        New-Item -ItemType Directory -Force -Path $publishProjectRoot | Out-Null
        $publishProject = Join-Path $publishProjectRoot 'PublishGame.csproj'
        $coreProject = Join-Path $repository 'Nova3D\Nova3D.csproj'
        $escapedCoreProject = [Security.SecurityElement]::Escape($coreProject)
        $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
        [IO.File]::WriteAllText($publishProject, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$escapedCoreProject" />
  </ItemGroup>
</Project>
"@, $utf8WithoutBom)
        [IO.File]::WriteAllText(
            (Join-Path $publishProjectRoot 'Program.cs'),
            'Console.WriteLine("Nova3D publish smoke");',
            $utf8WithoutBom)

        $publishOutput = @(& $command publish --runtime win-x64 $publishProject 2>&1)
        $publishExitCode = $LASTEXITCODE
        $publishedDirectory = Join-Path $publishProjectRoot 'dist\win-x64'
        $publishedExecutable = Join-Path $publishedDirectory 'PublishGame.exe'
        if ($publishExitCode -ne 0 -or
            -not ($publishOutput -match '^PUBLISH PASS \|') -or
            -not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
            $publishOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI publish failed with exit code $publishExitCode."
        }

        $replacementOutput = @(& $command publish --runtime win-x64 $publishProject 2>&1)
        $replacementExitCode = $LASTEXITCODE
        if ($replacementExitCode -ne 0 -or
            -not ($replacementOutput -match '^PUBLISH PASS \|') -or
            -not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
            $replacementOutput | ForEach-Object { Write-Host $_ }
            throw "Installed CLI replacement publish failed with exit code $replacementExitCode."
        }

        $lastGoodHash = (Get-FileHash -LiteralPath $publishedExecutable -Algorithm SHA256).Hash
        [IO.File]::WriteAllText(
            (Join-Path $publishProjectRoot 'Program.cs'),
            'this is intentionally invalid C#;',
            $utf8WithoutBom)
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $failedPublishOutput = @(& $command publish --runtime win-x64 $publishProject 2>&1)
            $failedPublishExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        $preservedHash = if (Test-Path -LiteralPath $publishedExecutable -PathType Leaf) {
            (Get-FileHash -LiteralPath $publishedExecutable -Algorithm SHA256).Hash
        } else {
            $null
        }
        if ($failedPublishExitCode -eq 0 -or $preservedHash -ne $lastGoodHash) {
            $failedPublishOutput | ForEach-Object { Write-Host $_ }
            throw 'Failed CLI publish did not preserve the last good executable.'
        }
    }
    finally {
        $resolvedToolRoot = [IO.Path]::GetFullPath($toolRoot)
        $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedToolRoot.StartsWith($resolvedTempRoot,
                [StringComparison]::OrdinalIgnoreCase) -or
            $resolvedToolRoot.Length -le $resolvedTempRoot.Length) {
            throw 'Refusing to remove an unverified CLI validation directory.'
        }
        if (Test-Path -LiteralPath $resolvedToolRoot) {
            Remove-Item -LiteralPath $resolvedToolRoot -Recurse -Force
        }
    }
    $stopwatch.Stop()
    Write-Host ("PASS isolated CLI tool install ({0:N2}s)" -f $stopwatch.Elapsed.TotalSeconds)
}

function Test-TemplateVariants {
    param(
        [Parameter(Mandatory)] [string]$TemplatePackage,
        [Parameter(Mandatory)] [string]$PackageSource
    )

    $script:temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
        'nova3d-validation-' + [Guid]::NewGuid().ToString('N'))
    Test-GeneratedGitIgnore "bin/`ndist/`n" 'LF regression fixture'
    Test-GeneratedGitIgnore "bin/`r`ndist/`r`n" 'CRLF regression fixture'
    $hive = Join-Path $temporaryRoot 'template-hive'
    $games = Join-Path $temporaryRoot 'games'
    New-Item -ItemType Directory -Force -Path $hive, $games | Out-Null

    # Keep restore sources deterministic and avoid native command-line parsing
    # changing an HTTPS source into a Windows path on some SDK/shell versions.
    $nugetConfig = Join-Path $temporaryRoot 'NuGet.Config'
    $escapedPackageSource = [Security.SecurityElement]::Escape($PackageSource)
    $isolatedPackageCache = Join-Path $temporaryRoot 'nuget-packages'
    $existingPackageCache = Join-Path $env:USERPROFILE '.nuget\packages'
    $escapedExistingPackageCache = [Security.SecurityElement]::Escape($existingPackageCache)
    $escapedIsolatedPackageCache = [Security.SecurityElement]::Escape($isolatedPackageCache)
    $nugetConfigContents = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="$escapedIsolatedPackageCache" />
  </config>
  <packageSources>
    <clear />
    <add key="Nova3D-Local" value="$escapedPackageSource" />
    <add key="ExistingPackageCache" value="$escapedExistingPackageCache" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="Nova3D-Local">
      <package pattern="Nova3D*" />
    </packageSource>
    <packageSource key="ExistingPackageCache">
      <package pattern="*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
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
        @{ Name = 'FullGame'; Options = @('--physics', '--ui') },
        @{ Name = 'SceneGame'; Options = @('--scene') },
        @{ Name = 'FullSceneGame'; Options = @('--physics', '--ui', '--scene') }
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
        foreach ($relativePath in @(
            'GAME_DESIGN.md',
            'PRESENTATION_REVIEW.md',
            'Docs\Recipes\game-presentation.md')) {
            $presentationFile = Join-Path $output $relativePath
            if (-not (Test-Path -LiteralPath $presentationFile)) {
                throw "Generated project is missing presentation guidance: $presentationFile"
            }
        }
        $gitIgnore = Join-Path $output '.gitignore'
        if (-not (Test-Path -LiteralPath $gitIgnore)) {
            throw "Generated project is missing repository ignore rules: $gitIgnore"
        }
        $sceneFile = Join-Path $output 'Assets\Scenes\starter.scene.json'
        if ($variant.Options -contains '--scene') {
            if (-not (Test-Path -LiteralPath $sceneFile)) {
                throw "Scene variant is missing its scene document: $sceneFile"
            }
        }
        elseif (Test-Path -LiteralPath $sceneFile) {
            throw "Non-scene variant unexpectedly contains a scene document: $sceneFile"
        }
        $gitIgnoreContents = Get-Content -Raw -LiteralPath $gitIgnore
        Test-GeneratedGitIgnore $gitIgnoreContents $gitIgnore
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
        $sceneContractTests = Join-Path $repository 'Benchmarks\SceneContractTests\SceneContractTests.csproj'
        $inputContractTests = Join-Path $repository 'Benchmarks\InputContractTests\InputContractTests.csproj'
        $audioContractTests = Join-Path $repository 'Benchmarks\AudioContractTests\AudioContractTests.csproj'
        $persistenceContractTests = Join-Path $repository 'Benchmarks\PersistenceContractTests\PersistenceContractTests.csproj'
        $visualCaptureContractTests = Join-Path $repository 'Benchmarks\VisualCaptureContractTests\VisualCaptureContractTests.csproj'
        $visualRegressionContractTests = Join-Path $repository 'Benchmarks\VisualRegressionContractTests\VisualRegressionContractTests.csproj'
        $performanceReportContractTests = Join-Path $repository 'Benchmarks\PerformanceReportContractTests\PerformanceReportContractTests.csproj'
        $skinningSpike = Join-Path $repository 'Benchmarks\SkinningSpike\SkinningSpike.csproj'
        $packages = Join-Path $repository 'artifacts\packages'
        $sampleProjects = @(
            (Join-Path $repository 'Samples\Minimal3D\Minimal3D.csproj'),
            (Join-Path $repository 'Samples\DataDrivenScene\DataDrivenScene.csproj'),
            (Join-Path $repository 'Samples\SceneFlow\SceneFlow.csproj'),
            (Join-Path $repository 'Samples\PhysicsPlayground\PhysicsPlayground.csproj'),
            (Join-Path $repository 'Samples\GumMenus\GumMenus.csproj'),
            (Join-Path $repository 'Samples\RollingBall\RollingBall.csproj'),
            (Join-Path $repository 'Samples\InputActions\InputActions.csproj'),
            (Join-Path $repository 'Samples\AnimatedCharacter\AnimatedCharacter.csproj')
        )
        $projectsToPack = @(
            (Join-Path $repository 'Nova3D\Nova3D.csproj'),
            (Join-Path $repository 'Nova3D.Physics.Bepu\Nova3D.Physics.Bepu.csproj'),
            (Join-Path $repository 'Nova3D.UI.Gum\Nova3D.UI.Gum.csproj'),
            (Join-Path $repository 'Nova3D.Cli\Nova3D.Cli.csproj'),
            (Join-Path $repository 'templates\Nova3D.Templates\Nova3D.Templates.csproj')
        )

        Test-ReleaseVersions $projectsToPack (
            Join-Path $repository 'templates\Nova3D.Templates\content\Nova3DGame.csproj')

        New-Item -ItemType Directory -Force -Path $packages | Out-Null
        if (-not $NoRestore) {
            Invoke-DotNetStep 'repository restore' @('restore', $benchmarkProject)
        }
        Invoke-DotNetStep 'repository Release build + MGCB' (
            @('build', $benchmarkProject, '-c', 'Release') + (Get-RestoreArguments))

        Test-CorePackageBoundary (Join-Path $repository 'Nova3D\Nova3D.csproj')

        Invoke-DotNetStep 'scene document regression' (
            @('run', '--project', $sceneContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'input action regression' (
            @('run', '--project', $inputContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'audio mixer regression' (
            @('run', '--project', $audioContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'persistence regression' (
            @('run', '--project', $persistenceContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'visual capture contract regression' (
            @('run', '--project', $visualCaptureContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'visual comparison contract regression' (
            @('run', '--project', $visualRegressionContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'performance report contract regression' (
            @('run', '--project', $performanceReportContractTests, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        Invoke-DotNetStep 'skinning palette and animation CPU regression' (
            @('run', '--project', $skinningSpike, '-c', 'Release') +
            (Get-RestoreArguments)) -ShowResult

        $skinningShader = Join-Path $repository 'Benchmarks\SkinningSpike\SkinningProbe.fx'
        $skinningProbeRoot = Join-Path $repository 'Benchmarks\SkinningSpike\obj\shader-probe'
        Invoke-DotNetStep 'skinning shader DesktopGL probe' @(
            'mgcb', '/quiet', '/rebuild', '/platform:DesktopGL', '/profile:HiDef',
            ("/outputDir:{0}" -f (Join-Path $skinningProbeRoot 'gl\bin')),
            ("/intermediateDir:{0}" -f (Join-Path $skinningProbeRoot 'gl\obj')),
            ("/build:{0}" -f $skinningShader))
        Invoke-DotNetStep 'skinning shader DirectX probe' @(
            'mgcb', '/quiet', '/rebuild', '/platform:Windows', '/profile:HiDef',
            ("/outputDir:{0}" -f (Join-Path $skinningProbeRoot 'dx\bin')),
            ("/intermediateDir:{0}" -f (Join-Path $skinningProbeRoot 'dx\obj')),
            ("/build:{0}" -f $skinningShader))

        $pbrShader = Join-Path $repository 'Nova3D\Shaders\PBR.fx'
        $shadowShader = Join-Path $repository 'Nova3D\Shaders\ShadowDepth.fx'
        $animatedShaderRoot = Join-Path $repository 'Benchmarks\SkinningSpike\obj\animated-render'
        Invoke-DotNetStep 'animated renderer shaders DesktopGL' @(
            'mgcb', '/quiet', '/rebuild', '/platform:DesktopGL', '/profile:HiDef',
            ("/outputDir:{0}" -f (Join-Path $animatedShaderRoot 'gl\bin')),
            ("/intermediateDir:{0}" -f (Join-Path $animatedShaderRoot 'gl\obj')),
            ("/build:{0}" -f $pbrShader),
            ("/build:{0}" -f $shadowShader))
        Invoke-DotNetStep 'animated renderer shaders DirectX' @(
            'mgcb', '/quiet', '/rebuild', '/platform:Windows', '/profile:HiDef',
            ("/outputDir:{0}" -f (Join-Path $animatedShaderRoot 'dx\bin')),
            ("/intermediateDir:{0}" -f (Join-Path $animatedShaderRoot 'dx\obj')),
            ("/build:{0}" -f $pbrShader),
            ("/build:{0}" -f $shadowShader))

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

        Invoke-DotNetStep 'sample SceneFlow headless smoke' (
            @('run', '--project', (Join-Path $repository 'Samples\SceneFlow\SceneFlow.csproj'),
              '-c', 'Release', '--no-build') + (Get-RestoreArguments) + @('--', '--smoke')) -ShowResult

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
        Test-CliToolPackage $packages $packageVersion
        Write-PackageHashManifest $packages $packageVersion

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

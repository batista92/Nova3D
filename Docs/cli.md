# Nova3D CLI

`Nova3D.Cli` is a separate .NET tool package. It lets generated projects run
Nova3D validation and diagnostics without a checkout of the toolkit repository.

## Commands

The installed command supports:

```text
nova3d --help
nova3d --version
nova3d doctor [PROJECT|DIRECTORY]
nova3d validate [PROJECT|DIRECTORY] [--no-restore]
nova3d inspect <MODEL.glb>
nova3d inspect <SCENE.scene.json>
nova3d publish --runtime <RID> [PROJECT|DIRECTORY]
nova3d visual compare <CAPTURE.png> --baseline-root <DIRECTORY>
nova3d visual update-baseline <CAPTURE.png> --baseline-root <DIRECTORY> --accept
```

`--format human|json` is a global option and may appear before or after a
command. `human` is the default. Successful human output is deliberately
compact: summaries, warnings and relevant `INFO`/`RUN` lines are retained;
verbose successful subprocess logs are suppressed. Failed commands retain the
full diagnostic output.

When no path is supplied, `doctor` uses the current directory. A directory must
contain exactly one top-level `.csproj`; otherwise pass the project explicitly.
The command checks:

- a .NET 8 or newer SDK is installed;
- the project targets `net8.0` or newer;
- a `Nova3D` package or project reference exists;
- `obj/project.assets.json` exists after restore;
- every declared `MonoGameContentReference` points to an existing file.

Projects without MGCB content receive a warning, not a failure. A successful
diagnosis returns `0`; a failed check returns `1`; invalid command usage returns
`2`. Each failure includes the corrective action when one is known.

`validate` restores the selected project, runs the same `doctor` preflight and
builds it in `Release`, including declared MGCB content. Use `--no-restore`
only after a successful restore. It validates compilation and content-pipeline
processing; it does not open a window or claim visual, input, audio, GPU-timing
or clean-machine publish coverage.

`inspect <MODEL.glb>` performs CPU-only validation without opening a window or
creating a `GraphicsDevice`. It verifies the GLB 2.0 header, length and chunks;
parses the glTF JSON; checks embedded/external buffers and images; reports scene,
mesh, primitive, material, skin and animation counts; and diagnoses Nova3D's
documented importer limits. Missing resources and unsupported required features
fail the command. Ignored optional attributes/extensions and alpha `BLEND`
produce warnings. This increment intentionally accepts `.glb` only.

`inspect <SCENE.scene.json>` uses Nova3D's authoritative CPU parser, built-in
component registry, semantic validator and prefab expander. It reports source
and expanded node counts, roots, hierarchy depth and component counts. Prefab
and `nova3d.model` references are resolved with `SceneAssetResolver`; when the
scene is under an `Assets` directory, that ancestor is the asset root. The
scene directory is the documented fallback for standalone fixtures. Custom
game component types remain unknown because the distributed CLI does not load
game assemblies or scan descriptors.

`publish` restores for the requested concrete runtime, runs the `doctor`
preflight and creates a self-contained `Release` build in `dist/<RID>`. The
output is first written to a private staging directory. An existing
`dist/<RID>` is replaced only after the new publish succeeds, so a failed build
does not destroy the last good output. For example:

```powershell
nova3d publish --runtime win-x64
nova3d publish --runtime linux-x64 .\MyGame.csproj
```

The command validates that the publish produced files and an application entry
point. It does not create an archive, launch the game or prove that graphics,
audio and native dependencies work on the target machine. Run the executable
from `dist/<RID>` on that target (preferably a clean machine or VM) before a
release.

`visual update-baseline` copies a reviewed capture and its metadata into
`<root>/<scene>/v<capture-version>/`. It never updates a baseline unless
`--accept` is present. The manifest records SHA-256 hashes, capture environment
and the default tolerances. Keep this directory in source control.

`visual compare` validates those hashes, compares RGBA pixels and writes a PNG
diff. It reports changed-pixel ratio, maximum channel delta, mean absolute error
and RMSE. A pixel differs when any channel exceeds `--channel-tolerance`; the
comparison fails when the resulting ratio exceeds
`--max-different-pixel-ratio`. Command-line values override the defaults stored
in the baseline manifest. For example:

```powershell
nova3d visual update-baseline .\artifacts\captures\terrain.png `
  --baseline-root .\Benchmarks\VisualBaselines --accept `
  --channel-tolerance 2 --max-different-pixel-ratio 0.001

nova3d visual compare .\artifacts\captures\terrain.png `
  --baseline-root .\Benchmarks\VisualBaselines `
  --diff .\artifacts\visual-diffs\terrain.png
```

The default stored tolerance is two byte values per channel and 0.1% changed
pixels. A GPU/backend mismatch emits a warning because cross-environment
comparisons are auxiliary evidence. The normative baseline manifest schema is
[nova3d.visual-baseline.1.schema.json](Schemas/nova3d.visual-baseline.1.schema.json).

## Stable automation contract

Nova3D CLI never asks questions or reads stdin. Child `dotnet` processes also
receive a closed stdin and opt out of first-run/telemetry UI. This makes every
command safe for CI and agent subprocesses without a pseudo-terminal.

Exit codes are stable for schema version 1:

| Code | Meaning |
|---:|---|
| `0` | command completed successfully, possibly with warnings |
| `1` | the requested validation, inspection or operation failed |
| `2` | command-line usage is invalid |
| `3` | unexpected internal CLI failure |

Every nonzero human result includes an `ACTION | ...` line. With JSON output,
the same guidance is returned in `recommendedActions`:

```powershell
nova3d inspect .\Assets\Models\vehicle.glb --format json
nova3d --format json validate .\MyGame.csproj
```

JSON is emitted as one document on stdout; stderr remains empty even when the
exit code is nonzero. The document contains `schemaVersion`, `command`,
`status`, `exitCode`, `summary`, `messages` and `recommendedActions`. Consumers
must branch on `schemaVersion` before reading fields and must still use the
process exit code as the primary success signal. The normative schema is
[nova3d-cli-result-v1.schema.json](Schemas/nova3d-cli-result-v1.schema.json).

## Performance budgets

```powershell
nova3d performance check <REPORT.json> --budget <BUDGET.json>
nova3d --format json performance check <REPORT.json> --budget <BUDGET.json>
```

The command validates version 1 report/budget contracts, scene identity,
minimum sample count, non-negative finite values and the committed timing/draw/
triangle limits. A different GPU, backend, resolution, profile or MSAA setting
adds `WARN environment`; such a comparison is auxiliary rather than the
official controlled-machine gate. See [performance.md](performance.md).

## Install from this checkout

Pack the current core and CLI, then install the tool into an isolated path:

```powershell
.\eng\install-cli.ps1
.\artifacts\tools\nova3d-cli\nova3d.exe --version
```

Use `-ToolPath` to select another directory. The script reinstalls an existing
tool-path package so source changes remain testable while the coordinated
package version is unchanged. It uses the local package source and tolerates an
unavailable remote NuGet feed.

## Install from NuGet

After `Nova3D.Cli` is published with the next coordinated release:

```powershell
dotnet tool install --global Nova3D.Cli --version <VERSION>
nova3d --version
```

Replace `<VERSION>` with the coordinated Nova3D release being installed. Use
`dotnet tool update --global Nova3D.Cli` for later releases. G7.1 validates
the NuGet `DotnetTool` package and install workflow locally; it does not publish
to nuget.org.

## Package contract

- Package ID: `Nova3D.Cli`.
- Command: `nova3d`.
- Target: .NET 8.
- Version: aligned with `Nova3D`, physics, UI and templates.
- Tool package embeds the matching core assembly and MonoGame runtime closure.
- CLI release metadata, license, repository commit and payload are audited by
  `eng/validate.ps1`.

Repository validation installs the produced `.nupkg` into a fresh temporary
tool path and requires `--version`, `doctor`, `validate --no-restore`, model
and scene inspection, and two consecutive self-contained publishes to pass
using that installed copy.

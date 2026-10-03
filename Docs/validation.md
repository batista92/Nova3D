# Validation

GitHub Actions runs the full repository command on Windows and a separate
Linux core/CPU-contract subset. The Windows job audits all five package
archives and tests the isolated CLI and template variants. Neither job replaces
the controlled graphics-machine visual/performance gates or a clean-machine
executable smoke; see [RELEASE_0.3.0.md](RELEASE_0.3.0.md).

Nova3D provides one command for deterministic build/package/template checks.
Run it from PowerShell at the repository root:

```powershell
.\eng\validate.ps1
```

The repository flow validates:

1. restore and Release build of the benchmark consumer;
2. MGCB shader/content compilation through that build;
3. the CPU-only scene, prefab, asset and authored sample regression, plus input,
   audio, persistence, visual-capture-plan, visual-comparison and performance
   report/budget contracts;
4. the skinning palette CPU/allocation regression and 48-joint shader probes
   for DesktopGL and DirectX HiDef;
5. the headless physics regression benchmark;
6. Release builds of all projects under `Samples/`, including `DataDrivenScene`
   and `SceneFlow`, followed by a headless `SceneFlow --smoke` lifecycle check;
7. Release packages for core, physics, UI, CLI and templates;
8. package identity, metadata, dependencies, required payload and forbidden
   `bin/`, `obj/` or unsafe paths;
9. isolated installation of the local `Nova3D.Cli` package (including its image
   comparison runtime and baseline schema) and execution of
   `--version`, `doctor`, `validate --no-restore`, model/scene `inspect` and
   the stable JSON/exit-code/non-interactive contract, two consecutive
   self-contained publishes of a temporary game, and a deliberate compile
   failure that must preserve the last good executable;
10. isolated generation and Release builds of default, `--physics`, `--ui`,
   `--physics --ui`, `--scene` and fully combined template variants.

The template smoke creates an isolated package cache and `NuGet.Config`.
`Nova3D*` packages are mapped to this run's local package output; other
dependencies can come from the existing package cache or nuget.org. This
prevents an older global `Nova3D` package with the same development version
from hiding missing APIs in generated projects.

Successful command output is intentionally compact:

```text
RUN  repository Release build + MGCB
PASS repository Release build + MGCB (2.10s)
...
VALIDATION PASS | scope repository | steps N | 175.00s
```

On failure, the command output for the failed step is printed and the script
returns a nonzero exit code. Temporary template projects are preserved on
failure and their path is reported. Use `-KeepTemporary` to preserve them after
success as well.

## Validate a consuming game

With the distributed CLI installed, run this from the game directory or pass
the project explicitly:

```powershell
nova3d validate
nova3d validate C:\Games\MyGame\MyGame.csproj
```

This is the normal consumer path and does not require a Nova3D source checkout.

From a Nova3D source checkout, the repository script can validate the same
consumer:

```powershell
.\eng\validate.ps1 -GameProject C:\Games\Marble3D\Marble3D.csproj
```

This restores and builds the game in Release, including its MGCB content. It
does not launch a graphical window, judge visual output or replace manual input,
audio and clean-machine publish testing.

Run the committed PBR/CSM, terrain/vegetation, water/PostFX and static/animated
GLB baselines separately on the controlled graphics machine:

```powershell
.\eng\run-visual-regression.ps1
```

The script captures all three deterministic scenes, compares them through the
distributed CLI contract and writes diffs under `artifacts/visual-diffs/suite`.
It never updates baselines. Review a capture and use the explicit
`nova3d visual update-baseline ... --accept` workflow when a change is intended.

Run the separate controlled-machine performance gate with:

```powershell
.\eng\run-performance-regression.ps1
```

It preserves the headless physics regression, captures the fixed CityBenchmark
performance report, and checks the committed timing, draw and triangle budget.
The normal repository validation tests the report contract but deliberately
does not open the graphical benchmark window.

## Focused options

```powershell
.\eng\validate.ps1 -NoRestore
.\eng\validate.ps1 -SkipPhysicsBenchmark
.\eng\validate.ps1 -SkipTemplateSmoke
```

`-NoRestore` is intended for an already restored repository or game. Template
smoke projects still restore because they consume the packages created during
the same run.

Do not use skip switches as release evidence. They exist for fast local work;
the default command is the release baseline.

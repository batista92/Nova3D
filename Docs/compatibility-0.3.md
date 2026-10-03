# Nova3D 0.3 compatibility and migration

This page describes the 0.3 source/GitHub release. Publication on nuget.org is
deferred; keep all five Nova3D local-feed packages at the same version.

## Upgrade from 0.2.0

1. Update `Nova3D` to `0.3.0`; update `Nova3D.Physics.Bepu` and
   `Nova3D.UI.Gum` to `0.3.0` when used. Do not mix Nova3D package versions.
2. Install `Nova3D.Cli` as a separate .NET tool if distributed validation,
   scene/model inspection, visual comparison or publishing is needed. It is not
   a game runtime dependency.
3. Existing code-first games can retain their `Game` loop, MonoGame types and
   existing static glTF rendering path. The scene, input, audio and persistence
   systems are opt-in; no scene conversion is required.
4. If adopting skinned glTF, use one `GltfSkeletonPose` and
   `GltfAnimationPlayer` per animated instance and pass the pose to
   `GltfModelRenderer`. The 48-joint limit applies per rendered primitive.
5. For generated projects, reinstall the `0.3.0` template and create a fresh
   project to compare starter layout; do not overwrite an existing game's
   authored files. The starter UI is not a finished game presentation.

No intentionally removed or renamed 0.2 public API is recorded for this
release. This is a source-migration statement, not a binary compatibility
guarantee: rebuild consumers and run their own Release and visual checks.

## Persisted and automation formats

The package version and a document's schema version are independent. The
following versions remain `1` in Nova3D 0.3:

| Format | Version field | Contract and compatibility rule |
|---|---|---|
| `nova3d.scene` | `version: 1` | [scene schema](Schemas/nova3d.scene.1.schema.json); unknown scene versions are rejected. Prefabs also use scene/1. |
| `nova3d.visual-capture` | `version: 1` | [capture schema](Schemas/nova3d.visual-capture.1.schema.json); capture metadata accompanies a PNG. |
| `nova3d.visual-baseline` | `version: 1` | [baseline schema](Schemas/nova3d.visual-baseline.1.schema.json); compare requires a matching capture version and scene. |
| `nova3d.performance-report` | `version: 1` | [report schema](Schemas/nova3d.performance-report.1.schema.json). |
| `nova3d.performance-budget` | `version: 1` | [budget schema](Schemas/nova3d.performance-budget.1.schema.json). |
| CLI JSON result | `schemaVersion: 1` | [CLI schema](Schemas/nova3d-cli-result-v1.schema.json); use stable exit codes and `recommendedActions`. |
| `nova3d.data` | game-selected `version` | Persistence envelope; the game selects its own `type` and version and provides explicit migrations. |
| input binding profile | `version: 1` | [input contract](input.md); game owns save location and profile lifecycle. |

Do not change a document version merely because the NuGet version changes.
When changing one of these contracts, add a new schema/version, parser or
migration behavior, regression fixtures and documentation in the same change.
Do not reinterpret an existing version silently. For saved game data, register
an explicit migration; newer unknown versions must fail rather than partially
loading.

## Release verification

`eng/validate.ps1` checks coordinated versions, compiles the solution consumer
and shaders, runs CPU contracts, builds samples, audits all five package
archives, installs the CLI in isolation and builds six generated variants.
Visual and performance regression scripts require the controlled graphics
machine and are separate release evidence. See [validation.md](validation.md)
and [releasing.md](releasing.md).

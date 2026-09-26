# Nova3D AI quickstart

Read this file first when creating or changing a game built with Nova3D. It is
the compact operational contract; open topic documents only when the task needs
them.

Agents may use the repository-local `nova3d-game-development` skill to route
this context. This file remains the authoritative first read.

## Product boundary

Nova3D is a code-first 3D rendering/world toolkit over MonoGame, not an engine.
Keep using MonoGame `Game`, `GraphicsDevice`, `ContentManager`, input, audio,
`Texture2D`, `Effect`, vectors and matrices directly.

Do not add wrappers that only rename MonoGame types. Game-specific state and
rules stay in the game repository.

## Packages

| Package | Purpose | Optional |
|---|---|---|
| `Nova3D` | rendering, terrain, vegetation, assets and diagnostics | no |
| `Nova3D.Physics.Bepu` | fixed-step BEPU integration | yes |
| `Nova3D.UI.Gum` | Gum lifecycle, navigation and HUD helpers | yes |

Core `Nova3D` must never reference BEPU or Gum.

## Create a game

```powershell
dotnet new nova3d -n MyGame
dotnet new nova3d -n MyPhysicsGame --physics
dotnet new nova3d -n MyUiGame --ui
dotnet new nova3d -n MyFullGame --physics --ui
```

For local Nova3D development, install or refresh packages first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\install-template.ps1
```

## Expected lifecycle

```text
Initialize
  base.Initialize
  GumUiHost, when enabled

Update
  asset/hot-reload polling
  Gum host and screen stack
  gameplay input not captured by UI
  game simulation and fixed-step physics
  visible/render-data preparation

Draw
  shadows
  world/transparent passes
  HDR and post-processing resolve
  Gum UI

Unload
  UI screen stack and host
  world and GPU resources
```

Create, update and destroy GPU resources on the graphics thread in v0.1.

## Ownership

| Object | Owner / rule |
|---|---|
| `Content.Load` result | `ContentManager`; game must not dispose it |
| object created with `new` and `IDisposable` | creator disposes it |
| `Mesh` | owns its buffers unless `ownsBuffers: false` |
| `GltfModel` | owns imported meshes and textures |
| material / `GltfModelRenderer` | does not own effects, textures or model |
| `BepuPhysicsWorld` | owns simulation, shapes and dispatcher |
| `BepuBody` / static | remove from its physics world when no longer used |
| `GumUiHost` | owns Gum initialization; dispose on graphics thread |
| `GumUiScreenStack` | owns every pushed screen |

Rendering owns meshes/materials. Physics owns shapes/poses. Gameplay owns what
collisions, checkpoints, victory, defeat and UI actions mean.

## v0.1 hard limits

- CSM has exactly four cascades.
- Treat 16 pixel samplers as a portability limit.
- Terrain has at most four layers: Grass, Dirt, Rock and Sand.
- Each terrain layer uses `AlbedoHeight` and `NormalAoRoughness`.
- Terrain is chunk based; local edits rebuild affected chunks only.
- Streaming retain radius is greater than or equal to load radius.
- glTF supports `TRIANGLES`, `POSITION`, `NORMAL` and `TEXCOORD_0`.
- No skinning, animation, morph, sparse accessors, Draco or Meshopt in v0.1.
- glTF alpha `BLEND` is currently rendered opaque.
- Physics uses a fixed timestep; render delta is never passed to BEPU directly.
- UI uses direct Gum controls; do not create `NovaButton` or equivalent wrappers.

If a feature must exceed a limit, update implementation, regression coverage
and documentation together. Never silently work around the limit.

## Hot-path rules

- Do not allocate per instance per frame.
- Use spatial queries before large per-instance frustum loops.
- Use squared distance for LOD selection.
- Update dynamic instance buffers only for visible instances.
- Do not recreate HUD controls during update.
- Measure milliseconds, allocations, draw calls and triangles; FPS alone is not
  evidence and CPU timing is not GPU timing.

## Assets and folders

```text
Content/          MGCB-compiled assets loaded by ContentManager
Assets/Models/    runtime GLB/glTF and external images
Assets/Textures/  runtime textures
Assets/Audio/     game audio not compiled through MGCB
Shaders/          explicit shader sources copied to output
```

Prefer GLB. Preserve external images when a glTF references them. Load and swap
hot-reloaded assets transactionally: create the replacement successfully before
discarding the last valid asset.

Nova3D does not wrap audio. Use MonoGame `SoundEffect`, `Song` and `MediaPlayer`.

## Input and UI

Update `GumUiHost` before routing gameplay input and draw it after the final
world resolve. Do not forward a captured device to gameplay. Give navigable
screens an `InitialFocus`; read Back from `GumUiHost.Navigation` so Escape and
gamepad B do not fire through separate paths.

Use anchors for resize. Apply accessibility hit targets after explicit control
dimensions. Validate theme foreground/surface contrast.

## Diagnose before fixing

1. Reproduce with the smallest scene.
2. Capture logs, exception, screenshot or measured statistics.
3. Identify owner: Game, Documentation, Nova3D.Core, Physics, UI, Templates or
   Upstream.
4. Use existing debug output before tuning constants.
5. Fix the owner, add a regression check and retest the consuming game.

Never hide a normal, matrix, shadow or coordinate bug by increasing coverage,
bias, draw distance or other constants.

Use `Docs/NOVA3D_EVALUATION_TEMPLATE.md` for reusable-toolkit problems.

## Required validation

For the Nova3D repository:

```powershell
.\eng\validate.ps1
```

For a consuming game, from the Nova3D repository:

```powershell
.\eng\validate.ps1 -GameProject C:\Games\MyGame\MyGame.csproj
```

When relevant, also compile changed shaders through MGCB, run the matching
benchmark, test the executable publish and report anything not visually tested.
See `Docs/validation.md` for focused options and validation boundaries.

## Load more context only when needed

| Task | Document |
|---|---|
| common implementation recipe | `Docs/Recipes/README.md` |
| public type, package or ownership | `Docs/API_INDEX.md` |
| project lifecycle, audio, publish | `Docs/game-project-guide.md` |
| architecture or public API | `Docs/architecture.md`, `Docs/AI_GUIDE.md` |
| rendering and frame passes | `Docs/rendering.md` |
| shaders or sampler changes | `Docs/shaders.md`, `Docs/materials.md` |
| terrain or streaming | `Docs/terrain.md` |
| GLB/runtime assets | `Docs/GltfImport.md`, `Docs/AssetManagement.md` |
| BEPU physics | `Docs/physics.md` |
| Gum UI | `Docs/ui.md` |
| performance regression | `Docs/performance.md` |
| build/package/template validation | `Docs/validation.md` |
| failure diagnosis | `Docs/troubleshooting.md` |
| packages/releases | `Docs/releasing.md` |

## Definition of done

- requested behavior works in the consuming game;
- Release build succeeds with no new warnings;
- ownership and optional-module boundaries remain correct;
- relevant test/benchmark passes;
- changed behavior and limits are documented;
- visual or platform validation not performed is stated explicitly.

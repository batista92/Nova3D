# Game project guide

This guide describes the supported v0.1 shape of a game that consumes Nova3D.
It intentionally keeps MonoGame visible.

For copy-sized implementation paths, use the
[recipe index](Recipes/README.md). Recipes cover lifecycle, GLB scenes, rolling
sphere physics, game flow/UI, persistence/audio and Windows publishing.

## Create the project

```powershell
dotnet new nova3d -n Marble3D --physics --ui
cd Marble3D
dotnet run
```

Use only the options the game needs:

| Option | Adds |
|---|---|
| none | `Nova3D` and a small 3D scene |
| `--physics` | `Nova3D.Physics.Bepu` and fixed-step sample |
| `--ui` | `Nova3D.UI.Gum` and a Gum overlay |
| `--physics --ui` | both optional modules |

## Ownership and lifecycle

`Game/MyGame.cs` owns MonoGame lifecycle services. `Game/World.cs` owns scene
resources. Optional UI owns only its Gum integration and visual tree.

The expected frame order is:

```text
Initialize
  MonoGame base initialization
  Gum host (when enabled)

Update
  file/hot-reload polling
  Gum input and UI
  gameplay input not captured by UI
  simulation/physics fixed-step update
  render-data preparation

Draw
  shadows and world
  HDR/post-processing resolve
  Gum UI

Unload
  UI visual tree and Gum host
  world/GPU resources
```

Create and dispose GPU resources on the graphics thread. Dispose objects created
with `new`; do not dispose assets owned by MonoGame `ContentManager`.

## Project folders

```text
Assets/          runtime files copied beside the executable
  Models/        prefer GLB
  Textures/      external runtime textures
  Audio/         runtime audio files when not using MGCB
Content/         MGCB project and compiled content
Game/            gameplay and game-owned presentation
Shaders/         explicit HLSL/MGFX sources
Docs/            Nova3D rules copied by the template
```

Use `Content/` for assets compiled by MGCB and load them through
`ContentManager`. Use `Assets/` for runtime loaders such as the GLB importer.
The project file copies `Assets/**` and `Shaders/**` to the output directory.

## Physics boundary

Physics owns collision shapes and simulated poses. Rendering owns meshes and
materials. Read `BepuBody.WorldMatrix` at the boundary; do not make a render
mesh the owner of a physics body. Gameplay owns controls, checkpoint rules,
respawning, victory and defeat.

## UI boundary

Use Gum controls directly. The game owns menu text and button actions.
`GumUiHost` owns initialization, scaling and input routing; `GumUiScreenStack`
owns pushed screens. Draw Gum after the world is resolved to the back buffer.

## Audio

Nova3D v0.1 does not wrap audio. Use MonoGame `SoundEffect`,
`SoundEffectInstance`, `Song` and `MediaPlayer` directly. Game code owns volume,
music/SFX categories and persistence. Add a Nova3D audio module only if a real
game reveals reusable behavior beyond MonoGame's API.

## Configuration and saves

Toolkit configuration covers renderer and streaming defaults. Game settings,
input bindings, level progress and checkpoint state belong to the game. Store
writable data in a platform-appropriate user-data directory, never beside
read-only packaged content.

## Build an executable

First validate a normal Release build:

```powershell
dotnet build -c Release
```

Then publish for a concrete runtime, for example Windows x64:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The executable is written below `bin/Release/net8.0/win-x64/publish`. Verify on
a clean machine or VM that `Content/`, runtime `Assets/`, shaders and native
MonoGame dependencies are present. Do not treat a successful compile as a
successful distributable build.

## Before reporting a toolkit issue

1. Reproduce in Release.
2. Record OS, GPU, backend, resolution and package versions.
3. Decide whether the behavior persists without game-specific code.
4. Capture the exception/log, screenshot or measured statistics.
5. Fill in the evaluation template and identify the suspected owner.

---
name: nova3d-game-development
description: Build, modify, diagnose, validate, and publish games that use Nova3D over MonoGame. Use for Nova3D rendering, terrain, GLB assets, BEPU physics, Gum UI, performance, project structure, or releases; do not use for MonoGame projects that do not use Nova3D.
---

# Nova3D game development

Work with Nova3D as a toolkit over MonoGame. Preserve the game's intent and
load only the context needed for the current task.

## Start

Locate the project root containing `AGENTS.md` or `Docs/AI_QUICKSTART.md`.
Read `Docs/AI_QUICKSTART.md` completely before editing. Treat its v0.1 limits,
ownership rules and module boundaries as constraints.

Classify the requested work before loading more context:

| Work | Read next |
|---|---|
| known implementation task | `Docs/Recipes/README.md`, then one recipe |
| choose or understand a public type | `Docs/API_INDEX.md` |
| lifecycle, persistence, audio, publish | `Docs/game-project-guide.md` |
| rendering/frame passes | `Docs/rendering.md` |
| shaders/materials | `Docs/shaders.md`, `Docs/materials.md` |
| terrain/streaming/vegetation | `Docs/terrain.md` |
| GLB/runtime assets | `Docs/GltfImport.md`, `Docs/AssetManagement.md` |
| physics | `Docs/physics.md` |
| UI/menu/HUD | `Docs/ui.md` |
| performance | `Docs/performance.md` |
| failure diagnosis | `Docs/troubleshooting.md` |
| public API or architecture change | `Docs/architecture.md`, `Docs/AI_GUIDE.md` |

In a Nova3D source checkout, use `Samples/README.md` to find the smallest
compilable example. Do not load every sample or topic document preemptively.

## Decide the owner

Keep gameplay, simulation rules, screens and content in the game. Change a
Nova3D package only for reusable behavior with a minimal reproduction.

Use these owners when diagnosing: `Game`, `Documentation`, `Nova3D.Core`,
`Nova3D.Physics.Bepu`, `Nova3D.UI.Gum`, `Nova3D.Templates`, or `Upstream`. For a
suspected toolkit defect, fill `Docs/NOVA3D_EVALUATION_TEMPLATE.md` before
changing the package.

Do not:

- wrap MonoGame types merely to rename them;
- move game-specific behavior into Nova3D;
- make core Nova3D depend on BEPU or Gum;
- hide normal, matrix, shadow, coordinate or culling defects by tuning coverage;
- exceed a documented limit without changing implementation, regression
  coverage and documentation together.

## Implement

Search existing APIs and samples before adding a type. Keep rendering and
simulation separate, preserve documented resource ownership and create/destroy
GPU resources on the graphics thread.

Use direct MonoGame input/audio/content APIs. Use the optional physics and UI
packages only when the game requests those capabilities. Critical snippets
should become a compilable sample or regression check when changing Nova3D.

## Validate

For a Nova3D source checkout, run:

```powershell
.\eng\validate.ps1
```

For a consuming game, use the toolkit checkout when available:

```powershell
.\eng\validate.ps1 -GameProject C:\path\to\Game.csproj
```

Otherwise run `dotnet build -c Release` in the game. Also perform the matching
benchmark, shader/MGCB compilation, publish or interactive check when relevant.
Never report a build as visual, input, audio, GPU-timing or platform validation.

## Report

Lead with the resulting behavior. State changed ownership/API/limits, commands
that passed, and any visual or platform checks not performed. If blocked, give
the smallest reproduction and identify the unresolved owner rather than
guessing at constants.

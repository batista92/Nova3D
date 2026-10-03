# Package boundaries

This document records why a reusable system belongs to the mandatory `Nova3D`
package or an optional package. It prevents package splits based only on feature
names or speculative engine architecture.

## Current packages

| Package | Boundary | Reason |
|---|---|---|
| `Nova3D` | Rendering, world, assets, scenes, input, audio coordination and persistence | Foundation with no dependency beyond MonoGame/BCL. |
| `Nova3D.Physics.Bepu` | BEPU simulation integration | Optional third-party backend and native gameplay choice. |
| `Nova3D.UI.Gum` | Gum lifecycle/navigation/HUD integration | Optional third-party UI backend and game presentation choice. |
| `Nova3D.Cli` | Distributed project validation and diagnostics | Separate `DotnetTool`; used by games but not referenced by runtime code. |

The package archive audit in `eng/validate.ps1` enforces this graph. The core
package has exactly one NuGet dependency: `MonoGame.Framework.DesktopGL`.
Physics and UI may depend on core; core cannot depend on either optional module.

## G6.3 decision: audio and persistence stay in core

Status: accepted on 2026-10-02 for the next Nova3D minor version.

Evidence was collected from two runnable game consumers:

| Consumer | Repeated behavior found | Core behavior used/provided |
|---|---|---|
| External `Marble3D` | Nine SFX, shared volume, event playback, LocalApplicationData path, temporary replacement, fallback and inline settings migration | Audio buses/pools plus platform paths, atomic backup, validation and migration. |
| `Samples/InputActions` | Local user-data path, versioned binding JSON, file error fallback and restart persistence | `GameDataPaths` and `VersionedJsonStore<JsonElement>` around `InputBindingJson`. |

`Marble3D` remains an external historical consumer and was inspected read-only;
G6.3 does not rewrite its game code. `InputActions` is the in-repository
standalone MonoGame sample and regression consumer.

Keeping both systems in core is deliberate:

- Audio uses the MonoGame audio types already present in every Nova3D game and
  adds no backend/package dependency.
- Persistence uses only the .NET base class library and is required by settings,
  remappable controls and save slots regardless of genre.
- A separate package would add installation, versioning and AI-routing cost
  without removing a dependency from the core package.
- Both APIs preserve game-owned types and rules instead of introducing an
  engine-level gameplay model.

## Boundary: reusable versus game-owned

Nova3D core may own:

- audio bus math, bounded native voice reuse, fades, 3D updates and focus
  lifecycle;
- user-data path containment, atomic replacement, backup recovery, version
  envelopes and migration hooks.

The game must own:

- which event plays which sound, procedural tone generation, playlists and
  gameplay music states;
- settings/progress records, field validation, slot meaning and migration
  content;
- when saves occur and how recovery is communicated to the player.

No core type may reference a game namespace, game state enum, level identifier,
checkpoint, score, victory/defeat rule or UI screen. The NuGet dependency audit
and focused contracts run on every repository validation.

## Reconsider the decision when

Move audio to an optional package only if it acquires a non-MonoGame backend,
native dependency, separate platform support matrix or independently versioned
release cadence. Move persistence only if it acquires cloud/provider SDKs or a
backend dependency. Those integrations should normally become new optional
packages while the current core contracts remain backend-neutral.

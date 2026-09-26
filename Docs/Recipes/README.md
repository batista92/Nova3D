# Nova3D recipes

Recipes are short implementation paths, not new framework abstractions. Load
only the recipe required by the current task.

Every recipe follows this structure:

1. **Use when** — scope and prerequisites.
2. **Files** — expected game-owned files.
3. **Implementation** — minimal code using public APIs.
4. **Ownership** — who creates and disposes resources.
5. **Validate** — observable completion checks.
6. **Common failures** — high-value diagnostics.

Current recipes:

| Goal | Recipe |
|---|---|
| create a game and preserve lifecycle | [create-game-lifecycle.md](create-game-lifecycle.md) |
| load/render a GLB in a PBR scene | [glb-material-scene.md](glb-material-scene.md) |
| rolling sphere and follow camera | [rolling-sphere-camera.md](rolling-sphere-camera.md) |
| menu, timer, checkpoint and end states | [game-flow-ui.md](game-flow-ui.md) |
| audio, settings and save data | [audio-settings-saves.md](audio-settings-saves.md) |
| produce a Windows executable | [publish-windows.md](publish-windows.md) |

Keep each recipe below 150 lines. Critical rendering, physics, UI and game-flow
patterns also exist as compilable projects under the repository's `Samples/`
directory.

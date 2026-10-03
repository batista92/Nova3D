# Nova3D agent router

Start with `Docs/AI_QUICKSTART.md`. It contains the supported lifecycle,
ownership table, v0.2 limits, validation commands and topic routing.

When building a game, read `GAME_DESIGN.md` and
`Docs/Recipes/game-presentation.md` before implementing menus, HUD, settings or
interactive objects. Fill the brief from the game's genre and actual assets.
Before calling the game complete, record rendered screenshots and interaction
checks in `PRESENTATION_REVIEW.md`. The template UI is a technical starter, not
the game's finished visual identity.

Load only the document required by the task:

- rendering/frame passes: `Docs/rendering.md`;
- shaders/materials: `Docs/shaders.md` and `Docs/materials.md`;
- terrain/streaming: `Docs/terrain.md`;
- scene documents/authoring: `Docs/scenes.md`;
- GLB/assets: `Docs/GltfImport.md` and `Docs/AssetManagement.md`;
- physics: `Docs/physics.md`;
- UI: `Docs/ui.md`;
- audio: `Docs/audio.md`;
- persistence/settings/saves: `Docs/persistence.md`;
- performance: `Docs/performance.md`;
- packaging: `Docs/releasing.md`.
- package/module boundaries: `Docs/package-boundaries.md`.
- CLI installation and commands: `Docs/cli.md`.

Read `Docs/architecture.md` and the full `Docs/AI_GUIDE.md` before changing
public API, module boundaries, ownership or a documented architecture limit.

For a suspected Nova3D defect, use `Docs/NOVA3D_EVALUATION_TEMPLATE.md` and
reproduce before editing. Keep game-specific behavior in the game repository.

If a task exceeds a documented v0.2 limit, update implementation, regression
coverage and documentation together. Do not silently work around constraints.

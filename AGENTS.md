# Nova3D agent router

Start with `Docs/AI_QUICKSTART.md`. It contains the supported lifecycle,
ownership table, v0.1 limits, validation commands and topic routing.

Load only the document required by the task:

- rendering/frame passes: `Docs/rendering.md`;
- shaders/materials: `Docs/shaders.md` and `Docs/materials.md`;
- terrain/streaming: `Docs/terrain.md`;
- GLB/assets: `Docs/GltfImport.md` and `Docs/AssetManagement.md`;
- physics: `Docs/physics.md`;
- UI: `Docs/ui.md`;
- performance: `Docs/performance.md`;
- packaging: `Docs/releasing.md`.

Read `Docs/architecture.md` and the full `Docs/AI_GUIDE.md` before changing
public API, module boundaries, ownership or a documented architecture limit.

For a suspected Nova3D defect, use `Docs/NOVA3D_EVALUATION_TEMPLATE.md` and
reproduce before editing. Keep game-specific behavior in the game repository.

If a task exceeds a documented v0.1 limit, update implementation, regression
coverage and documentation together. Do not silently work around constraints.

# Nova3D Architecture Rules

This file is the operational contract for AI-assisted changes. Follow it before
general engine conventions or speculative abstractions.

## Identity

- Nova3D is a toolkit over MonoGame, not a replacement engine.
- Do not create wrappers around MonoGame types without new behavior or ownership.
- Use `GraphicsDevice`, `Texture2D`, `Effect`, `Vector2`, `Vector3`, `Quaternion`,
  `Matrix`, `BoundingBox` and `BoundingFrustum` directly.
- Do not introduce `NovaTexture`, `NovaVector`, `NovaMatrix` or equivalent aliases.
- Do not add ECS, service locators, scene graphs or dependency containers without
  a concrete game requirement.

## Boundaries

- Rendering and simulation must remain separate.
- Nova3D must never reference CityBuilder gameplay, benchmarks or test scenes.
- Game code owns gameplay state; renderers consume render data.
- MonoGame owns the platform/game loop. Do not wrap `Game` or `GraphicsDevice`.
- GPU resources are created and destroyed on the graphics thread in v0.1.

## Ownership

- Every GPU resource must have one documented owner.
- Dispose resources created with `new`; do not dispose ContentManager assets.
- `GltfModel` owns imported meshes and textures.
- `GltfModelRenderer` and materials do not own models, effects or textures.
- A hot reload must create the replacement successfully before swapping it.
- Never discard the last valid asset when reload fails.

## Terrain

- Terrain is chunk based.
- Terrain rendering uses `TerrainMaterial` and `TerrainLayerSet`.
- Runtime deformation rebuilds affected chunks only.
- Terrain, placement and vegetation must sample the same height provider.
- Terrain supports a maximum of four layers in v0.1: Grass, Dirt, Rock and Sand.
- Each terrain layer uses `AlbedoHeight` plus `NormalAoRoughness`.
- `AlbedoHeight`: RGB albedo, A height.
- `NormalAoRoughness`: RG normal, B AO, A roughness.
- Do not use texture atlases for repeating terrain materials.
- LOD distances must be ascending and match the number of LOD meshes.
- Streaming retain radius must be greater than or equal to load radius.

## Shaders and materials

- Treat 16 pixel samplers as a hard portability limit.
- `LargeTerrain.fx` currently uses 12 samplers; four remain.
- `PBR.fx` currently uses 11 samplers; five remain.
- Do not add samplers without documenting the new budget.
- Shader parameter names are API; update C# bindings in the same change.
- Preserve DesktopGL and DirectX shader macros and profiles.
- Perform lighting in linear space and gamma conversion once.
- Do not gamma-correct normal, AO, roughness, metallic, height or shadow data.
- Use inverse-transpose matrices for normals under non-uniform scale.
- Check winding and cull state before changing normals.
- Materials do not own effects or textures.
- Use `Func<Effect>` material constructors for shader hot reload.

## Rendering

- Call `RenderContext.BeginFrame(camera)` once per rendered frame.
- Restore graphics states explicitly between passes.
- Apply every pass in the current effect technique before drawing.
- Register draws and triangles in `RenderStatistics`.
- Shadow draws must be recorded with `shadow: true`.
- CSM has exactly four cascades in v0.1.
- Diagnose shadows with cascade/debug output before tuning distances or bias.
- Do not mask a coordinate, normal or matrix bug by increasing shadow coverage.

## Instancing, culling and LOD

- Use instancing for repeated meshes.
- Use spatial queries before per-instance frustum checks at large counts.
- Culling bounds must match the rendered object and its transform.
- LOD selection uses squared distance; do not add square roots in hot loops.
- Dynamic instance buffers are updated only for visible instances.
- Do not allocate per instance per frame.

## Assets

- Prefer GLB for runtime models and hot reload.
- glTF 2.x is supported; FBX and STOVE are not supported.
- Supported geometry mode is `TRIANGLES`.
- Supported attributes are POSITION, NORMAL and TEXCOORD_0.
- Sparse accessors, skinning, animations, morph targets, Draco and Meshopt are
  not supported in v0.1.
- glTF alpha `BLEND` is currently rendered opaque.
- Preserve external images when a GLB references them; not every GLB is fully embedded.

## Performance

- Measure before optimizing.
- FPS alone is not evidence; use milliseconds and render statistics.
- Never call a CPU timing a GPU timing.
- Preserve the CityBenchmark when changing renderer behavior.
- Do not increase draw distance, shadow resolution or streaming radii to hide a bug.
- Do not rebuild the whole terrain for a local edit.
- Do not create GPU objects or large temporary arrays every frame.

## Physics

- Physics is optional and lives in `Nova3D.Physics.Bepu`; core Nova3D must not
  reference BEPU.
- Use a fixed timestep. Rendering frame time must not be passed directly to
  `Simulation.Timestep`.
- BEPU uses `System.Numerics`; convert only at the physics/rendering boundary.
- Physics owns simulation handles and collision shapes; rendering owns meshes.
- Removing a body or static must also release its collision shape.
- Terrain colliders follow terrain chunk ownership and must be rebuilt only for
  affected chunks after deformation.
- Gameplay owns the meaning of collisions. Do not put game-specific collision
  rules in the reusable physics module.
- `KinematicCharacterController.Move` receives displacement, not velocity.
- Keep input, gravity, jumping and character state in gameplay code; the reusable
  controller only owns collision queries and sweep-and-slide movement.
- Access `BepuPhysicsWorld.Simulation` directly for advanced BEPU features
  instead of duplicating the complete BEPU API.

## UI

- Game UI is optional and uses Gum; core Nova3D must not reference Gum.
- Use Gum Forms controls directly. Do not create wrappers such as `NovaButton`,
  `NovaLabel` or `NovaPanel`.
- Update Gum once per frame and draw it after the HDR scene is resolved.
- Gameplay owns UI content and actions; reusable integration owns lifecycle,
  scaling, input routing and screen navigation only.
- Register keyboard and gamepads explicitly and provide an initial focused
  control when controller navigation is expected.
- Preserve anchors and usable hit targets when the window is resized.
- UI timings are CPU timings. Measure managed allocations separately.
- `GumUiScreenStack` owns every screen after `Push`; do not reuse or manually
  dispose a pushed screen.
- Only the top UI screen updates. Use `CoversPrevious = false` for a modal that
  leaves the previous screen visible but inactive.
- Keep HUD controls alive. Use `GumValueBinding<T>` or an equivalent change
  check instead of recreating controls or rewriting unchanged values per frame.
- `GumWorldMarker` converts viewport pixels to Gum canvas coordinates; do not
  position markers with back-buffer pixels when virtual scaling is enabled.
- Preallocate transient HUD controls. `GumNotificationQueue` reuses a fixed
  label pool and intentionally replaces the oldest slot when full.
- `GumUiTheme` styles Gum V3 visuals in place; it is not a control factory and
  must not own or dispose controls. Custom visuals remain styled by game code.
- Gum owns generated/loaded fonts. `GumFontStyle` only assigns font properties
  and must not cache or dispose Gum font resources.
- Read UI back navigation from `GumUiHost.Navigation`; do not create separate
  Escape and gamepad-B paths that can fire twice in one frame.
- Apply accessibility hit targets after setting explicit control dimensions.
  Theme foreground/surface contrast must satisfy the configured ratio.
- `ReducedMotion` is a user preference consumed by screen/game animations; it
  must not disable input feedback or functional state changes.
- Template UI code must remain behind the `ui` template symbol. The default
  `dotnet new nova3d` output must not reference Gum or `Nova3D.UI.Gum`.

## Change protocol

For an external game, first read `Docs/README.md`, `Docs/game-project-guide.md`
and the topic document for the module being changed. Record suspected toolkit
defects using `Docs/NOVA3D_EVALUATION_TEMPLATE.md`. Do not move game-specific
rules into Nova3D merely to share code inside one game.

Before editing:

1. Read the relevant document in `Docs/`.
2. Locate the current owner and call site with code search.
3. Reproduce or instrument bugs before tuning constants.
4. State any v0.1 limitation the change intends to exceed.

Before completing:

1. Build Release.
2. Compile changed shaders through MGCB.
3. Run the relevant scene or benchmark.
4. Check resource disposal and graphics-thread ownership.
5. Update docs when behavior, limits or sampler counts changed.
6. Report what was verified and what was not visually verified.

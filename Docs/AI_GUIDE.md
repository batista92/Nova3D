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
- Nova3D packages must never reference benchmark or game-specific scenes.
- Game code owns gameplay state; renderers consume render data.
- MonoGame owns the platform/game loop. Do not wrap `Game` or `GraphicsDevice`.
- GPU resources are created and destroyed on the graphics thread in v0.2.
- Audio coordination and persistence stay in core while they require only
  MonoGame/BCL. Do not split packages without satisfying the evidence and
  reconsideration criteria in `Docs/package-boundaries.md`.

## Ownership

- Every GPU resource must have one documented owner.
- Dispose resources created with `new`; do not dispose ContentManager assets.
- `GltfModel` owns imported meshes and textures.
- `GltfModelRenderer` and materials do not own models, effects or textures.
- A hot reload must create the replacement successfully before swapping it.
- Never discard the last valid asset when reload fails.

## Scenes

- Parse, validate and build a `SceneLoadPlan` before allocating runtime resources.
- Register component descriptors explicitly; never scan assemblies or use a
  service locator to resolve scene dependencies.
- Use `ISceneRuntimeComponentDescriptor` only when a component creates a runtime
  value, and implement both its creation and destruction paths.
- Construct `SceneInstantiator` on the graphics thread. Instantiate, rollback and
  dispose its `SceneInstance` on that same thread.
- Let `SceneInstance` own created component values. Do not dispose them separately.
- Keep parsing, validation and hierarchy resolution independent of
  `GraphicsDevice`, ContentManager, physics and UI modules.
- Use the built-in component property names documented in `Docs/scenes.md` and
  reject unknown fields instead of silently ignoring agent mistakes.
- Treat the node world matrix as authoritative for built-in camera, light,
  model and spawn transforms. Gameplay alone interprets tags and spawn kinds.
- Keep physics scene descriptors in the optional physics module or game; core
  Nova3D must not acquire a BEPU dependency.
- Resolve persisted asset references with `SceneAssetResolver`; never concatenate
  an unchecked scene string with the checkout or output directory.
- Use a `SceneAssetLease<T>` when sharing cached scene assets. Dispose scenes
  before their cache and never dispose the leased value directly.
- Pass cancellation through runtime factories and let `SceneInstantiator`
  perform rollback; do not create GPU resources on a background task.
- Use `ScenePrefabLoader.Prepare` for scenes with `nova3d.prefab`. Keep prefab
  expansion, override checking and recursive-reference detection on the CPU.
- Address prefab override targets by IDs local to the referenced file. Only
  override existing typed component properties or local transform vectors.
- Use `SceneDebugVisualization` with `DebugRenderer` for node/model bounds; the
  game draws text for the provided world-space name anchors.
- Let `SceneService` own the active instance and dispose it before shared caches
  and physics worlds. `SceneFlowController` only coordinates reusable phases;
  the game owns victory/defeat rules and UI content.
- Keep Gum as a `StateChanged` observer. Use optional `BepuSceneFlowAdapter` to
  step physics only in Playing; scene descriptors remove their own bodies.

## Terrain

- Terrain is chunk based.
- Terrain rendering uses `TerrainMaterial` and `TerrainLayerSet`.
- Runtime deformation rebuilds affected chunks only.
- Terrain, placement and vegetation must sample the same height provider.
- Terrain supports a maximum of four layers in v0.2: Grass, Dirt, Rock and Sand.
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
- CSM has exactly four cascades in v0.2.
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
- Supported attributes are POSITION, NORMAL, TEXCOORD_0, JOINTS_0 and WEIGHTS_0.
- Skin and TRS animation data with LINEAR/STEP interpolation is imported.
  Create one `GltfSkeletonPose` and `GltfAnimationPlayer` per animated instance
  and pass that pose to `GltfModelRenderer` for PBR and shadow drawing.
- Sparse accessors, second influence sets, CUBICSPLINE, morph targets, Draco
  and Meshopt are not supported.
- G5's selected skinning transport is a uniform `float4x4` palette with at most
  48 joints per rendered primitive and zero added samplers. A larger skin must
  use per-primitive local palette remapping or fail explicitly; do not silently
  truncate influences or joints.
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

## Input

- Build `InputActionMap` bindings during setup and update once per game frame.
- Pass native MonoGame keyboard, mouse and gamepad states; do not hide them.
- Read gameplay intentions from named actions, not repeated key checks inside
  simulation code. Normalize combined axis values in the game when needed.
- Route gameplay/menu/debug actions through `InputContextRouter` when they
  coexist. Make modal contexts block lower priorities; activate them explicitly.
- Update Gum before the router and pass `GumInputCapture.Read(ui)` to filter
  captured devices. Core input must remain independent of Gum.
- Do not synthesize gameplay presses when capture or context ownership changes:
  held controls must return to neutral before they can trigger again.
- Capture default bindings before loading user settings. Remap with
  `ReplaceBindings` so existing action references remain valid, inspect
  `FindConflicts`, and serialize maps with `InputBindingJson` outside the frame
  hot path. The game owns save paths, Gum controls and conflict policy.

## Audio

- Preserve MonoGame `SoundEffect`, `SoundEffectInstance`, `Song`,
  `AudioListener`, `AudioEmitter` and `MediaPlayer` in public game code.
- Use `AudioSystem` only for reusable behavior: hierarchical buses, fades,
  bounded voice pools, 3D updates and focus lifecycle.
- ContentManager owns loaded audio assets. `AudioSystem` owns only instances
  created by its pools and its exclusive global MediaPlayer session.
- Keep one music owner per process because `MediaPlayer` is global.
- Do not load audio or grow voice collections in `Update`; choose fixed pool
  capacities and an explicit reject/steal policy.
- Gameplay owns which sounds play and when. Nova3D must not infer events,
  music states or pause rules from scenes.

## Persistence

- Use `GameDataPaths` instead of writing beside the executable. Persisted
  studio, game and slot identifiers must be safe single path segments.
- Keep settings, progress and save-slot records game-owned and free of GPU,
  physics, Gum and runtime scene objects.
- Use `VersionedJsonStore<T>` for an explicit document type/version, atomic
  replacement and last-known-good backup recovery.
- Add an explicit migration before increasing a persisted version. Reject
  future versions and never partially apply invalid data.
- Validate semantic ranges inside the store transaction so invalid primary
  data can fall back to backup before reaching gameplay.
- Inspect `PersistenceLoadResult.Source`; log and communicate recovery when it
  affects player progress.
- Synchronous file I/O stays outside `Update`. Game code coordinates any
  background save and prevents concurrent writes to the same path.

## Change protocol

For an external game, first read `Docs/README.md`, `Docs/game-project-guide.md`
and the topic document for the module being changed. Record suspected toolkit
defects using `Docs/NOVA3D_EVALUATION_TEMPLATE.md`. Do not move game-specific
rules into Nova3D merely to share code inside one game.

Before editing:

1. Read the relevant document in `Docs/`.
2. Locate the current owner and call site with code search.
3. Reproduce or instrument bugs before tuning constants.
4. State any v0.2 limitation the change intends to exceed.

Before completing:

1. Build Release.
2. Compile changed shaders through MGCB.
3. Run the relevant scene or benchmark.
4. Check resource disposal and graphics-thread ownership.
5. Update docs when behavior, limits or sampler counts changed.
6. Report what was verified and what was not visually verified.

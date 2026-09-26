# Nova3D public API index

Use this catalog to locate the supported public type before searching source.
It describes intent and lifetime, not every member overload. MonoGame types stay
visible and are not duplicated by Nova3D.

Ownership shorthand:

- **value/config**: no disposal; the game owns the value;
- **owner**: dispose the instance when its documented lifetime ends;
- **borrower**: referenced resources remain owned by the caller or
  `ContentManager`;
- **conditional**: constructor flags decide whether a supplied resource is owned.

## Package `Nova3D`

### Rendering foundation

Read [rendering.md](rendering.md). See the
[GLB scene recipe](Recipes/glb-material-scene.md) for frame usage.

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `Camera3D` | View, projection, frustum and viewport state. | Game-owned state; no disposal. |
| `Mesh` | Indexed GPU mesh with 16/32-bit indices. | Owner; disposes buffers unless `ownsBuffers: false`. |
| `RenderContext` | Per-frame device, camera, statistics and profiler context. | Long-lived game object; borrows device/camera. Call `BeginFrame` once. |
| `RenderStatistics` | Draw, triangle, instance, chunk and shadow counters. | Owned by `RenderContext` or game; no disposal. |
| `DirectionalLight` | Directional light parameters. | Value-like game state; no disposal. |
| `ImageBasedLighting` | Environment textures and PBR intensities. | Borrower; textures retain their existing owner. |
| `Material` | Base contract that applies an `Effect` to a frame. | Borrower; never owns effect/textures. |
| `ForwardLitMaterial` | Forward directional-light material. | Borrower; effect and light remain caller-owned. |
| `PbrMaterial` | Metallic/roughness PBR material with CSM/IBL inputs. | Borrower; effect provider, textures, light and IBL remain external. |
| `GltfModelRenderer` | Draws imported instances/materials through PBR and shadows. | Owner of its created states/material objects; borrows model/effects/light/IBL. |
| `InstanceTransform` | GPU instance transform vertex. | Value type; no disposal. |
| `InstancedMeshBatch` | Spatially culled single-LOD instance batch. | Owner of instance buffer; conditionally owns mesh, default `true`. |
| `LodInstancedMeshBatch` | Spatial culling plus distance LOD for instances. | Owner of instance buffers; conditionally owns LOD meshes, default `true`. |
| `CascadedShadowMap` | Four-cascade directional shadow resources and matrices. | Owner of render targets; dispose on graphics thread. |
| `HdrPostProcessPipeline` | HDR target, bloom, FXAA and final composition. | Owner of targets and `SpriteBatch`; borrows effect. |
| `WaterMaterial` | Transparent animated water shader bindings. | Borrower; effect/textures are external. |
| `WaterRenderer` | Water mesh pass with explicit transparent states. | Conditionally owns mesh, default `true`; borrows material. |

Limits: CSM has exactly four cascades. Call `RenderContext.BeginFrame` once per
rendered frame. Materials do not own effects/textures. Treat 16 pixel samplers
as the portability limit; `PBR.fx` currently uses 11. LOD thresholds are
ascending and below cull distance. GPU resources stay on the graphics thread.

### Terrain, vegetation, spatial data and streaming

Read [terrain.md](terrain.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `IHeightProvider` | Shared terrain height sampling contract. | Implementer/game-owned; no disposal contract. |
| `DelegateHeightProvider` | Adapts a sampling delegate to `IHeightProvider`. | Value-like adapter; no disposal. |
| `IDeformableHeightProvider` | Height provider that reports edited regions. | Implementer/game-owned; no disposal contract. |
| `DeformableHeightProvider` | Editable height overlay over another provider. | Game-owned CPU data; borrows base provider. |
| `TerrainRegion` | World-space bounds of a terrain edit. | Value type. |
| `TerrainVertex` | Terrain GPU vertex layout. | Value type. |
| `TerrainLayer` | Named albedo-height and normal-AO-roughness texture pair. | Borrower; textures retain their owner. |
| `TerrainLayerSet` | Validated terrain layer collection. | Borrower; does not dispose layer textures. |
| `TerrainMaterial` | Binds layer set, lighting and terrain effect. | Borrower; no effect/texture ownership. |
| `ChunkedTerrainSettings` | Chunk, LOD, bounds and optional streaming configuration. | Config; no disposal. |
| `ChunkedTerrain` | Resident terrain chunks, mesh LOD, culling and local rebuilds. | Owner of generated chunk meshes; dispose on graphics thread. |
| `VegetationInstances` | Scatter transforms and enabled flags. | Game-owned CPU arrays. |
| `VegetationScatter` | Deterministic terrain-aware scatter helper. | Static utility. |
| `ConiferMeshFactory` | Creates placeholder conifer LOD meshes. | Static factory; caller owns returned meshes. |
| `VegetationSystem` | LOD-instanced vegetation culling and drawing. | Owner; disposes instance buffers and supplied LOD meshes. |
| `StaticSpatialGrid` | CPU grid for radius candidate queries. | Game/system-owned CPU data; no disposal. |
| `WorldCell` | Integer streaming cell coordinate. | Value type. |
| `WorldStreamingSettings` | Load/retain radius and per-update budgets. | Config; no disposal. |
| `WorldStreamer<T>` | Budgeted load/unload lifecycle for cells. | Owner of active-cell lifecycle; `Dispose` invokes unload callback. |

Limits: terrain is chunk based and supports at most four layers (`Grass`,
`Dirt`, `Rock`, `Sand`). Every layer uses `AlbedoHeight` plus
`NormalAoRoughness`; `LargeTerrain.fx` uses 12 samplers. Local edits rebuild
affected chunks only. Streaming retain radius must be at least load radius.
Terrain, placement, vegetation and physics must share one height provider.

### Runtime assets and glTF

Read [AssetManagement.md](AssetManagement.md) and
[GltfImport.md](GltfImport.md). Follow the
[GLB scene recipe](Recipes/glb-material-scene.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `AssetHandle<T>` | Stable reference to a versioned runtime asset. | Borrower; manager controls replacement/lifetime. |
| `FileAssetManager` | Polls and transactionally reloads file assets. | Owner when a dispose callback is registered; dispose manager on owning thread. |
| `ShaderHotReload` | Registers raw `.mgfxo` effects with the asset manager. | Static helper; resulting manager registration owns effect lifetime. |
| `GltfAssetExtensions` | Registers GLB/glTF models with `FileAssetManager`. | Static helper; manager owns registered model lifetime. |
| `GltfImporter` | Imports GLB/glTF into GPU resources. | Borrower of `GraphicsDevice`; returned `GltfModel` is caller-owned. |
| `GltfVertex` | Imported glTF GPU vertex layout. | Value type. |
| `GltfMaterial` | Imported material factors and optional textures. | Model-owned data; do not dispose textures separately. |
| `GltfPrimitive` | Mesh, material index and bounds. | Model-owned; its mesh is disposed by `GltfModel`. |
| `GltfInstance` | Named primitive instance and node transform. | Model-owned immutable data. |
| `GltfModel` | Imported primitives, materials and instances. | Owner of imported meshes/textures; caller or asset manager disposes it. |

Limits: only glTF 2.x `TRIANGLES`, `POSITION`, `NORMAL` and `TEXCOORD_0` are
supported. No sparse accessors, skinning, animation, morph targets, Draco or
Meshopt. Alpha `BLEND` renders opaque. Prefer GLB for reload; external `.gltf`
dependencies are not independently watched. Import/reload on the graphics
thread and never discard the last valid asset after a failed reload.

### Resources, configuration and diagnostics

Read [performance.md](performance.md) and [releasing.md](releasing.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `ResourceRegistration` | Description of a named Content resource. | Value type. |
| `ResourceLibrary` | Named access to `ContentManager` resources. | Borrower; never disposes Content assets. |
| `ShaderLibrary` | Named `Effect` access over `ResourceLibrary`. | Borrower; never disposes Content effects. |
| `Nova3DConfiguration` | Root file configuration model. | Config; no disposal. |
| `WindowConfiguration` | Window configuration section. | Config; no disposal. |
| `RendererConfiguration` | Renderer configuration section. | Config; no disposal. |
| `StreamingConfiguration` | Streaming configuration section. | Config; no disposal. |
| `ConfigurationLoader` | Loads and validates configuration files. | Static utility. |
| `DebugRenderer` | Accumulates and flushes debug lines/shapes. | Owner of internal `BasicEffect`; dispose on graphics thread. |
| `FrameProfiler` | Named CPU scope timings per frame. | Game/context-owned CPU state; no disposal. |
| `Scope` (`FrameProfiler.Scope`) | `using` scope that records elapsed CPU time. | Value scope; dispose at scope end. |
| `ILogger` | Nova3D logging sink contract. | Implementer defines lifetime. |
| `FileLogger` | Thread-safe file logger implementation. | Owner of file writer; dispose to flush/close. |
| `LogLevel` | Logging severity. | Enum. |

CPU profiler timings are not GPU timings. FPS alone is not regression evidence;
also report milliseconds, allocations, draws and triangles.

## Package `Nova3D.Physics.Bepu`

Read [physics.md](physics.md). Follow the
[rolling sphere recipe](Recipes/rolling-sphere-camera.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `PhysicsWorldOptions` | Gravity, fixed step and threading configuration. | Config; no disposal. |
| `BepuPhysicsWorld` | BEPU simulation, stepping, bodies, statics, queries and triggers. | Owner of simulation, dispatcher and registered shapes; dispose world. |
| `BepuBody` | Handle-backed body facade with MonoGame pose/velocity access. | Value facade; world owns handle/shape. Remove it through its world. |
| `CollisionFilter` | 32-layer membership and collision mask. | Value type. |
| `PhysicsMaterial` | Friction, recovery and contact spring parameters. | Value type. |
| `PhysicsRayHit` | Closest ray-query result. | Value type. |
| `ShapeCastHit` | Closest sphere/capsule sweep result. | Value type. |
| `TriggerEventType` | Enter, stay or exit classification. | Enum. |
| `TriggerPair` | Stable trigger/other collidable pair. | Value type. |
| `TriggerEvent` | Trigger pair plus event classification. | Value type. |
| `KinematicCharacterSettings` | Capsule, slope, iteration and filter configuration. | Config; no disposal. |
| `KinematicCharacterController` | Filtered capsule sweep-and-slide and ground probe. | Game-owned controller; world owns query shapes/resources. |
| `TerrainPhysicsSettings` | Terrain collider grid/streaming configuration. | Config; no disposal. |
| `TerrainPhysics` | Per-chunk triangle-mesh colliders from a height provider. | Owner of its static handles/shapes; dispose before world. |
| `BepuConversions` | Explicit MonoGame/System.Numerics conversions. | Static utility; use only at boundary. |
| `NarrowPhaseCallbacks` | BEPU contact/filter callback implementation. | Infrastructure value used by `BepuPhysicsWorld`. |
| `PoseIntegratorCallbacks` | BEPU gravity/integration callback implementation. | Infrastructure value used by `BepuPhysicsWorld`. |

Physics is optional; core `Nova3D` never references BEPU. Pass frame seconds to
`BepuPhysicsWorld.Update`; it owns fixed-step accumulation. `Move` on the
kinematic controller receives displacement, not velocity. Gameplay owns input,
gravity/jump policy and collision meaning. Use `Simulation` directly for
advanced BEPU features instead of wrapping the complete backend.

## Package `Nova3D.UI.Gum`

Read [ui.md](ui.md). Follow the
[game-flow/UI recipe](Recipes/game-flow-ui.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `GumUiHostOptions` | Scaling, input and accessibility configuration. | Config; no disposal. |
| `GumUiScalingMode` | Expand, height or width scaling policy. | Enum. |
| `GumUiInputMode` | Overlay or exclusive input capture. | Enum. |
| `GumUiAccessibilitySettings` | Text scale, target size, contrast and motion policy. | Config; no disposal. |
| `GumUiHost` | Gum initialization, frame lifecycle, resize, input and diagnostics. | Owner of Gum runtime lifecycle; dispose on graphics thread. |
| `GumUiNavigationInput` | Edge-based unified Back navigation state. | Host-owned state; no disposal. |
| `GumUiFrameStatistics` | UI CPU/update/draw/allocation snapshot. | Value type. |
| `GumUiScreen` | Direct Gum root plus navigation/lifecycle metadata. | Disposable screen; stack owns it after `Push`. |
| `GumUiScreenStack` | Push/pop visibility, focus and screen ownership. | Owner of every pushed screen; dispose stack to clear all. |
| `GumValueBinding<T>` | Applies HUD changes only when a value changes. | Game-owned helper; no disposal. |
| `GumWorldMarker` | Projects a world position into Gum canvas coordinates. | Game-owned helper; borrows existing control/viewport data. |
| `GumNotificationQueue` | Fixed pool of reusable notification labels. | Game-owned helper; screen owns visual tree/labels. |
| `GumUiTheme` | Applies visual tokens to existing Gum controls. | Borrower; never owns or creates controls. |
| `GumFontStyle` | Assigns Gum font family/size/style properties. | Value-like config; Gum owns font resources. |

UI is optional; core `Nova3D` never references Gum. Use Gum controls directly,
update the host before gameplay input and draw it after final world resolve.
After `Push`, never manually dispose or reuse a screen. Only the top screen
updates. Apply explicit dimensions before accessibility hit targets. UI timings
are CPU timings, not GPU timings.

## Choosing an API

| Goal | Start with |
|---|---|
| Build a normal frame | `Camera3D`, `RenderContext`, `RenderStatistics` |
| Load a static model | `GltfImporter`, `GltfModel`, `GltfModelRenderer` |
| Repeat many objects | `InstancedMeshBatch` or `LodInstancedMeshBatch` |
| Render/edit terrain | `ChunkedTerrain`, one shared `IHeightProvider` |
| Hot reload a runtime asset | `FileAssetManager`, `AssetHandle<T>` |
| Simulate a rolling object | `BepuPhysicsWorld`, `BepuBody` |
| Add menus/HUD | `GumUiHost`, `GumUiScreenStack`, direct Gum controls |
| Diagnose rendering | `DebugRenderer`, `RenderStatistics`, `FrameProfiler` |

If a required behavior is absent here, inspect the relevant topic document
before adding a public abstraction. Exceeding a v0.1 limit requires coordinated
implementation, regression coverage and documentation changes.

# Nova3D public API index

For package-version coordination, persisted format versions and the 0.2 to 0.3
migration path, see [compatibility-0.3.md](compatibility-0.3.md). The CLI is a
separate tool, not a runtime API; see [cli.md](cli.md).

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
| `GltfModelRenderer` | Draws static or posed glTF instances through PBR/shadows and exposes conservative animated bounds. | Owner of its created states/material objects; borrows model, optional pose, effects, light and IBL. |
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
| `GltfSkinnedVertex` | Imported four-influence vertex layout using primitive-local blend indices. | Value type. |
| `GltfMaterial` | Imported material factors and optional textures. | Model-owned data; do not dispose textures separately. |
| `GltfPrimitive` | Mesh, material index, bounds and optional local joint palette. | Model-owned; its mesh is disposed by `GltfModel`. |
| `GltfInstance` | Named primitive instance, node/skin indices and bind transform. | Model-owned immutable data. |
| `GltfNode` / `GltfSkin` | Imported hierarchy, bind TRS, joints and inverse bind matrices. | Model-owned immutable data. |
| `GltfAnimationClip` | Named TRS channels and LINEAR/STEP samplers. | Model-owned immutable data. |
| `GltfSkeletonPose` | Mutable local/world transforms and primitive-local skin palettes for one instance. | Game-owned CPU state; create one per independently animated instance. |
| `GltfAnimationPlayer` | Allocation-free clip playback, looping, speed, stop and two-clip cross-fade. | Game-owned CPU state; borrows model animation data and its pose. |
| `GltfSkeletonDebug` | Adds posed bones and joint crosses to `DebugRenderer`. | Static helper; owns no resources. |
| `GltfModel` | Imported geometry, scene hierarchy, skins and animation clips. | Owner of imported meshes/textures; caller or asset manager disposes it. |

Limits: only glTF 2.x `TRIANGLES` and the documented base/skinning attributes
are supported. Skin/animation data can be imported, evaluated and rendered.
No sparse accessors, second influence set,
CUBICSPLINE, morph targets, Draco or Meshopt. Alpha `BLEND` renders opaque. Prefer GLB for reload; external `.gltf`
dependencies are not independently watched. Import/reload on the graphics
thread and never discard the last valid asset after a failed reload.

### Scene documents

Read [scenes.md](scenes.md).

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `SceneDocument` | CPU-only versioned scene composition. | Immutable caller-owned data; no disposal. |
| `SceneNodeDocument` | Node ID, hierarchy reference, transform and components. | Immutable document-owned data; no disposal. |
| `SceneTransformDocument` | Local position, degree rotation and scale using MonoGame types. | Immutable document-owned data; no disposal. |
| `SceneComponentDocument` | Typed component envelope with detached JSON properties. | Immutable document-owned data; no disposal. |
| `SceneDocumentSerializer` | Strict UTF-8 parser and deterministic writer. | Static utility; no retained resources. |
| `SceneDocumentParseException` | Source path, JSON path and optional syntax location. | Exception value; no disposal. |
| `SceneDocumentValidator` | Aggregated CPU-only semantic validation. | Static utility; no retained resources. |
| `SceneValidationResult` | Deterministically ordered scene issues and validity state. | Immutable caller-owned result; no disposal. |
| `SceneValidationIssue` | Stable code, JSON path and diagnostic message. | Immutable result data; no disposal. |
| `SceneValidationCodes` | Constants for tooling-safe diagnostic codes. | Static constants. |
| `ISceneComponentDescriptor` | Contract for explicitly registered component-property validation. | Registry borrows the descriptor; no disposal contract. |
| `DelegateSceneComponentDescriptor` | Descriptor backed by a game/module validation delegate. | Registry borrows descriptor and delegate. |
| `SceneComponentRegistry` | Case-sensitive setup-time descriptor registry. | Game-owned CPU data; configure before concurrent reads. |
| `SceneComponentValidationContext` | Component data and aggregated issue reporter. | Call-scoped borrower; do not retain it. |
| `ISceneRuntimeComponentDescriptor` | Explicit validation plus runtime create/destroy contract. | Registry borrows descriptor; returned values belong to `SceneInstance`. |
| `SceneComponentInstantiationContext` | Planned node/component and resolved transforms supplied to a factory. | Call-scoped borrower; do not retain it. |
| `SceneLoader` | Parses, validates and prepares a parent-first CPU load plan. | Static utility; allocates no runtime/GPU resource. |
| `SceneLoadPlan` | Immutable validated nodes and resolved transforms. | Caller-owned CPU data; no disposal. |
| `ScenePlannedNode` | Parent-first document node with local/world matrices. | Plan-owned immutable data; no disposal. |
| `SceneDocumentValidationException` | Aggregated validation failure raised during preparation. | Exception value; no disposal. |
| `SceneInstantiator` | Runs registered runtime factories on its captured owner thread. | Game-owned coordinator; no disposal. |
| `SceneInstance` | Instantiated nodes and owned runtime component values. | Owner; dispose on the instantiator's thread. |
| `SceneService` | Transactional load/activate/reload/unload for one active scene. | Owns the active `SceneInstance`; dispose on its owning thread before shared asset caches. |
| `SceneFlowController` | Boot/menu/loading/playing/paused/result transitions and game-owned state hooks. | Borrows `SceneService`; owns no scene, physics or UI resources. |
| `BepuSceneFlowAdapter` (`Nova3D.Physics.Bepu`) | Steps BEPU only while flow is Playing with an active scene. | Borrows flow and physics world; never owns scene bodies. |
| `SceneNodeInstance` | Runtime node view, transforms and created components. | Instance-owned view; no independent disposal. |
| `SceneComponentInstance` | Typed runtime value created for a component envelope. | Instance owns value through its descriptor. |
| `SceneInstantiationException` | Node/component context for a failed factory. | Exception value; no disposal. |
| `SceneBuiltInComponentRegistration` | Explicit registration for Nova3D camera/light/spawn/tag and optional model. | Static utility; registry borrows descriptors. |
| `SceneModelComponentDescriptor` | Validates and creates a renderable GLB/glTF through injected factories. | Descriptor borrows factories; configured ownership controls loaded models. |
| `SceneModelComponent` | Model renderer, authored flags and world transform. | Scene-owned; owns renderer and conditionally owns model. |
| `SceneCameraComponent` | `Camera3D` plus primary-camera intent. | Scene-owned value; no disposable resource. |
| `SceneDirectionalLightComponent` | Directional light plus shadow intent. | Scene-owned value; no disposable resource. |
| `SceneSpawnPoint` | Gameplay spawn kind and resolved world transform. | Scene-owned value; no disposable resource. |
| `SceneTag` | Game-interpreted semantic marker. | Scene-owned immutable value. |
| `SceneBuiltInValidationCodes` | Stable component-property diagnostic codes. | Static constants. |
| `SceneAssetResolver` | Contains portable references under a configured Assets root and verifies existence/casing. | Game-owned CPU configuration; no disposal. |
| `SceneAssetCache<T>` | Thread-affine reference-counted cache with explicit load/destroy callbacks. | Owner of every retained value; dispose after scene instances. |
| `SceneAssetLease<T>` | One reference to a cached value. | Owner of one cache reference; dispose on cache thread. |
| `SceneAssetNotFoundException` | Scene, authored reference and resolved-path missing-file diagnostic. | Exception value; no disposal. |
| `SceneAssetPathException` | Unsafe, escaping or casing-invalid reference diagnostic. | Exception value; no disposal. |
| `SceneModelAsset` | Explicit owned, borrowed or cache-leased model lifetime. | Scene model component disposes it. |
| `ScenePrefabLoader` | Validates and expands reusable scene/1 files into a CPU load plan. | Static utility; allocates no runtime resources. |
| `ScenePrefabComponentDescriptor` | Validates the reserved `nova3d.prefab` envelope during expansion. | Loader-owned validation descriptor. |
| `ScenePrefabException` | Source and anchor context for override or recursive-reference errors. | Exception value; no disposal. |
| `SceneDebugVisualization` | Queues node/model bounds and reports name anchors for caller-owned text rendering. | Static utility; borrows `SceneInstance` and `DebugRenderer`. |

The parser enforces document shape but not semantic validity. Call
`SceneDocumentValidator.Validate` with an explicit `SceneComponentRegistry`
before runtime work. `SceneLoader.Prepare` combines those CPU-only steps and
resolves hierarchy matrices. Create `SceneInstantiator` on the graphics thread;
its `SceneInstance` owns runtime values and rolls them back/disposes them in
reverse creation order. The registry performs no assembly scanning and borrows
its descriptors.

Use `SceneInstance.GetComponents<T>()` for scene-wide typed discovery, such as
finding primary cameras or spawn points. Gameplay decides how to interpret
multiple primary cameras, tags and spawn kinds. Use
`SceneModelComponentDescriptor.CreateCached` for the standard asset-root and
shared-model lifecycle. Dispose scene instances before their cache.
Use `ScenePrefabLoader.Prepare` when the scene contains `nova3d.prefab`; it
expands references and overrides before runtime allocation.

### Input actions

Read [input.md](input.md). The map borrows native MonoGame input snapshots and
owns no device or UI state.

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `InputActionMap` | Evaluates registered intentions once per frame. | Game-owned CPU state; no disposal. |
| `InputAction` | Current digital/1D/2D value and pressed/released/down transitions. | Owned by its map; do not create independently. |
| `InputBinding` | Immutable keyboard, mouse or gamepad source plus deadzone/scale/inversion. | Value; configured during setup. |
| `InputContextRouter` | Priority routing and modal isolation for action maps. | Game-owned CPU state; no disposal. |
| `InputContext` | Explicitly active group of actions; may block lower priorities. | Owned by its router. |
| `InputDeviceCapture` | Keyboard/mouse/gamepad mask for UI capture. | Value; provided each frame. |
| `InputBindingJson` | Versioned JSON serialization and transactional import for one action map. | Stateless; game owns file I/O. |
| `InputBindingConflict` | Reports physical-control overlap between actions in one map. | Value; returned by `FindConflicts`. |

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
| `VisualCapturePlan` | Fixed resolution, seed, timestep and warm-up contract for one capture. | Immutable CPU configuration; the game must apply its values. |
| `VisualCaptureSession` | One-shot back-buffer PNG and environment-metadata capture. | Graphics-thread coordinator; owns no persistent GPU resource. |
| `VisualCaptureMetadata` | Versioned deterministic inputs and GPU/backend identity stored beside the PNG. | Immutable returned data. |
| `VisualCaptureResult` | Image/metadata paths and the captured metadata. | Immutable returned data. |
| `ILogger` | Nova3D logging sink contract. | Implementer defines lifetime. |
| `FileLogger` | Thread-safe file logger implementation. | Owner of file writer; dispose to flush/close. |
| `LogLevel` | Logging severity. | Enum. |

CPU profiler timings are not GPU timings. FPS alone is not regression evidence;
also report milliseconds, allocations, draws and triangles.

### Audio

Read [audio.md](audio.md). Audio assets remain MonoGame types loaded and owned
by `ContentManager`.

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `AudioSystem` | Coordinates buses, music, SFX pools and focus lifecycle. | Owner of pools/instances and exclusive MediaPlayer session; borrows audio assets. |
| `AudioMixer` | Owns Master, Music, SFX and custom bus hierarchy. | System/game-owned CPU state; no disposal. |
| `AudioBus` | Volume, mute, parent gain and linear fade. | Mixer-owned CPU state; no disposal. |
| `MusicChannel` | Loop/fade/pause control over global MonoGame MediaPlayer. | AudioSystem-owned; borrows `Song`. |
| `SoundEffectPool` | Fixed-capacity reusable native voice pool with 2D/3D playback. | Owner of created `SoundEffectInstance`s; borrows `SoundEffect` and bus. |
| `SoundPlaybackOptions` | Volume, pitch, pan and loop request. | Value type. |
| `AudioPoolOverflowPolicy` | Reject or steal-oldest behavior at capacity. | Enum. |

Only one `AudioSystem` may control music because `MediaPlayer` is global.
Gameplay owns sound-event meaning and persistence.

### Persistence

Read [persistence.md](persistence.md). Game code owns all persisted data types,
validation rules and migrations.

| Type | Purpose | Ownership / lifetime |
|---|---|---|
| `GameDataPaths` | Platform user-data root plus settings, bindings and contained save-slot paths. | Immutable path configuration; creates no directory by itself. |
| `VersionedJsonStore<T>` | Strict versioned JSON, atomic replacement, backup recovery, validation and migration. | Stateless disk coordinator; borrows delegates/logger and owns no `T`. |
| `PersistenceLoadResult<T>` | Loaded value plus primary/backup/default source and migration evidence. | Value returned to game code. |
| `PersistenceLoadSource` | Primary, backup, missing-default or invalid-default origin. | Enum. |
| `PersistenceMigration<T>` | Explicit old-payload to current-type conversion. | Game-owned delegate; store borrows it. |

The store is synchronous and not thread-safe. Save outside the frame hot path;
coordinate concurrent/background writes in game code.

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
| Capture a deterministic frame | `VisualCapturePlan`, `VisualCaptureSession` |

If a required behavior is absent here, inspect the relevant topic document
before adding a public abstraction. Exceeding a current limit requires coordinated
implementation, regression coverage and documentation changes.

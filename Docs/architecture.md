# Architecture

Nova3D is a rendering and world toolkit over MonoGame. It is not a replacement
engine and deliberately keeps MonoGame types visible.

```text
Game / simulation
        │
        ▼
      Nova3D
  ┌─────┼──────────────┐
Rendering  World  Production
  │          │       │
  └──────────┴───────┘
             │
          MonoGame
             │
            GPU
```

## Modules

- `Nova3D.Rendering`: camera, meshes, materials, lighting, CSM, instancing,
  models, water, post-processing and render statistics.
- `Nova3D.World`: terrain, vegetation, spatial queries and world streaming.
- `Nova3D.Production`: runtime assets, glTF/GLB, configuration, logging,
  debugging, profiling, device-neutral input actions, audio coordination and
  versioned user-data persistence.
- `Nova3D.Resources`: named access to MonoGame Content resources and shaders.
- `Nova3D/Shaders`: reusable effects shipped with the Nova3D package.
- `Benchmarks/CityBenchmark/Shaders`: effects that belong only to the benchmark.

Audio coordination and persistence remain in the mandatory core package: they
add no dependency beyond MonoGame/BCL and repeated in two runnable consumers.
Physics and Gum remain optional because each selects an external backend. The
evidence and reconsideration triggers are recorded in
[package-boundaries.md](package-boundaries.md).

## Dependency direction

- Game code may depend on every Nova3D module.
- World systems may depend on Rendering.
- Rendering must not depend on game simulation or city-builder rules.
- Nova3D must not depend on `Nova3D.Benchmarks` or its validation scenes.
- Gameplay state must not be stored in renderers, materials or GPU buffers.

## Ownership

- `ContentManager` owns resources loaded through `Content.Load`.
- `Mesh` owns its buffers unless constructed with `ownsBuffers: false`.
- `GltfModel` owns imported meshes and textures.
- `GltfModelRenderer` does not own its `GltfModel` or effects.
- `GltfSkeletonPose` and `GltfAnimationPlayer` are per-instance CPU state and
  borrow immutable nodes, skins and clips from their `GltfModel`.
- `AudioSystem` owns the SFX instances created by its bounded pools, borrows
  ContentManager audio assets and exclusively controls the global MediaPlayer.
- `VersionedJsonStore<T>` owns no game data; it synchronously writes the
  caller's game-owned records and retains one last-known-good disk backup.
- Materials do not own effects or textures.
- `WorldStreamer<T>` calls the supplied unload callback for active resources.
- `SceneLoadPlan` is immutable CPU data and owns no runtime resource.
- `SceneInstance` owns values returned by runtime component descriptors and
  destroys them in reverse creation order.
- `SceneService` owns its active `SceneInstance` but not the instantiator,
  component registry or shared asset caches.
- `SceneFlowController` borrows `SceneService`; UI, physics and gameplay state
  remain game-owned.
- Optional `BepuSceneFlowAdapter` borrows the flow and BEPU world. The game or
  its scene descriptors still own and remove scene-specific physics handles.
- `SceneAssetCache<T>` owns retained asset values; each `SceneAssetLease<T>` owns
  one reference, and scene instances must be disposed before their cache.
- Every class that creates GPU resources must implement and honor `IDisposable`.

## Threading

Nova3D v0.2 creates, updates and destroys GPU resources on the graphics thread.
`FileAssetManager.Update`, GLB import, chunk creation and dynamic buffer uploads
must run there. Background work may prepare CPU-only data, but it must not touch
`GraphicsDevice` resources.

`SceneLoader.Prepare` may run as CPU work. Construct `SceneInstantiator` on the
graphics thread; instantiation, rollback and `SceneInstance.Dispose` must run on
that captured thread because registered factories may own GPU resources.
Create, acquire, release and dispose `SceneAssetCache<T>` on that thread as well.

## Public API style

Use MonoGame's `Texture2D`, `Effect`, `Vector2`, `Vector3`, `Quaternion`,
`Matrix`, `BoundingBox` and `BoundingFrustum` directly. Add a Nova3D type only
when it expresses Nova3D behavior or ownership, not to rename an existing type.

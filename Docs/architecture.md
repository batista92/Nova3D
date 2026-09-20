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
  debugging and profiling.
- `Nova3D.Resources`: named access to MonoGame Content resources and shaders.
- `Nova3D/Shaders`: reusable effects shipped with the Nova3D package.
- `Benchmarks/CityBenchmark/Shaders`: effects that belong only to the benchmark.

## Dependency direction

- Game code may depend on every Nova3D module.
- World systems may depend on Rendering.
- Rendering must not depend on game simulation or city-builder rules.
- Nova3D must not depend on the benchmark or `CityBuilder.Tests`.
- Gameplay state must not be stored in renderers, materials or GPU buffers.

## Ownership

- `ContentManager` owns resources loaded through `Content.Load`.
- `Mesh` owns its buffers unless constructed with `ownsBuffers: false`.
- `GltfModel` owns imported meshes and textures.
- `GltfModelRenderer` does not own its `GltfModel` or effects.
- Materials do not own effects or textures.
- `WorldStreamer<T>` calls the supplied unload callback for active resources.
- Every class that creates GPU resources must implement and honor `IDisposable`.

## Threading

Nova3D v0.1 creates, updates and destroys GPU resources on the graphics thread.
`FileAssetManager.Update`, GLB import, chunk creation and dynamic buffer uploads
must run there. Background work may prepare CPU-only data, but it must not touch
`GraphicsDevice` resources.

## Public API style

Use MonoGame's `Texture2D`, `Effect`, `Vector2`, `Vector3`, `Quaternion`,
`Matrix`, `BoundingBox` and `BoundingFrustum` directly. Add a Nova3D type only
when it expresses Nova3D behavior or ownership, not to rename an existing type.

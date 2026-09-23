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

## Change protocol

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

# Rendering

## Frame context

Create one `RenderContext` per renderer and call `BeginFrame(camera)` once per
frame. It resets current statistics, preserves the previous frame and starts
the CPU profiler.

```csharp
renderContext.BeginFrame(camera);
material.Apply(renderContext);
```

`Camera3D.ViewProjection` is `View * Projection`, matching MonoGame row-vector
matrix conventions.

## Expected pass order

```text
1. Update camera, culling, LOD and streaming
2. Update CSM matrices
3. Render four shadow cascades
4. Begin HDR scene
5. Draw sky and opaque world
6. Draw terrain, vegetation and models
7. Draw water/transparency
8. Draw debug geometry
9. Resolve HDR, bloom and FXAA
```

Restore `BlendState`, `DepthStencilState` and `RasterizerState` explicitly when
changing passes. Do not assume a previous renderer left the device in a useful
state.

## Meshes

`Mesh` contains a vertex buffer, index buffer and primitive count. It supports
16-bit and 32-bit indices. Prefer 16-bit indices when a mesh has at most 65,535
vertices. A draw must apply every pass of the active effect technique.

## Instancing and LOD

`LodInstancedMeshBatch` performs spatial candidate selection, distance culling,
frustum sphere culling and LOD selection on CPU, then uploads visible instance
transforms to dynamic buffers. LOD distances must be ascending and below the
cull distance. Supply realistic `BoundsRadius` and `BoundsOffset`; incorrect
bounds cause visible popping, not a shadow or camera problem.

## Shadows

`CascadedShadowMap.CascadeCount` is fixed at four in v0.1. Call `Update`, render
each cascade between `BeginCascade(i)` and `End`, then call `Apply(effect)` for
receivers. Do not infer shadow coverage from screen distance: diagnose cascade
selection, light-space XY/Z coverage and receiver normals separately.

## Statistics

Every renderer must register draw calls and triangle counts in
`RenderContext.Statistics`. Shadow draws use `shadow: true`. Performance work
without statistics and profiler evidence is incomplete.

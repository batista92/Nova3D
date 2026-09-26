# GLB, material and scene

## Use when

Loading a static glTF/GLB scene or obstacle through Nova3D's runtime importer.
The game must already own its PBR effect, directional light and IBL textures.

## Files

```text
Assets/Models/obstacle.glb
Game/World.cs
```

## Implementation

Create GPU resources in `LoadContent` or another graphics-thread call:

```csharp
using Nova3D.Production.Assets.Gltf;
using Nova3D.Rendering.Models;

_model = new GltfImporter(GraphicsDevice)
    .Load("Assets/Models/obstacle.glb");
_renderer = new GltfModelRenderer(_model, pbrEffect, sun, imageBasedLighting)
{
    Transform = Matrix.CreateScale(2f) * Matrix.CreateTranslation(0f, 1f, -5f)
};
```

Begin the render context once, then draw:

```csharp
_renderContext.BeginFrame(_camera);
_renderer.Draw(_renderContext);
```

When CSM is enabled, draw the same model inside every shadow cascade:

```csharp
_renderer.DrawShadows(_renderContext, shadowEffect, lightViewProjection);
```

Dispose in reverse ownership order:

```csharp
_renderer.Dispose(); // owns renderer-created states/material objects
_model.Dispose();    // owns imported meshes and textures
```

The renderer does not own the model, effects, light or IBL textures.

## Validate

- model renders at the expected transform;
- bounds/culling do not remove visible geometry;
- textured and untextured primitives render;
- shadow debug confirms the model enters expected cascades;
- process exits without disposed-resource errors.

## Common failures

- missing external image beside `.gltf`: prefer self-contained GLB;
- invisible model: check scale, transform, winding and camera before normals;
- unsupported skin/animation/compression: replace with static `TRIANGLES` GLB;
- wrong colors: do not gamma-correct normal/AO/roughness data;
- importing in background thread: GPU resource creation is graphics-thread only.


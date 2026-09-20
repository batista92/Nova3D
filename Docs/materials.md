# Materials

Materials express render semantics and parameter binding. They do not own the
effect or textures they reference.

## Material base

`Material.Effect` may come from a fixed `Effect` or `Func<Effect>`. Use the
provider form for versioned shader hot reload:

```csharp
var material = new PbrMaterial(
    "building", () => shaderHandle.Value, sun, environment);
```

Do not cache `material.Effect` across frames when using a provider.

## PBR material

`PbrMaterial` uses metallic-roughness PBR with direct directional light and
split-sum IBL. Supported inputs are:

- linear albedo factor;
- metallic, roughness and ambient-occlusion factors;
- base-color texture, interpreted as sRGB;
- tangent-space normal texture and normal scale;
- glTF metallic-roughness texture: G = roughness, B = metallic;
- occlusion texture: R = occlusion;
- exposure and debug views.

Normal mapping reconstructs the tangent basis from world-position and UV
derivatives. v0.1 supports `TEXCOORD_0` only. glTF alpha `BLEND` is currently
rendered as opaque.

## Terrain material

`TerrainMaterial` binds directional light, texture scale, triplanar sharpness
and a `TerrainLayerSet`. Layer texture names must match shader parameter names.

## Forward-lit and water materials

`ForwardLitMaterial` is the lightweight path used by benchmark geometry and
vegetation. `WaterMaterial` binds camera, sun and time for `WaterRenderer`.
Do not add gameplay state to either material.

## Color space

Perform lighting in linear space and gamma-encode once at final output. Base
color textures are sRGB data; normal, metallic, roughness, AO, height and shadow
maps are linear data. Never apply gamma correction to data textures.

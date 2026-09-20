# Shaders

MonoGame effects are HLSL-compatible `.fx` files compiled by MGCB or MGFXC.
DesktopGL and DirectX use different shader profiles, so retain the existing
`OPENGL`, `SV_POSITION`, `VS_SHADERMODEL` and `PS_SHADERMODEL` macros.

## Sampler budget

Treat 16 samplers as the hard v0.1 portability limit for a pixel shader. Count
the compiled shader's declared samplers before adding one.

| Shader path | Material | Shadows | IBL | Used | Free |
|---|---:|---:|---:|---:|---:|
| `LargeTerrain.fx` | 8 | 4 | 0 | 12 | 4 |
| `PBR.fx` | 4 | 4 | 3 | 11 | 5 |

The four terrain layers already consume eight samplers because every layer has
an albedo-height and a normal-AO-roughness texture. Do not add a fifth terrain
layer without redesigning the texture representation and validating every
target backend.

## Parameter contracts

Shader parameter names are API. C# bindings use names such as `World`,
`ViewProjection`, `LightDirection`, `ShadowMap0` and `GrassAlbedoHeight`.
Renaming a parameter requires updating its material/system in the same change.
Optional cross-shader parameters may use null-conditional lookup; required
material parameters should fail clearly.

## Matrix and normal rules

- Use MonoGame row-vector order: local transform first, parent/world after it.
- Send `WorldInverseTranspose` for non-uniformly scaled normal transforms.
- Normals are directions: transform with W = 0 and normalize.
- Validate winding and `CullMode` before declaring normals inverted.
- Depth reconstruction and shadow coordinates must use the same conventions as
  the matrices that generated them.

## Hot reload

`ShaderHotReload.LoadEffect` consumes compiled `.mgfxo`, not `.fx` source.
Reload is transactional: a failed replacement keeps the current effect alive.
Materials must use an effect provider to observe a new handle version.

## Editing checklist

1. Count samplers and interpolators.
2. Confirm DesktopGL profile compatibility.
3. Update C# parameter bindings.
4. Build the Content Pipeline.
5. Run the relevant debug view.
6. Re-run the city benchmark and compare timings.

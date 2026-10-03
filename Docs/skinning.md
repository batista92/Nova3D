# glTF skinning and animation

G5 adds GPU skinning and clip playback to the existing runtime glTF importer.
G5.2 imports the complete data contract and G5.3 evaluates runtime poses and
clips. Animated rendering remains assigned to G5.4.

## glTF contract selected for the first cut

- one `JOINTS_0` plus `WEIGHTS_0` set, with at most four influences per vertex;
- skin joints follow the glTF node hierarchy;
- inverse-bind matrices are `MAT4` floats and default to identity when absent;
- animation targets are node translation, rotation and scale;
- `LINEAR` and `STEP` interpolation only;
- no morph targets, `CUBICSPLINE`, IK, retargeting or automatic root motion.

## Imported data contract (G5.2)

`GltfImporter.Load` now imports:

- `JOINTS_0` as unsigned byte/short and `WEIGHTS_0` as float or normalized
  unsigned byte/short;
- one local joint palette per skinned primitive, deterministically remapped to
  no more than 48 entries;
- node hierarchy and bind-pose TRS, preserving matrix nodes when they are not
  animation targets;
- skins, joint node indices, optional skeleton root and `MAT4` inverse binds;
- named clips with translation, rotation and scale channels using `LINEAR` or
  `STEP` samplers.

The corresponding public data is exposed through `GltfModel.Nodes`, `Skins`
and `Animations`; `GltfInstance.NodeIndex/SkinIndex` connects render instances
to it. `GltfPrimitive.JointPalette` maps the vertex `BlendIndices` stored in
`GltfSkinnedVertex` back to a skin's joint list.

The importer validates hierarchy cycles, bad references, mismatched accessor
counts, invalid/zero weights, non-finite animation data and palette overflow.
It rejects sparse accessors, `JOINTS_1`/`WEIGHTS_1`, morph animation and
`CUBICSPLINE` explicitly. `GltfModelRenderer` must not be described as an
animated renderer until G5.4: during G5.2 it can only submit the imported bind
geometry through the existing static path.

After fetching the official fixtures, exercise the actual GPU importer with:

```powershell
dotnet run --project Benchmarks/SkinningSpike/SkinningSpike.csproj -c Release -- `
  --import LocalAssets/Animation/RiggedSimple.glb `
  LocalAssets/Animation/CesiumMan.glb LocalAssets/Animation/Fox.glb
```

## Runtime pose and playback (G5.3)

Create one mutable pose and player for each independently animated model
instance:

```csharp
var pose = new GltfSkeletonPose(model);
var player = new GltfAnimationPlayer(model, pose);

player.Play("Walk", loop: true, speed: 1f);
player.Update(gameTime.ElapsedGameTime);

// Cross-fade from the current clip to Idle over 0.2 seconds.
player.Play("Idle", loop: true, blendDuration: 0.2f);
```

`GltfSkeletonPose` stores local TRS and hierarchy-resolved world matrices for
all model nodes. `WriteSkinPalette` combines inverse binds, animated joint world
transforms and the inverse animated mesh-node transform into the primitive's
local joint order. G5.4 passes that result to the shader.

`GltfAnimationPlayer` supports clip index or exact clip name, looping,
non-negative playback speed, explicit stop and a two-clip cross-fade. A new
blend requested during another blend uses the current target clip as its source;
it does not capture an arbitrary three-clip blended snapshot. Stop resets to
bind pose by default; pass `resetToBindPose: false` to preserve the last pose.

The player preallocates its source/target TRS arrays. Its hot `Update` path,
including hierarchy propagation, creates no managed allocations. The G5.3
regression covers LINEAR/STEP sampling, loop, speed, stop, blend and palette
generation. On the 2026-10-02 development machine its three-node synthetic
fixture measured approximately 0.0023 ms/update and 0 B/update over 10,000
updates. This is a CPU runtime measurement, not GPU time.

These rules follow the [Khronos glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html).

## Reference assets

Run the opt-in download, then inspect all fixtures:

```powershell
.\eng\fetch-skinning-fixtures.ps1
dotnet run --project Benchmarks/SkinningSpike/SkinningSpike.csproj -c Release -- `
  LocalAssets/Animation/RiggedSimple.glb `
  LocalAssets/Animation/CesiumMan.glb `
  LocalAssets/Animation/Fox.glb
```

Fixtures are downloaded from Khronos `glTF-Sample-Assets` into ignored
`LocalAssets/Animation`; SHA-256 hashes are pinned by the script. They are not
redistributed in Nova3D. Their model licenses are CC-BY-4.0, except Fox combines
CC0-1.0 and CC-BY-4.0 as recorded by the upstream repository.

| Asset | Purpose | Joints | Clips | Channels | Interpolation |
|---|---|---:|---:|---:|---|
| RiggedSimple | minimum skin/animation fixture | 2 | 1 | 3 | LINEAR |
| CesiumMan | humanoid hierarchy and dense TRS channels | 19 | 1 | 57 | LINEAR |
| Fox | multiple named animation cycles | 24 | 3 | 63 | LINEAR |

## Joint limit measurement

`Benchmarks/SkinningSpike/SkinningProbe.fx` retains the selected shader shape:
four weighted matrices, PBR world/view/projection inputs and four CSM matrices.
With MonoGame MGCB 3.8.4.1 in HiDef:

| Backend compilation | Result |
|---|---|
| DesktopGL, 128 `float4x4` joints | compiled; not treated as a portable guarantee |
| Windows DirectX `vs_4_0_level_9_1`, 55 joints | compiled |
| Windows DirectX `vs_4_0_level_9_1`, 56 joints | failed with X4507, 256-register limit |

Nova3D therefore adopts **48 joints per draw** for the first implementation.
The seven-matrix margin is reserved for shader evolution and backend variance.
A skin may contain more joints overall only when each rendered primitive is
remapped to a local palette of at most 48; otherwise import must fail clearly.

## Palette strategy

| Strategy | Transfer | Samplers | Decision |
|---|---:|---:|---|
| uniform `float4x4[]` | 64 B/joint | 0 | selected: direct `Matrix[]` upload and portable shader path |
| uniform packed 3x4 rows | 48 B/joint | 0 | deferred: saves registers but complicates packing and matrix conventions |
| float texture palette | 48 B/joint | 1 | deferred: higher joint capacity, but vertex texture support varies by backend |
| CPU-skinned dynamic vertices | vertex-count dependent | 0 | rejected as the normal path; upload and CPU cost scale with geometry |

The selected path does not change PBR's 11-pixel-sampler budget and adds no
draw by itself. A 48-joint palette is 3 KiB per skinned draw.

## Measured CPU and GPU budget

On the 2026-10-02 development machine (Windows 10, .NET SDK 10.0.400), the
headless benchmark calculated hierarchical world matrices and palettes for 100
characters x 48 joints in 0.46-0.58 ms/frame, with 0 B allocated after warmup.
Uploading full matrices for that workload represents 300 KiB/frame.

This is CPU pose/palette cost, not GPU time. G5.1 records the GPU-side contract
as four weighted joint fetches, two skinned vector transforms, zero additional
samplers and the verified shader-register limit. G5.4 exercises the complete
renderer, but does not capture GPU timestamps; Nova3D must not label CPU submit
time or an estimate as GPU timing.

## Animated rendering and debug (G5.4)

Pass the same per-instance pose used by the player to `GltfModelRenderer`:

```csharp
var pose = new GltfSkeletonPose(model);
var player = new GltfAnimationPlayer(model, pose);
using var renderer = new GltfModelRenderer(model, pbrEffect, sun, environment, pose);

player.Play("Walk");
player.Update(gameTime.ElapsedGameTime);
renderer.UpdateBounds(); // Call before culling; Draw also refreshes it.
renderer.Draw(context);
```

`PBR.fx` exposes `PBR` and `PBRSkinned`; `ShadowDepth.fx` exposes
`ShadowDepth` and `SkinnedShadowDepth`. Both skinned techniques consume the same
48-entry `JointPalette` and add no sampler. The renderer selects the technique
per primitive, supports rigid animated nodes in a mixed model, and submits the
same palette in main and shadow passes.

Animated `Bounds` are conservative rather than guessed: import stores the
bind-pose vertex bounds influenced by each local palette joint, then the
renderer transforms and unions those boxes using the current palette and model
transform. Call `UpdateBounds` after updating the player when culling happens
before drawing.

For debug lines:

```csharp
GltfSkeletonDebug.Draw(debugRenderer, model, pose, renderer.Transform,
    Color.Cyan, Color.Yellow);
debugRenderer.BoundingBox(renderer.Bounds, Color.Lime);
```

Run the external-asset sample after fetching fixtures:

```powershell
.\eng\fetch-skinning-fixtures.ps1
dotnet run --project Samples/AnimatedCharacter/AnimatedCharacter.csproj
```

It loads Fox by default, plays its first clip, switches clips with Tab/1-9 using
a 0.25-second cross-fade, renders an animated shadow pass and toggles skeleton
plus bounds with D. The automated five-frame smoke rendered one draw and 576
triangles for Fox. This proves submission and shader execution; no GPU timestamp
was captured, so the result is deliberately not reported as GPU milliseconds.

The current linear-blend normal path assumes joint transforms do not contain
problematic non-uniform scale. World transforms still use inverse-transpose as
before. Assets requiring mathematically exact normals under non-uniform joint
scale need a future tangent/normal skinning path.

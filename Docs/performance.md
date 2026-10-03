# Performance

The CityBenchmark is the regression baseline for Nova3D. Do not optimize from
FPS alone; record resolution, camera position, visibility and frame timings.

Reference validation scene:

```text
Terrain       2048 x 2048
Trees         10,000
Buildings     1,000
Vehicles      500
CSM           4 cascades
Water         enabled
PostFX        HDR + bloom + FXAA
Observed      about 3.31 ms / 302 FPS on the original validation machine
```

That number is evidence from one machine, not a universal target.

## Measurement

- `RenderStatistics`: draws, triangles, visible instances and shadow draws.
- `FrameProfiler`: named CPU sections such as shadow, world and post.
- Window title: quick live diagnostics, not stored benchmark data.
- GPU timings are not implemented in v0.2; do not label CPU timings as GPU.

## Performance rules

- Cull before uploading instance data.
- Use `StaticSpatialGrid` to reduce candidate counts.
- Use instancing for repeated meshes.
- Use LOD for terrain chunks and repeated geometry.
- Budget streaming work per update.
- Rebuild only dirty terrain chunks.
- Avoid allocating arrays or GPU objects every frame.
- Do not call `GetData` on GPU resources during normal rendering.
- Do not trade correctness for a higher FPS without a debug comparison.
- Compare milliseconds, draw calls, triangles and candidate counts before and
  after a renderer change.

## Regression procedure

1. Use the same build configuration and window resolution.
2. Use a comparable camera position.
3. Let streaming and smoothed timings stabilize.
4. Record CPU shadow/world/post timings and render statistics.
5. Change one system.
6. Repeat and explain any material regression.

## Deterministic visual capture

`VisualCapturePlan` records the scene name, exact resolution, random seed,
fixed timestep and warm-up count. A game must actually apply those inputs;
recording them is not a substitute for deterministic scene setup.
`VisualCaptureSession.CompleteFrame` is called once after final back-buffer
composition. On frame `WarmupFrames + 1`, it performs the one intentional GPU
readback and writes:

```text
<scene>.png
<scene>.capture.json
```

The metadata uses `nova3d.visual-capture` version 1 and records the backend,
adapter, graphics profile, color/depth formats, MSAA, OS and architecture. Its
schema is [nova3d.visual-capture.1.schema.json](Schemas/nova3d.visual-capture.1.schema.json).
Construct and call the session on the graphics thread. It owns no persistent
GPU resource; the temporary readback texture is disposed before the call
returns. Never run capture readback during normal gameplay frames.

The CityBenchmark is the first capture host:

```powershell
dotnet run --project .\Benchmarks\Nova3D.Benchmarks\Nova3D.Benchmarks.csproj `
  -c Release -- --capture .\artifacts\visual-captures\city
```

Capture mode fixes 1280×720, camera, seed `9127`, 1/60-second updates, no MSAA
and 120 warm-up frames; it excludes interactive UI/input and exits after frame
121. Byte-identical output is expected only when the complete recorded
environment is controlled. G8.2 owns baseline storage, tolerance, comparison
metrics and explicit baseline updates.

## Visual baseline comparison

Baselines live under `Benchmarks/VisualBaselines/<scene>/v<capture-version>`.
Create or replace one only after reviewing the capture:

```powershell
nova3d visual update-baseline .\artifacts\visual-captures\city\city-benchmark.png `
  --baseline-root .\Benchmarks\VisualBaselines --accept
```

Compare a later deterministic capture with:

```powershell
nova3d visual compare .\artifacts\visual-captures\city\city-benchmark.png `
  --baseline-root .\Benchmarks\VisualBaselines `
  --diff .\artifacts\visual-diffs\city-benchmark.png
```

The versioned `baseline.json` stores SHA-256 hashes, environment and default
per-channel/changed-pixel tolerances. CLI overrides are available for diagnosis,
but changing the committed policy requires another explicit baseline update.
Review the generated diff before accepting any visual change. Cross-GPU results
are auxiliary; the official gate must use a controlled environment.

## Initial visual coverage

Run the complete controlled-GPU suite with:

```powershell
.\eng\run-visual-regression.ps1
```

| Baseline scene | Coverage |
|---|---|
| `city-benchmark` | four-cascade CSM, chunked terrain, vegetation, water, HDR, bloom and FXAA |
| `pbr-material-csm` | PBR metallic/dielectric/roughness response and PBR cascade bindings |
| `gltf-static-animated` | generated GLB import, static PBR draw, skinned animation and static/skinned shadow techniques |

The GLB probe creates its tiny fixtures at runtime, then removes them after GPU
resources are disposed. This avoids an external download or an asset-license
dependency in regression coverage. Its animation evaluates a fixed absolute
pose because a fixed-step loop may legally run multiple updates before one
captured draw.

The repository validation builds every probe but does not open graphical
windows. `run-visual-regression.ps1` is the separate controlled-machine gate.

## Performance budget gate

Run the renderer budget and the preserved headless physics regression on the
controlled machine with:

```powershell
.\eng\run-performance-regression.ps1
```

The script warms the fixed CityBenchmark for 180 frames, samples 240 frames at
1280x720 without MSAA, writes
`artifacts/performance/city-benchmark.json`, then evaluates it against
`Benchmarks/PerformanceBudgets/city-benchmark.json`. Use `-SkipPhysics` only
when the regular repository validation has already covered physics.

The report contract is `nova3d.performance-report` version 1. It records the
environment plus average, p50, p95 and maximum timings, and average/maximum
render counts. `frameIntervalMilliseconds` is observed presentation interval;
all fields ending in `CpuMilliseconds` are CPU timings. Nova3D still has no GPU
timestamp query and must not describe either value as GPU time.

Automated capture disables MonoGame's inactive-window sleep so focus changes do
not add an artificial delay to every frame. Current timing limits include the
observed run-to-run CPU variance; structural draw, triangle and candidate
limits remain exact upper bounds for this scene.

Evaluate a stored report directly, including from an agent or CI job:

```powershell
nova3d performance check .\artifacts\performance\city-benchmark.json `
  --budget .\Benchmarks\PerformanceBudgets\city-benchmark.json

nova3d --format json performance check .\artifacts\performance\city-benchmark.json `
  --budget .\Benchmarks\PerformanceBudgets\city-benchmark.json
```

Exit code `1` identifies every exceeded metric and returns an action. Hardware,
backend or resolution different from the budget reference emits a warning: the
result remains useful evidence, but only the recorded reference environment is
the official gate. Never raise a budget merely to make a regression pass;
profile and explain it first. The normative schemas are
[nova3d.performance-report.1.schema.json](Schemas/nova3d.performance-report.1.schema.json)
and [nova3d.performance-budget.1.schema.json](Schemas/nova3d.performance-budget.1.schema.json).

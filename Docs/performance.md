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
- GPU timings are not implemented in v0.1; do not label CPU timings as GPU.

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

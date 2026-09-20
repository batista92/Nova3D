# Terrain

Nova3D terrain is chunk based. `ChunkedTerrain` owns resident chunk meshes,
selects visible chunks and chooses a mesh LOD per chunk.

## Configuration

`ChunkedTerrainSettings` defines:

- `WorldSize`: total terrain width and depth;
- `ChunksPerAxis`: chunk grid resolution;
- `LodSegments`: mesh resolution for each LOD;
- `LodDistances`: ascending thresholds, one fewer than the LOD count;
- `ShadowSegments`: resolution of the shared shadow terrain mesh;
- `BoundsPadding`: vertical safety margin for chunk bounds;
- `Streaming`: optional load and retention policy.

The shadow mesh must fit 16-bit indices. The settings validate that
`(ShadowSegments + 1)^2 <= 65,535`.

## Heights and editing

Terrain samples an `IHeightProvider`. Runtime editing uses
`DeformableHeightProvider`; deformation marks affected chunks dirty and only
those chunks are rebuilt. Do not rebuild the full terrain after a local edit.

Keep collision, placement and vegetation sampling connected to the same height
provider. A separate gameplay height formula will drift from rendered terrain.

## Materials

Terrain rendering uses `TerrainMaterial` and `TerrainLayerSet`. The v0.1 shader
contract supports at most four named layers:

```text
Grass
Dirt
Rock
Sand
```

Each layer consumes two repeating textures:

```text
AlbedoHeight          RGB = albedo, A = height
NormalAoRoughness     RG = encoded normal, B = AO, A = roughness
```

The terrain shader uses triplanar mapping. Do not use a texture atlas for these
repeating materials: mip bleeding and wrap boundaries make it unsuitable.

## Streaming

`WorldStreamer<T>` is budgeted and main-thread safe for GPU creation. Load
radius selects desired cells; retain radius must be greater than or equal to it
to provide hysteresis. Tune load/unload budgets before increasing radii.

using BepuPhysics;
using Microsoft.Xna.Framework;
using Nova3D.World.Terrain;
using Nova3D.World.Streaming;

namespace Nova3D.Physics.Bepu;

/// <summary>Chunk-owned static triangle meshes generated from a Nova3D height provider.</summary>
public sealed class TerrainPhysics : IDisposable
{
    private readonly BepuPhysicsWorld _world;
    private readonly IHeightProvider _heights;
    private readonly TerrainPhysicsSettings _settings;
    private readonly Dictionary<(int X, int Z), StaticHandle>? _residentChunks;
    private readonly WorldStreamer<PhysicsChunk>? _streamer;
    private bool _disposed;

    public TerrainPhysics(BepuPhysicsWorld world, IHeightProvider heights,
        TerrainPhysicsSettings? settings = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _heights = heights ?? throw new ArgumentNullException(nameof(heights));
        _settings = settings ?? new TerrainPhysicsSettings();
        _settings.Validate();

        float chunkSize = _settings.WorldSize / _settings.ChunksPerAxis;
        if (_settings.Streaming is null)
        {
            _residentChunks = new Dictionary<(int X, int Z), StaticHandle>(
                _settings.ChunksPerAxis * _settings.ChunksPerAxis);
            for (int z = 0; z < _settings.ChunksPerAxis; z++)
            for (int x = 0; x < _settings.ChunksPerAxis; x++)
                _residentChunks.Add((x, z), CreateChunk(x, z));
        }
        else
        {
            if (MathF.Abs(_settings.Streaming.CellSize - chunkSize) > 0.001f)
                throw new ArgumentException("Physics streaming cell size must equal terrain chunk size.");
            float expectedOrigin = -_settings.WorldSize * 0.5f;
            if (Vector2.DistanceSquared(_settings.Streaming.Origin,
                    new Vector2(expectedOrigin, expectedOrigin)) > 0.000001f)
                throw new ArgumentException("Physics streaming origin must match the terrain's minimum X/Z corner.");
            _streamer = new WorldStreamer<PhysicsChunk>(
                _settings.Streaming,
                cell => new PhysicsChunk(CreateChunk(cell.X, cell.Z)),
                chunk => _world.Remove(chunk.Handle),
                cell => cell.X >= 0 && cell.X < _settings.ChunksPerAxis &&
                        cell.Z >= 0 && cell.Z < _settings.ChunksPerAxis);
        }
    }

    public int ChunkCount => _streamer?.ActiveCount ?? _residentChunks!.Count;
    public int PendingChunkLoads => _streamer?.PendingLoadCount ?? 0;
    public int ChunkLoadsLastUpdate => _streamer?.LoadsLastUpdate ?? 0;
    public int ChunkUnloadsLastUpdate => _streamer?.UnloadsLastUpdate ?? 0;
    public int LastRebuiltChunkCount { get; private set; }
    public long TotalChunkRebuilds { get; private set; }

    public void Update(Vector3 focus) => _streamer?.Update(focus);

    public int RebuildRegion(TerrainRegion region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        float halfSize = _settings.WorldSize * 0.5f;
        float chunkSize = _settings.WorldSize / _settings.ChunksPerAxis;
        TerrainRegion expanded = region.Expand(chunkSize / _settings.SegmentsPerChunk);
        if (expanded.MaxX < -halfSize || expanded.MinX > halfSize ||
            expanded.MaxZ < -halfSize || expanded.MinZ > halfSize)
        {
            LastRebuiltChunkCount = 0;
            return 0;
        }

        int minX = Math.Clamp((int)MathF.Floor((expanded.MinX + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        int maxX = Math.Clamp((int)MathF.Floor((expanded.MaxX + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        int minZ = Math.Clamp((int)MathF.Floor((expanded.MinZ + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        int maxZ = Math.Clamp((int)MathF.Floor((expanded.MaxZ + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);

        LastRebuiltChunkCount = 0;
        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            if (_streamer is not null)
            {
                if (_streamer.Reload(new WorldCell(x, z))) LastRebuiltChunkCount++;
            }
            else
            {
                var key = (x, z);
                _world.Remove(_residentChunks![key]);
                _residentChunks[key] = CreateChunk(x, z);
                LastRebuiltChunkCount++;
            }
        }
        TotalChunkRebuilds += LastRebuiltChunkCount;
        return LastRebuiltChunkCount;
    }

    private StaticHandle CreateChunk(int chunkX, int chunkZ)
    {
        int segments = _settings.SegmentsPerChunk;
        float chunkSize = _settings.WorldSize / _settings.ChunksPerAxis;
        float originX = -_settings.WorldSize * 0.5f + chunkX * chunkSize;
        float originZ = -_settings.WorldSize * 0.5f + chunkZ * chunkSize;
        float step = chunkSize / segments;
        var vertices = new Vector3[(segments + 1) * (segments + 1)];
        for (int z = 0; z <= segments; z++)
        for (int x = 0; x <= segments; x++)
        {
            float localX = x * step;
            float localZ = z * step;
            vertices[z * (segments + 1) + x] = new Vector3(
                localX, _heights.SampleHeight(originX + localX, originZ + localZ), localZ);
        }

        var indices = new int[segments * segments * 6];
        int at = 0;
        for (int z = 0; z < segments; z++)
        for (int x = 0; x < segments; x++)
        {
            int a = z * (segments + 1) + x;
            int b = a + 1;
            int c = a + segments + 1;
            int d = c + 1;
            // BEPU's triangle front face uses the opposite winding from the
            // terrain render mesh. Keep physical normals facing upward.
            indices[at++] = a; indices[at++] = d; indices[at++] = c;
            indices[at++] = a; indices[at++] = b; indices[at++] = d;
        }

        return _world.CreateStaticTriangleMesh(
            new Vector3(originX, 0f, originZ), vertices, indices,
            _settings.CollisionFilter, _settings.Material);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_streamer is not null) _streamer.Dispose();
        else
        {
            foreach (StaticHandle handle in _residentChunks!.Values) _world.Remove(handle);
            _residentChunks.Clear();
        }
        _disposed = true;
    }

    private sealed record PhysicsChunk(StaticHandle Handle);
}

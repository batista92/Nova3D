using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;
using Nova3D.World.Streaming;

namespace Nova3D.World.Terrain;

public sealed class ChunkedTerrain : IDisposable
{
    private readonly IHeightProvider _heights;
    private readonly GraphicsDevice _device;
    private readonly ChunkedTerrainSettings _settings;
    private readonly Func<float, Color> _colorProvider;
    private readonly TerrainChunk[]? _residentChunks;
    private readonly WorldStreamer<TerrainChunk>? _streamer;
    private readonly List<VisibleChunk> _visible;
    private Mesh _shadowMesh;

    public ChunkedTerrain(GraphicsDevice device, IHeightProvider heights,
        ChunkedTerrainSettings? settings = null, Func<float, Color>? colorProvider = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _heights = heights ?? throw new ArgumentNullException(nameof(heights));
        _settings = settings ?? new ChunkedTerrainSettings();
        _settings.Validate();
        _colorProvider = colorProvider ?? DefaultColor;
        TotalChunks = _settings.ChunksPerAxis * _settings.ChunksPerAxis;
        VisibleLods = new int[_settings.LodSegments.Length];
        _visible = new List<VisibleChunk>(TotalChunks);
        _shadowMesh = CreateGridMesh(device, -_settings.WorldSize * 0.5f,
            -_settings.WorldSize * 0.5f, _settings.WorldSize, _settings.ShadowSegments, _colorProvider);

        var chunkSize = _settings.WorldSize / _settings.ChunksPerAxis;
        if (_settings.Streaming is null)
        {
            _residentChunks = new TerrainChunk[TotalChunks];
            var index = 0;
            for (var z = 0; z < _settings.ChunksPerAxis; z++)
            for (var x = 0; x < _settings.ChunksPerAxis; x++)
                _residentChunks[index++] = CreateChunk(device, x, z, chunkSize, _colorProvider);
        }
        else
        {
            var streaming = _settings.Streaming;
            if (MathF.Abs(streaming.CellSize - chunkSize) > 0.001f)
                throw new ArgumentException("Terrain streaming cell size must equal terrain chunk size.");
            _streamer = new WorldStreamer<TerrainChunk>(streaming,
                cell => CreateChunk(device, cell.X, cell.Z, chunkSize, _colorProvider),
                chunk => chunk.Dispose(),
                cell => cell.X >= 0 && cell.X < _settings.ChunksPerAxis &&
                        cell.Z >= 0 && cell.Z < _settings.ChunksPerAxis);
        }
    }

    public int TotalChunks { get; }
    public int[] VisibleLods { get; }
    public int VisibleCount => _visible.Count;
    public int DrawCalls => _visible.Count;
    public long VisibleTriangles { get; private set; }
    public int ShadowPrimitiveCount => _shadowMesh.PrimitiveCount;
    public int ResidentChunkCount => _streamer?.ActiveCount ?? TotalChunks;
    public int PendingChunkLoads => _streamer?.PendingLoadCount ?? 0;
    public int ChunkLoadsLastUpdate => _streamer?.LoadsLastUpdate ?? 0;
    public int ChunkUnloadsLastUpdate => _streamer?.UnloadsLastUpdate ?? 0;
    public int LastRebuiltChunkCount { get; private set; }
    public long TotalChunkRebuilds { get; private set; }

    public int DeformRadial(float worldX, float worldZ, float radius, float delta)
    {
        if (_heights is not IDeformableHeightProvider deformable)
            throw new InvalidOperationException("This terrain's height provider does not support deformation.");
        var region = deformable.ApplyRadialDelta(worldX, worldZ, radius, delta);
        return RebuildRegion(region);
    }

    public int RebuildRegion(TerrainRegion region)
    {
        var halfSize = _settings.WorldSize * 0.5f;
        var expanded = region.Expand(1.5f);
        if (expanded.MaxX < -halfSize || expanded.MinX > halfSize ||
            expanded.MaxZ < -halfSize || expanded.MinZ > halfSize)
        {
            LastRebuiltChunkCount = 0;
            return 0;
        }

        var chunkSize = _settings.WorldSize / _settings.ChunksPerAxis;
        var minChunkX = Math.Clamp((int)MathF.Floor((expanded.MinX + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        var maxChunkX = Math.Clamp((int)MathF.Floor((expanded.MaxX + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        var minChunkZ = Math.Clamp((int)MathF.Floor((expanded.MinZ + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);
        var maxChunkZ = Math.Clamp((int)MathF.Floor((expanded.MaxZ + halfSize) / chunkSize), 0, _settings.ChunksPerAxis - 1);

        LastRebuiltChunkCount = 0;
        for (var z = minChunkZ; z <= maxChunkZ; z++)
        for (var x = minChunkX; x <= maxChunkX; x++)
        {
            if (_streamer is not null)
            {
                if (_streamer.Reload(new WorldCell(x, z))) LastRebuiltChunkCount++;
            }
            else
            {
                var index = z * _settings.ChunksPerAxis + x;
                var replacement = CreateChunk(_device, x, z, chunkSize, _colorProvider);
                _residentChunks![index].Dispose();
                _residentChunks[index] = replacement;
                LastRebuiltChunkCount++;
            }
        }

        _visible.Clear();
        TotalChunkRebuilds += LastRebuiltChunkCount;
        RebuildShadowMesh();
        return LastRebuiltChunkCount;
    }

    private void RebuildShadowMesh()
    {
        var replacement = CreateGridMesh(_device, -_settings.WorldSize * 0.5f,
            -_settings.WorldSize * 0.5f, _settings.WorldSize, _settings.ShadowSegments, _colorProvider);
        _shadowMesh.Dispose();
        _shadowMesh = replacement;
    }

    public void Update(Camera3D camera) => Update(camera.View, camera.Projection, camera.Position);

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _streamer?.Update(cameraPosition);
        _visible.Clear();
        Array.Clear(VisibleLods);
        VisibleTriangles = 0;
        var frustum = new BoundingFrustum(view * projection);
        var chunks = _streamer is null
            ? _residentChunks!.AsEnumerable()
            : _streamer.ActiveCells.Select(pair => pair.Value);
        foreach (var chunk in chunks)
        {
            if (frustum.Contains(chunk.Bounds) == ContainmentType.Disjoint) continue;
            var offset = new Vector2(cameraPosition.X - chunk.Center.X, cameraPosition.Z - chunk.Center.Z);
            var distanceSquared = offset.LengthSquared();
            var lod = 0;
            while (lod < _settings.LodDistances.Length &&
                   distanceSquared >= _settings.LodDistances[lod] * _settings.LodDistances[lod]) lod++;
            _visible.Add(new VisibleChunk(chunk, lod));
            VisibleLods[lod]++;
            VisibleTriangles += chunk.Meshes[lod].PrimitiveCount;
        }
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        foreach (var item in _visible)
        {
            var mesh = item.Chunk.Meshes[item.Lod];
            mesh.Bind(device);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }
    }

    public void DrawShadow(GraphicsDevice device, Effect effect)
    {
        _shadowMesh.Bind(device);
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _shadowMesh.PrimitiveCount);
        }
    }

    private TerrainChunk CreateChunk(GraphicsDevice device, int chunkX, int chunkZ,
        float chunkSize, Func<float, Color> colorProvider)
    {
        var originX = -_settings.WorldSize * 0.5f + chunkX * chunkSize;
        var originZ = -_settings.WorldSize * 0.5f + chunkZ * chunkSize;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var boundsSegments = _settings.LodSegments.Max();
        for (var z = 0; z <= boundsSegments; z++)
        for (var x = 0; x <= boundsSegments; x++)
        {
            var y = _heights.SampleHeight(originX + x * chunkSize / boundsSegments,
                originZ + z * chunkSize / boundsSegments);
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
        }
        var bounds = new BoundingBox(new Vector3(originX, minY - _settings.BoundsPadding, originZ),
            new Vector3(originX + chunkSize, maxY + _settings.BoundsPadding, originZ + chunkSize));
        var meshes = _settings.LodSegments.Select(segments =>
            CreateGridMesh(device, originX, originZ, chunkSize, segments, colorProvider)).ToArray();
        return new TerrainChunk(meshes, bounds);
    }

    private Mesh CreateGridMesh(GraphicsDevice device, float originX, float originZ,
        float size, int segments, Func<float, Color> colorProvider)
    {
        var vertices = new TerrainVertex[(segments + 1) * (segments + 1)];
        var step = size / segments;
        for (var z = 0; z <= segments; z++)
        for (var x = 0; x <= segments; x++)
        {
            var wx = originX + x * step;
            var wz = originZ + z * step;
            var y = _heights.SampleHeight(wx, wz);
            const float epsilon = 1.5f;
            var normal = Vector3.Normalize(new Vector3(
                _heights.SampleHeight(wx - epsilon, wz) - _heights.SampleHeight(wx + epsilon, wz),
                2f * epsilon,
                _heights.SampleHeight(wx, wz - epsilon) - _heights.SampleHeight(wx, wz + epsilon)));
            vertices[z * (segments + 1) + x] = new TerrainVertex(new Vector3(wx, y, wz), normal, colorProvider(y));
        }

        var indices = new ushort[segments * segments * 6];
        var at = 0;
        for (var z = 0; z < segments; z++)
        for (var x = 0; x < segments; x++)
        {
            var a = (ushort)(z * (segments + 1) + x);
            var b = (ushort)(a + 1);
            var c = (ushort)(a + segments + 1);
            var d = (ushort)(c + 1);
            indices[at++] = a; indices[at++] = c; indices[at++] = d;
            indices[at++] = a; indices[at++] = d; indices[at++] = b;
        }
        return Mesh.Create(device, vertices, indices);
    }

    private static Color DefaultColor(float height) => Color.Lerp(
        new Color(52, 88, 43), new Color(121, 112, 74),
        MathHelper.Clamp((height + 30f) / 120f, 0f, 1f));

    public void Dispose()
    {
        _shadowMesh.Dispose();
        if (_streamer is not null) _streamer.Dispose();
        else foreach (var chunk in _residentChunks!) chunk.Dispose();
    }

    private sealed class TerrainChunk : IDisposable
    {
        public TerrainChunk(Mesh[] meshes, BoundingBox bounds)
        {
            Meshes = meshes;
            Bounds = bounds;
            Center = bounds.Min + (bounds.Max - bounds.Min) * 0.5f;
        }
        public Mesh[] Meshes { get; }
        public BoundingBox Bounds { get; }
        public Vector3 Center { get; }
        public void Dispose() { foreach (var mesh in Meshes) mesh.Dispose(); }
    }

    private readonly record struct VisibleChunk(TerrainChunk Chunk, int Lod);
}

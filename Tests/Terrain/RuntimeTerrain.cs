using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Terrain;

internal enum TerrainBrushMode { Raise, Lower, Flatten, Smooth }

internal sealed class RuntimeTerrain : IDisposable
{
    private const int CellsPerChunk = 32;
    private const int ChunkCount = 4;
    private const int TotalCells = CellsPerChunk * ChunkCount;
    private const float Size = 28f;
    private const float Spacing = Size / TotalCells;
    private readonly GraphicsDevice _device;
    private readonly float[,] _heights = new float[TotalCells + 1, TotalCells + 1];
    private readonly TerrainChunk[,] _chunks = new TerrainChunk[ChunkCount, ChunkCount];

    public RuntimeTerrain(GraphicsDevice device)
    {
        _device = device;
        for (var z = 0; z <= TotalCells; z++)
        for (var x = 0; x <= TotalCells; x++)
            _heights[x, z] = InitialHeight(GridToWorld(x), GridToWorld(z));

        for (var z = 0; z < ChunkCount; z++)
        for (var x = 0; x < ChunkCount; x++)
            _chunks[x, z] = new TerrainChunk(device, x * CellsPerChunk, z * CellsPerChunk);
        RebuildAll();
    }

    public int LastUpdatedChunkCount { get; private set; }

    public void Draw()
    {
        foreach (var chunk in _chunks)
            chunk.Draw(_device);
    }

    public bool Raycast(Ray ray, out Vector3 hit)
    {
        const float step = 0.18f;
        var previousT = 0f;
        var previousAbove = true;
        for (var t = 0f; t < 120f; t += step)
        {
            var point = ray.Position + ray.Direction * t;
            if (!Contains(point.X, point.Z))
            {
                previousT = t;
                continue;
            }

            var above = point.Y >= SampleHeight(point.X, point.Z);
            if (previousAbove && !above)
            {
                var low = previousT;
                var high = t;
                for (var i = 0; i < 10; i++)
                {
                    var middle = (low + high) * 0.5f;
                    var candidate = ray.Position + ray.Direction * middle;
                    if (candidate.Y >= SampleHeight(candidate.X, candidate.Z)) low = middle;
                    else high = middle;
                }
                hit = ray.Position + ray.Direction * ((low + high) * 0.5f);
                hit.Y = SampleHeight(hit.X, hit.Z);
                return true;
            }
            previousAbove = above;
            previousT = t;
        }
        hit = default;
        return false;
    }

    public void ApplyBrush(Vector3 center, float radius, float amount, TerrainBrushMode mode, float flattenHeight)
    {
        var minX = Math.Max(0, WorldToGrid(center.X - radius));
        var maxX = Math.Min(TotalCells, WorldToGrid(center.X + radius) + 1);
        var minZ = Math.Max(0, WorldToGrid(center.Z - radius));
        var maxZ = Math.Min(TotalCells, WorldToGrid(center.Z + radius) + 1);
        float[,]? source = mode == TerrainBrushMode.Smooth ? (float[,])_heights.Clone() : null;

        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = GridToWorld(x) - center.X;
            var dz = GridToWorld(z) - center.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance >= radius)
                continue;
            var falloff = 1f - distance / radius;
            falloff = falloff * falloff * (3f - 2f * falloff);
            switch (mode)
            {
                case TerrainBrushMode.Raise:
                    _heights[x, z] += amount * falloff;
                    break;
                case TerrainBrushMode.Lower:
                    _heights[x, z] -= amount * falloff;
                    break;
                case TerrainBrushMode.Flatten:
                    _heights[x, z] = MathHelper.Lerp(_heights[x, z], flattenHeight, MathHelper.Clamp(amount * falloff, 0f, 1f));
                    break;
                case TerrainBrushMode.Smooth:
                    var average = NeighborhoodAverage(source!, x, z);
                    _heights[x, z] = MathHelper.Lerp(source![x, z], average, MathHelper.Clamp(amount * falloff, 0f, 1f));
                    break;
            }
        }

        RebuildRegion(minX - 1, minZ - 1, maxX + 1, maxZ + 1);
    }

    private float NeighborhoodAverage(float[,] source, int centerX, int centerZ)
    {
        var sum = 0f;
        var count = 0;
        for (var z = Math.Max(0, centerZ - 1); z <= Math.Min(TotalCells, centerZ + 1); z++)
        for (var x = Math.Max(0, centerX - 1); x <= Math.Min(TotalCells, centerX + 1); x++)
        {
            sum += source[x, z];
            count++;
        }
        return sum / count;
    }

    private void RebuildAll() => RebuildRegion(0, 0, TotalCells, TotalCells);

    private void RebuildRegion(int minGridX, int minGridZ, int maxGridX, int maxGridZ)
    {
        var minChunkX = Math.Clamp(Math.Max(0, minGridX) / CellsPerChunk, 0, ChunkCount - 1);
        var minChunkZ = Math.Clamp(Math.Max(0, minGridZ) / CellsPerChunk, 0, ChunkCount - 1);
        var maxChunkX = Math.Clamp(Math.Min(TotalCells - 1, maxGridX) / CellsPerChunk, 0, ChunkCount - 1);
        var maxChunkZ = Math.Clamp(Math.Min(TotalCells - 1, maxGridZ) / CellsPerChunk, 0, ChunkCount - 1);
        LastUpdatedChunkCount = 0;
        for (var z = minChunkZ; z <= maxChunkZ; z++)
        for (var x = minChunkX; x <= maxChunkX; x++)
        {
            _chunks[x, z].UpdateVertices(BuildChunkVertices(x * CellsPerChunk, z * CellsPerChunk));
            LastUpdatedChunkCount++;
        }
    }

    private TerrainVertex[] BuildChunkVertices(int startX, int startZ)
    {
        var vertices = new TerrainVertex[(CellsPerChunk + 1) * (CellsPerChunk + 1)];
        var cursor = 0;
        for (var localZ = 0; localZ <= CellsPerChunk; localZ++)
        for (var localX = 0; localX <= CellsPerChunk; localX++)
        {
            var x = startX + localX;
            var z = startZ + localZ;
            var left = _heights[Math.Max(0, x - 1), z];
            var right = _heights[Math.Min(TotalCells, x + 1), z];
            var down = _heights[x, Math.Max(0, z - 1)];
            var up = _heights[x, Math.Min(TotalCells, z + 1)];
            var normal = Vector3.Normalize(new Vector3(left - right, Spacing * 2f, down - up));
            vertices[cursor++] = new TerrainVertex(new Vector3(GridToWorld(x), _heights[x, z], GridToWorld(z)), normal);
        }
        return vertices;
    }

    private float SampleHeight(float worldX, float worldZ)
    {
        var gx = (worldX + Size * 0.5f) / Spacing;
        var gz = (worldZ + Size * 0.5f) / Spacing;
        var x0 = Math.Clamp((int)MathF.Floor(gx), 0, TotalCells - 1);
        var z0 = Math.Clamp((int)MathF.Floor(gz), 0, TotalCells - 1);
        var tx = gx - x0;
        var tz = gz - z0;
        return MathHelper.Lerp(
            MathHelper.Lerp(_heights[x0, z0], _heights[x0 + 1, z0], tx),
            MathHelper.Lerp(_heights[x0, z0 + 1], _heights[x0 + 1, z0 + 1], tx), tz);
    }

    private static bool Contains(float x, float z) => x >= -Size * 0.5f && x <= Size * 0.5f && z >= -Size * 0.5f && z <= Size * 0.5f;
    private static float GridToWorld(int value) => value * Spacing - Size * 0.5f;
    private static int WorldToGrid(float value) => (int)MathF.Floor((value + Size * 0.5f) / Spacing);

    private static float InitialHeight(float x, float z)
    {
        var rolling = MathF.Sin(x * 0.32f) * MathF.Cos(z * 0.27f) * 0.55f;
        var mountain = 4.2f * MathF.Exp(-(x * x + z * z) / 32f);
        var ridge = 2.2f * MathF.Exp(-((x + 6f) * (x + 6f) + (z - 3f) * (z - 3f)) / 9f);
        var basin = -1.35f * MathF.Exp(-((x - 7f) * (x - 7f) + (z + 5f) * (z + 5f)) / 16f);
        return rolling + mountain + ridge + basin - 0.8f;
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks)
            chunk.Dispose();
    }

    private sealed class TerrainChunk : IDisposable
    {
        private readonly DynamicVertexBuffer _vertices;
        private readonly IndexBuffer _indices;

        public TerrainChunk(GraphicsDevice device, int startX, int startZ)
        {
            _vertices = new DynamicVertexBuffer(device, TerrainVertex.VertexDeclaration,
                (CellsPerChunk + 1) * (CellsPerChunk + 1), BufferUsage.WriteOnly);
            var indices = new ushort[CellsPerChunk * CellsPerChunk * 6];
            var cursor = 0;
            var stride = CellsPerChunk + 1;
            for (var z = 0; z < CellsPerChunk; z++)
            for (var x = 0; x < CellsPerChunk; x++)
            {
                var topLeft = (ushort)(z * stride + x);
                var topRight = (ushort)(topLeft + 1);
                var bottomLeft = (ushort)(topLeft + stride);
                var bottomRight = (ushort)(bottomLeft + 1);
                indices[cursor++] = topLeft; indices[cursor++] = bottomRight; indices[cursor++] = topRight;
                indices[cursor++] = topLeft; indices[cursor++] = bottomLeft; indices[cursor++] = bottomRight;
            }
            _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indices.SetData(indices);
        }

        public void UpdateVertices(TerrainVertex[] vertices) =>
            _vertices.SetData(vertices, 0, vertices.Length, SetDataOptions.Discard);

        public void Draw(GraphicsDevice device)
        {
            device.SetVertexBuffer(_vertices);
            device.Indices = _indices;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, CellsPerChunk * CellsPerChunk * 2);
        }

        public void Dispose() { _vertices.Dispose(); _indices.Dispose(); }
    }
}

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.World.Terrain;
using Nova3D.World.Streaming;
using Nova3D.Production.Configuration;

namespace CityBuilder.Benchmarks.CityBenchmark;

/// <summary>CityBenchmark terrain configuration over Nova3D's chunked terrain.</summary>
internal sealed class LargeWorldTerrain : IDisposable
{
    public const int ChunkCountPerAxis = 32;
    public const int TotalChunks = ChunkCountPerAxis * ChunkCountPerAxis;
    public const float WorldSize = 2048f;
    private readonly DeformableHeightProvider _heights;
    private readonly ChunkedTerrain _terrain;

    public LargeWorldTerrain(GraphicsDevice device, StreamingConfiguration streamingConfiguration)
    {
        _heights = new DeformableHeightProvider(new DelegateHeightProvider(SampleHeight));
        _terrain = new ChunkedTerrain(device, _heights,
            new ChunkedTerrainSettings
            {
                WorldSize = WorldSize,
                ChunksPerAxis = ChunkCountPerAxis,
                LodSegments = new[] { 16, 8, 4 },
                LodDistances = new[] { 300f, 760f },
                ShadowSegments = 128,
                BoundsPadding = 8f,
                Streaming = new WorldStreamingSettings
                {
                    CellSize = WorldSize / ChunkCountPerAxis,
                    Origin = new Vector2(-WorldSize * 0.5f),
                    LoadRadius = streamingConfiguration.LoadRadius,
                    RetainRadius = streamingConfiguration.RetainRadius,
                    MaxLoadsPerUpdate = streamingConfiguration.MaxLoadsPerFrame,
                    MaxUnloadsPerUpdate = streamingConfiguration.MaxUnloadsPerFrame
                }
            });
    }

    public int[] VisibleLods => _terrain.VisibleLods;
    public int VisibleCount => _terrain.VisibleCount;
    public int DrawCalls => _terrain.DrawCalls;
    public long VisibleTriangles => _terrain.VisibleTriangles;
    public int ShadowPrimitiveCount => _terrain.ShadowPrimitiveCount;
    public int LastRebuiltChunkCount => _terrain.LastRebuiltChunkCount;
    public int ResidentChunkCount => _terrain.ResidentChunkCount;
    public int PendingChunkLoads => _terrain.PendingChunkLoads;
    public int ChunkLoadsLastUpdate => _terrain.ChunkLoadsLastUpdate;
    public int ChunkUnloadsLastUpdate => _terrain.ChunkUnloadsLastUpdate;
    public IHeightProvider HeightProvider => _heights;

    public TerrainRegion DeformRadial(float x, float z, float radius, float delta)
    {
        TerrainRegion region = _heights.ApplyRadialDelta(x, z, radius, delta);
        _terrain.RebuildRegion(region);
        return region;
    }

    public static float SampleHeight(float x, float z)
    {
        var broad = MathF.Sin(x * 0.006f) * 20f + MathF.Cos(z * 0.0075f) * 16f;
        var detail = MathF.Sin((x + z) * 0.021f) * 4f + MathF.Cos((x - z) * 0.016f) * 3f;
        var centralHill = 75f * MathF.Exp(-(x * x + z * z) / 170000f);
        return broad + detail + centralHill;
    }

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition) =>
        _terrain.Update(view, projection, cameraPosition);

    public void Draw(GraphicsDevice device, Effect effect) => _terrain.Draw(device, effect);
    public void DrawShadow(GraphicsDevice device, Effect effect) => _terrain.DrawShadow(device, effect);
    public void Dispose() => _terrain.Dispose();
}

// Benchmark geometry outside the terrain still uses this compatible layout.
[StructLayout(LayoutKind.Sequential)]
internal readonly struct WorldVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    public WorldVertex(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Color Color;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

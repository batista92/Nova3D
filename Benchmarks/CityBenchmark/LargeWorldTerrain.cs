using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Benchmarks.CityBenchmark;

internal sealed class LargeWorldTerrain : IDisposable
{
    public const int ChunkCountPerAxis = 32;
    public const int TotalChunks = ChunkCountPerAxis * ChunkCountPerAxis;
    public const float WorldSize = 2048f;
    private const float ChunkSize = WorldSize / ChunkCountPerAxis;
    private static readonly int[] Segments = { 16, 8, 4 };
    private readonly Chunk[] _chunks = new Chunk[TotalChunks];
    private readonly List<VisibleChunk> _visible = new(TotalChunks);
    private readonly ShadowMesh _shadowMesh;

    public int[] VisibleLods { get; } = new int[3];
    public int VisibleCount => _visible.Count;
    public int DrawCalls => _visible.Count;
    public long VisibleTriangles { get; private set; }
    public int ShadowPrimitiveCount => _shadowMesh.PrimitiveCount;

    public LargeWorldTerrain(GraphicsDevice device)
    {
        _shadowMesh = new ShadowMesh(device, 128);
        var index = 0;
        for (var z = 0; z < ChunkCountPerAxis; z++)
        for (var x = 0; x < ChunkCountPerAxis; x++)
            _chunks[index++] = new Chunk(device, x, z);
    }

    public static float SampleHeight(float x, float z)
    {
        var broad = MathF.Sin(x * 0.006f) * 20f + MathF.Cos(z * 0.0075f) * 16f;
        var detail = MathF.Sin((x + z) * 0.021f) * 4f + MathF.Cos((x - z) * 0.016f) * 3f;
        var centralHill = 75f * MathF.Exp(-(x * x + z * z) / 170000f);
        return broad + detail + centralHill;
    }

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _visible.Clear();
        Array.Clear(VisibleLods);
        VisibleTriangles = 0;
        var frustum = new BoundingFrustum(view * projection);
        foreach (var chunk in _chunks)
        {
            if (frustum.Contains(chunk.Bounds) == ContainmentType.Disjoint) continue;
            var distance = Vector2.Distance(new Vector2(cameraPosition.X, cameraPosition.Z), new Vector2(chunk.Center.X, chunk.Center.Z));
            var lod = distance < 300f ? 0 : distance < 760f ? 1 : 2;
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
            device.SetVertexBuffer(mesh.VertexBuffer);
            device.Indices = mesh.IndexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }
    }

    public void DrawShadow(GraphicsDevice device, Effect effect)
    {
        device.SetVertexBuffer(_shadowMesh.VertexBuffer);
        device.Indices = _shadowMesh.IndexBuffer;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _shadowMesh.PrimitiveCount);
        }
    }

    public void Dispose()
    {
        _shadowMesh.Dispose();
        foreach (var chunk in _chunks) chunk.Dispose();
    }

    private sealed class ShadowMesh : IDisposable
    {
        public readonly VertexBuffer VertexBuffer;
        public readonly IndexBuffer IndexBuffer;
        public readonly int PrimitiveCount;

        public ShadowMesh(GraphicsDevice device, int segments)
        {
            var vertices = new WorldVertex[(segments + 1) * (segments + 1)];
            var step = WorldSize / segments;
            for (var z = 0; z <= segments; z++)
            for (var x = 0; x <= segments; x++)
            {
                var wx = -WorldSize * 0.5f + x * step;
                var wz = -WorldSize * 0.5f + z * step;
                vertices[z * (segments + 1) + x] = new WorldVertex(
                    new Vector3(wx, SampleHeight(wx, wz), wz), Vector3.Up, Color.White);
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

            VertexBuffer = new VertexBuffer(device, WorldVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
            PrimitiveCount = indices.Length / 3;
        }

        public void Dispose() { VertexBuffer.Dispose(); IndexBuffer.Dispose(); }
    }

    private sealed class Chunk : IDisposable
    {
        public readonly TerrainLodMesh[] Meshes = new TerrainLodMesh[3];
        public readonly BoundingBox Bounds;
        public readonly Vector3 Center;

        public Chunk(GraphicsDevice device, int chunkX, int chunkZ)
        {
            var originX = -WorldSize * 0.5f + chunkX * ChunkSize;
            var originZ = -WorldSize * 0.5f + chunkZ * ChunkSize;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var z = 0; z <= 4; z++)
            for (var x = 0; x <= 4; x++)
            {
                var y = SampleHeight(originX + x * ChunkSize / 4f, originZ + z * ChunkSize / 4f);
                minY = MathF.Min(minY, y); maxY = MathF.Max(maxY, y);
            }
            Bounds = new BoundingBox(new Vector3(originX, minY - 8f, originZ), new Vector3(originX + ChunkSize, maxY + 8f, originZ + ChunkSize));
            Center = Bounds.Min + (Bounds.Max - Bounds.Min) * 0.5f;
            for (var lod = 0; lod < 3; lod++) Meshes[lod] = new TerrainLodMesh(device, originX, originZ, Segments[lod]);
        }

        public void Dispose() { foreach (var mesh in Meshes) mesh.Dispose(); }
    }

    private sealed class TerrainLodMesh : IDisposable
    {
        public readonly VertexBuffer VertexBuffer;
        public readonly IndexBuffer IndexBuffer;
        public readonly int PrimitiveCount;

        public TerrainLodMesh(GraphicsDevice device, float originX, float originZ, int segments)
        {
            var vertices = new WorldVertex[(segments + 1) * (segments + 1)];
            var step = ChunkSize / segments;
            for (var z = 0; z <= segments; z++)
            for (var x = 0; x <= segments; x++)
            {
                var wx = originX + x * step; var wz = originZ + z * step; var y = SampleHeight(wx, wz);
                const float epsilon = 1.5f;
                var normal = Vector3.Normalize(new Vector3(SampleHeight(wx - epsilon, wz) - SampleHeight(wx + epsilon, wz), 2f * epsilon, SampleHeight(wx, wz - epsilon) - SampleHeight(wx, wz + epsilon)));
                var color = Color.Lerp(new Color(52, 88, 43), new Color(121, 112, 74), MathHelper.Clamp((y + 30f) / 120f, 0f, 1f));
                vertices[z * (segments + 1) + x] = new WorldVertex(new Vector3(wx, y, wz), normal, color);
            }
            var indices = new ushort[segments * segments * 6]; var at = 0;
            for (var z = 0; z < segments; z++) for (var x = 0; x < segments; x++)
            {
                var a = (ushort)(z * (segments + 1) + x); var b = (ushort)(a + 1); var c = (ushort)(a + segments + 1); var d = (ushort)(c + 1);
                indices[at++] = a; indices[at++] = c; indices[at++] = d;
                indices[at++] = a; indices[at++] = d; indices[at++] = b;
            }
            VertexBuffer = new VertexBuffer(device, WorldVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly); VertexBuffer.SetData(vertices);
            IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly); IndexBuffer.SetData(indices);
            PrimitiveCount = indices.Length / 3;
        }
        public void Dispose() { VertexBuffer.Dispose(); IndexBuffer.Dispose(); }
    }

    private readonly record struct VisibleChunk(Chunk Chunk, int Lod);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct WorldVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    public WorldVertex(Vector3 position, Vector3 normal, Color color) { Position = position; Normal = normal; Color = color; }
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Color Color;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

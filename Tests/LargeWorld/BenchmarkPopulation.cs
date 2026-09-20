using CityBuilder.Tests.Vegetation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class BenchmarkPopulation : IDisposable
{
    private readonly InstanceBatch _buildings;
    private readonly InstanceBatch _vehicles;

    public BenchmarkPopulation(GraphicsDevice device)
    {
        _buildings = new InstanceBatch(device, CreateBuildingMesh(device), CreateBuildings(), 1900f, 15f);
        _vehicles = new InstanceBatch(device, CreateVehicleMesh(device), CreateVehicles(), 1300f, 4f);
    }

    public int VisibleBuildings => _buildings.VisibleCount;
    public int VisibleVehicles => _vehicles.VisibleCount;
    public int DrawCalls => _buildings.DrawCalls + _vehicles.DrawCalls;
    public long VisibleTriangles => _buildings.VisibleTriangles + _vehicles.VisibleTriangles;

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _buildings.Update(view, projection, cameraPosition);
        _vehicles.Update(view, projection, cameraPosition);
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        _buildings.Draw(device, effect);
        _vehicles.Draw(device, effect);
    }

    private static Matrix[] CreateBuildings()
    {
        var result = new Matrix[1000];
        var random = new Random(8042);
        var index = 0;
        for (var row = 0; index < result.Length; row++)
        for (var column = 0; column < 48 && index < result.Length; column++)
        {
            // Every sixth lot is a proper street; the lake and central park
            // remain empty instead of being covered by boxes.
            if (column % 6 == 0 || row % 6 == 0) continue;
            var x = (column - 23.5f) * 32f;
            var z = (row - 14f) * 38f;
            if (InsideLake(x, z) || (MathF.Abs(x) < 115f && MathF.Abs(z) < 90f)) continue;
            var width = 13f + (float)random.NextDouble() * 9f;
            var depth = 15f + (float)random.NextDouble() * 10f;
            var downtown = 1f - MathHelper.Clamp(MathF.Sqrt(x * x + z * z) / 850f, 0f, 1f);
            var height = 10f + MathF.Pow((float)random.NextDouble(), 1.6f) * (32f + downtown * 70f);
            var y = LargeWorldTerrain.SampleHeight(x, z);
            result[index++] = Matrix.CreateScale(width, height, depth) * Matrix.CreateTranslation(x, y, z);
        }
        return result;
    }

    private static Matrix[] CreateVehicles()
    {
        var result = new Matrix[500];
        var random = new Random(1911);
        for (var i = 0; i < result.Length; i++)
        {
            var horizontal = (i & 1) == 0;
            var street = i % 5 - 2;
            var along = ((i * 47) % 500) / 499f * 1450f - 725f;
            var x = horizontal ? along : street * 192f + 16f;
            var z = horizontal ? street * 228f - 76f : along;
            x += (float)(random.NextDouble() - 0.5) * 2f;
            z += (float)(random.NextDouble() - 0.5) * 2f;
            var y = LargeWorldTerrain.SampleHeight(x, z) + 0.35f;
            var yaw = horizontal ? MathHelper.PiOver2 : 0f;
            result[i] = Matrix.CreateScale(1.8f, 1.25f, 4.2f) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z);
        }
        return result;
    }

    internal static bool InsideLake(float x, float z)
    {
        var dx = (x - 420f) / 235f;
        var dz = (z + 330f) / 165f;
        return dx * dx + dz * dz < 1.15f;
    }

    private static BenchmarkMesh CreateBuildingMesh(GraphicsDevice device)
    {
        return BenchmarkMesh.CreateBox(device, new Color(68, 79, 91), new Color(112, 126, 138));
    }

    private static BenchmarkMesh CreateVehicleMesh(GraphicsDevice device)
    {
        return BenchmarkMesh.CreateBox(device, new Color(165, 45, 38), new Color(225, 112, 48));
    }

    public void Dispose()
    {
        _buildings.Dispose();
        _vehicles.Dispose();
    }

    private sealed class InstanceBatch : IDisposable
    {
        private readonly BenchmarkMesh _mesh;
        private readonly Matrix[] _transforms;
        private readonly InstanceVertex[] _visible;
        private readonly DynamicVertexBuffer _instances;
        private readonly float _cullDistance;
        private readonly float _boundsRadius;

        public InstanceBatch(GraphicsDevice device, BenchmarkMesh mesh, Matrix[] transforms, float cullDistance, float boundsRadius)
        {
            _mesh = mesh;
            _transforms = transforms;
            _visible = new InstanceVertex[transforms.Length];
            _instances = new DynamicVertexBuffer(device, InstanceVertex.VertexDeclaration, transforms.Length, BufferUsage.WriteOnly);
            _cullDistance = cullDistance;
            _boundsRadius = boundsRadius;
        }

        public int VisibleCount { get; private set; }
        public int DrawCalls => VisibleCount > 0 ? 1 : 0;
        public long VisibleTriangles => (long)VisibleCount * _mesh.PrimitiveCount;

        public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            VisibleCount = 0;
            var frustum = new BoundingFrustum(view * projection);
            foreach (var transform in _transforms)
            {
                var position = transform.Translation;
                if (Vector3.DistanceSquared(cameraPosition, position) > _cullDistance * _cullDistance ||
                    frustum.Contains(new BoundingSphere(position + Vector3.Up * (_boundsRadius * 0.5f), _boundsRadius)) == ContainmentType.Disjoint)
                    continue;
                _visible[VisibleCount++] = new InstanceVertex(transform);
            }
            if (VisibleCount > 0)
                _instances.SetData(_visible, 0, VisibleCount, SetDataOptions.Discard);
        }

        public void Draw(GraphicsDevice device, Effect effect)
        {
            if (VisibleCount == 0) return;
            device.SetVertexBuffers(new VertexBufferBinding(_mesh.VertexBuffer), new VertexBufferBinding(_instances, 0, 1));
            device.Indices = _mesh.IndexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _mesh.PrimitiveCount, VisibleCount);
            }
        }

        public void Dispose()
        {
            _instances.Dispose();
            _mesh.Dispose();
        }
    }
}

internal sealed class BenchmarkMesh : IDisposable
{
    private BenchmarkMesh(GraphicsDevice device, WorldVertex[] vertices, ushort[] indices)
    {
        VertexBuffer = new VertexBuffer(device, WorldVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices);
        IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);
        PrimitiveCount = indices.Length / 3;
    }

    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public int PrimitiveCount { get; }

    public static BenchmarkMesh CreateBox(GraphicsDevice device, Color side, Color top)
    {
        var vertices = new List<WorldVertex>();
        var indices = new List<ushort>();
        // Each face uses axisA x axisB as its geometric outward normal.
        // The previous ordering pointed all four walls into the box.
        AddFace(vertices, indices, new(0.5f, 0, -0.5f), new(-1, 0, 0), new(0, 1, 0), Vector3.Forward, side);
        AddFace(vertices, indices, new(-0.5f, 0, 0.5f), new(1, 0, 0), new(0, 1, 0), Vector3.Backward, side);
        AddFace(vertices, indices, new(-0.5f, 0, -0.5f), new(0, 0, 1), new(0, 1, 0), Vector3.Left, side);
        AddFace(vertices, indices, new(0.5f, 0, 0.5f), new(0, 0, -1), new(0, 1, 0), Vector3.Right, side);
        AddFace(vertices, indices, new(-0.5f, 1, 0.5f), new(1, 0, 0), new(0, 0, -1), Vector3.Up, top);
        return new BenchmarkMesh(device, vertices.ToArray(), indices.ToArray());
    }

    private static void AddFace(List<WorldVertex> vertices, List<ushort> indices, Vector3 origin, Vector3 axisA, Vector3 axisB, Vector3 normal, Color color)
    {
        var start = (ushort)vertices.Count;
        vertices.Add(new WorldVertex(origin, normal, color));
        vertices.Add(new WorldVertex(origin + axisA, normal, color));
        vertices.Add(new WorldVertex(origin + axisA + axisB, normal, color));
        vertices.Add(new WorldVertex(origin + axisB, normal, color));
        indices.Add(start); indices.Add((ushort)(start + 1)); indices.Add((ushort)(start + 2));
        indices.Add(start); indices.Add((ushort)(start + 2)); indices.Add((ushort)(start + 3));
    }

    public void Dispose() { VertexBuffer.Dispose(); IndexBuffer.Dispose(); }
}

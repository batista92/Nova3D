using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;
using Nova3D.Rendering.Instancing;

namespace CityBuilder.Benchmarks.CityBenchmark;

internal sealed class BenchmarkPopulation : IDisposable
{
    private readonly InstancedMeshBatch _buildings;
    private readonly InstancedMeshBatch _vehicles;

    public BenchmarkPopulation(GraphicsDevice device)
    {
        _buildings = new InstancedMeshBatch(device, CreateBuildingMesh(device), CreateBuildings(),
            1900f, 15f, Vector3.Up * 7.5f);
        _vehicles = new InstancedMeshBatch(device, CreateVehicleMesh(device), CreateVehicles(),
            1300f, 4f, Vector3.Up * 2f);
    }

    public int VisibleBuildings => _buildings.VisibleCount;
    public int VisibleVehicles => _vehicles.VisibleCount;
    public int CandidateCount => _buildings.LastCandidateCount + _vehicles.LastCandidateCount;
    public int DrawCalls => _buildings.DrawCalls + _vehicles.DrawCalls;
    public long VisibleTriangles => _buildings.VisibleTriangles + _vehicles.VisibleTriangles;

    public void Update(Camera3D camera)
    {
        _buildings.Update(camera);
        _vehicles.Update(camera);
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

    private static Mesh CreateBuildingMesh(GraphicsDevice device)
    {
        return CreateBox(device, new Color(68, 79, 91), new Color(112, 126, 138));
    }

    private static Mesh CreateVehicleMesh(GraphicsDevice device)
    {
        return CreateBox(device, new Color(165, 45, 38), new Color(225, 112, 48));
    }

    private static Mesh CreateBox(GraphicsDevice device, Color side, Color top)
    {
        var vertices = new List<WorldVertex>();
        var indices = new List<ushort>();
        AddFace(vertices, indices, new(0.5f, 0, -0.5f), new(-1, 0, 0), new(0, 1, 0), Vector3.Forward, side);
        AddFace(vertices, indices, new(-0.5f, 0, 0.5f), new(1, 0, 0), new(0, 1, 0), Vector3.Backward, side);
        AddFace(vertices, indices, new(-0.5f, 0, -0.5f), new(0, 0, 1), new(0, 1, 0), Vector3.Left, side);
        AddFace(vertices, indices, new(0.5f, 0, 0.5f), new(0, 0, -1), new(0, 1, 0), Vector3.Right, side);
        AddFace(vertices, indices, new(-0.5f, 1, 0.5f), new(1, 0, 0), new(0, 0, -1), Vector3.Up, top);
        return Mesh.Create(device, vertices.ToArray(), indices.ToArray());
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

    public void Dispose()
    {
        _buildings.Dispose();
        _vehicles.Dispose();
    }

}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;
using Nova3D.Rendering.Instancing;

namespace CityBuilder.Tests.Vegetation;

internal sealed class InstancedForest : IDisposable
{
    private const int TreeCount = 10_000;
    private readonly TreeMesh[] _meshes;
    private readonly LodInstancedMeshBatch _batch;
    private readonly Matrix[] _transforms = new Matrix[TreeCount];
    private readonly bool[] _enabled = new bool[TreeCount];

    public InstancedForest(
        GraphicsDevice device,
        float areaSize = 340f,
        float lod0Distance = 50f,
        float lod1Distance = 150f,
        float cullDistance = 400f,
        Func<float, float, float>? heightProvider = null,
        Func<float, float, bool>? placementAllowed = null)
    {
        _meshes = new[] { new TreeMesh(device, 10, 3), new TreeMesh(device, 6, 2), new TreeMesh(device, 4, 1) };
        var random = new Random(9127);
        for (var i = 0; i < TreeCount; i++)
        {
            var gx = i % 100;
            var gz = i / 100;
            var spacing = areaSize / 100f;
            var x = (gx - 49.5f) * spacing + (float)(random.NextDouble() - 0.5) * spacing * 0.7f;
            var z = (gz - 49.5f) * spacing + (float)(random.NextDouble() - 0.5) * spacing * 0.7f;
            var scale = 0.72f + (float)random.NextDouble() * 0.75f;
            var yaw = (float)random.NextDouble() * MathF.Tau;
            var y = heightProvider?.Invoke(x, z) ?? 0f;
            _transforms[i] = Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z);
            _enabled[i] = placementAllowed?.Invoke(x, z) ?? true;
        }
        var lodMeshes = _meshes.Select(mesh =>
            new Mesh(mesh.VertexBuffer, mesh.IndexBuffer, mesh.PrimitiveCount, ownsBuffers: false)).ToArray();
        _batch = new LodInstancedMeshBatch(device, lodMeshes, _transforms,
            new[] { lod0Distance, lod1Distance }, cullDistance, 2.4f,
            Vector3.Up * 1.6f, _enabled, ownsMeshes: true);
    }

    public IReadOnlyList<int> VisibleCounts => _batch.VisibleCounts;
    public int TotalVisible => _batch.TotalVisible;
    public int CandidateCount => _batch.LastCandidateCount;
    public int DrawCalls => _batch.DrawCalls;
    public long VisibleTriangles => _batch.VisibleTriangles;

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _batch.Update(view, projection, cameraPosition);
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        _batch.Draw(device, effect);
    }

    public void Dispose()
    {
        _batch.Dispose();
        foreach (var mesh in _meshes) mesh.Dispose();
    }
}

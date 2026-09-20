using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;
using Nova3D.Rendering.Instancing;

namespace Nova3D.World.Vegetation;

public sealed class VegetationSystem : IDisposable
{
    private readonly LodInstancedMeshBatch _batch;

    public VegetationSystem(GraphicsDevice device, IReadOnlyList<Mesh> lodMeshes,
        VegetationInstances instances, IReadOnlyList<float> lodDistances,
        float cullDistance, float boundsRadius, Vector3 boundsOffset,
        float spatialCellSize = 128f)
    {
        ArgumentNullException.ThrowIfNull(instances);
        _batch = new LodInstancedMeshBatch(device, lodMeshes, instances.Transforms,
            lodDistances, cullDistance, boundsRadius, boundsOffset, instances.Enabled,
            ownsMeshes: true, spatialCellSize: spatialCellSize);
    }

    public IReadOnlyList<int> VisibleCounts => _batch.VisibleCounts;
    public int TotalVisible => _batch.TotalVisible;
    public int CandidateCount => _batch.LastCandidateCount;
    public int DrawCalls => _batch.DrawCalls;
    public long VisibleTriangles => _batch.VisibleTriangles;

    public void Update(Camera3D camera) => _batch.Update(camera);
    public void Draw(GraphicsDevice device, Effect effect) => _batch.Draw(device, effect);
    public void Dispose() => _batch.Dispose();
}

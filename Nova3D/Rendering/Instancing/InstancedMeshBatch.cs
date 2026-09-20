using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.World.Spatial;

namespace Nova3D.Rendering.Instancing;

/// <summary>One mesh rendered from a dynamic, CPU-culled transform buffer.</summary>
public sealed class InstancedMeshBatch : IDisposable
{
    private readonly Mesh _mesh;
    private readonly bool _ownsMesh;
    private readonly Matrix[] _transforms;
    private readonly InstanceTransform[] _visible;
    private readonly DynamicVertexBuffer _instanceBuffer;
    private readonly StaticSpatialGrid _spatialGrid;
    private readonly List<int> _candidates = new();

    public InstancedMeshBatch(GraphicsDevice device, Mesh mesh, Matrix[] transforms,
        float cullDistance, float boundsRadius, Vector3 boundsOffset = default, bool ownsMesh = true,
        float spatialCellSize = 128f)
    {
        ArgumentNullException.ThrowIfNull(device);
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _transforms = transforms ?? throw new ArgumentNullException(nameof(transforms));
        if (transforms.Length == 0) throw new ArgumentException("At least one transform is required.", nameof(transforms));
        if (cullDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(cullDistance));
        if (boundsRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(boundsRadius));
        CullDistance = cullDistance;
        BoundsRadius = boundsRadius;
        BoundsOffset = boundsOffset;
        _ownsMesh = ownsMesh;
        _visible = new InstanceTransform[transforms.Length];
        _spatialGrid = new StaticSpatialGrid(transforms.Select(transform => transform.Translation).ToArray(),
            spatialCellSize);
        _instanceBuffer = new DynamicVertexBuffer(device, InstanceTransform.VertexDeclaration,
            transforms.Length, BufferUsage.WriteOnly);
    }

    public float CullDistance { get; set; }
    public float BoundsRadius { get; set; }
    public Vector3 BoundsOffset { get; set; }
    public int VisibleCount { get; private set; }
    public int LastCandidateCount { get; private set; }
    public int DrawCalls => VisibleCount > 0 ? 1 : 0;
    public long VisibleTriangles => (long)VisibleCount * _mesh.PrimitiveCount;

    public void Update(Camera3D camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        VisibleCount = 0;
        var maximumDistanceSquared = CullDistance * CullDistance;
        var frustum = camera.Frustum;
        _spatialGrid.Query(camera.Position, CullDistance, _candidates);
        LastCandidateCount = _candidates.Count;
        foreach (var index in _candidates)
        {
            var transform = _transforms[index];
            var position = transform.Translation;
            if (Vector3.DistanceSquared(camera.Position, position) > maximumDistanceSquared ||
                frustum.Contains(new BoundingSphere(position + BoundsOffset, BoundsRadius)) == ContainmentType.Disjoint)
                continue;
            _visible[VisibleCount++] = new InstanceTransform(transform);
        }
        if (VisibleCount > 0)
            _instanceBuffer.SetData(_visible, 0, VisibleCount, SetDataOptions.Discard);
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(effect);
        if (VisibleCount == 0) return;
        device.SetVertexBuffers(new VertexBufferBinding(_mesh.VertexBuffer),
            new VertexBufferBinding(_instanceBuffer, 0, 1));
        device.Indices = _mesh.IndexBuffer;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0,
                _mesh.PrimitiveCount, VisibleCount);
        }
    }

    public void Dispose()
    {
        _instanceBuffer.Dispose();
        if (_ownsMesh) _mesh.Dispose();
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.World.Spatial;

namespace Nova3D.Rendering.Instancing;

/// <summary>CPU culling and distance LOD selection for one instanced object set.</summary>
public sealed class LodInstancedMeshBatch : IDisposable
{
    private readonly Mesh[] _meshes;
    private readonly bool _ownsMeshes;
    private readonly Matrix[] _transforms;
    private readonly bool[]? _enabled;
    private readonly float[] _lodDistanceSquared;
    private readonly InstanceTransform[][] _visible;
    private readonly DynamicVertexBuffer[] _instanceBuffers;
    private readonly int[] _visibleCounts;
    private readonly StaticSpatialGrid _spatialGrid;
    private readonly List<int> _candidates = new();

    public LodInstancedMeshBatch(GraphicsDevice device, IReadOnlyList<Mesh> meshes, Matrix[] transforms,
        IReadOnlyList<float> lodDistances, float cullDistance, float boundsRadius,
        Vector3 boundsOffset = default, bool[]? enabled = null, bool ownsMeshes = true,
        float spatialCellSize = 128f)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentNullException.ThrowIfNull(transforms);
        ArgumentNullException.ThrowIfNull(lodDistances);
        if (meshes.Count == 0) throw new ArgumentException("At least one LOD mesh is required.", nameof(meshes));
        if (transforms.Length == 0) throw new ArgumentException("At least one transform is required.", nameof(transforms));
        if (lodDistances.Count != meshes.Count - 1)
            throw new ArgumentException("LOD distances must contain one threshold between each mesh.", nameof(lodDistances));
        if (enabled is not null && enabled.Length != transforms.Length)
            throw new ArgumentException("Enabled flags must match the transform count.", nameof(enabled));
        if (cullDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(cullDistance));
        if (boundsRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(boundsRadius));

        _meshes = meshes.ToArray();
        _transforms = transforms;
        _enabled = enabled;
        _ownsMeshes = ownsMeshes;
        _lodDistanceSquared = new float[lodDistances.Count];
        var previous = 0f;
        for (var i = 0; i < lodDistances.Count; i++)
        {
            var distance = lodDistances[i];
            if (distance <= previous || distance >= cullDistance)
                throw new ArgumentException("LOD distances must be ascending and below the cull distance.", nameof(lodDistances));
            _lodDistanceSquared[i] = distance * distance;
            previous = distance;
        }

        CullDistance = cullDistance;
        BoundsRadius = boundsRadius;
        BoundsOffset = boundsOffset;
        _visibleCounts = new int[_meshes.Length];
        _spatialGrid = new StaticSpatialGrid(transforms.Select(transform => transform.Translation).ToArray(),
            spatialCellSize);
        _visible = new InstanceTransform[_meshes.Length][];
        _instanceBuffers = new DynamicVertexBuffer[_meshes.Length];
        for (var lod = 0; lod < _meshes.Length; lod++)
        {
            _visible[lod] = new InstanceTransform[transforms.Length];
            _instanceBuffers[lod] = new DynamicVertexBuffer(device, InstanceTransform.VertexDeclaration,
                transforms.Length, BufferUsage.WriteOnly);
        }
    }

    public float CullDistance { get; set; }
    public float BoundsRadius { get; set; }
    public Vector3 BoundsOffset { get; set; }
    public IReadOnlyList<int> VisibleCounts => _visibleCounts;
    public int TotalVisible => _visibleCounts.Sum();
    public int LastCandidateCount { get; private set; }
    public int DrawCalls => _visibleCounts.Count(count => count > 0);
    public long VisibleTriangles => Enumerable.Range(0, _meshes.Length)
        .Sum(lod => (long)_visibleCounts[lod] * _meshes[lod].PrimitiveCount);

    public void Update(Camera3D camera) => Update(camera.View, camera.Projection, camera.Position);

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        Array.Clear(_visibleCounts);
        var frustum = new BoundingFrustum(view * projection);
        var cullDistanceSquared = CullDistance * CullDistance;
        _spatialGrid.Query(cameraPosition, CullDistance, _candidates);
        LastCandidateCount = _candidates.Count;
        foreach (var index in _candidates)
        {
            if (_enabled is not null && !_enabled[index]) continue;
            var transform = _transforms[index];
            var position = transform.Translation;
            var distanceSquared = Vector3.DistanceSquared(cameraPosition, position);
            if (distanceSquared >= cullDistanceSquared ||
                frustum.Contains(new BoundingSphere(position + BoundsOffset, BoundsRadius)) == ContainmentType.Disjoint)
                continue;

            var lod = 0;
            while (lod < _lodDistanceSquared.Length && distanceSquared >= _lodDistanceSquared[lod]) lod++;
            _visible[lod][_visibleCounts[lod]++] = new InstanceTransform(transform);
        }

        for (var lod = 0; lod < _meshes.Length; lod++)
            if (_visibleCounts[lod] > 0)
                _instanceBuffers[lod].SetData(_visible[lod], 0, _visibleCounts[lod], SetDataOptions.Discard);
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        for (var lod = 0; lod < _meshes.Length; lod++)
        {
            if (_visibleCounts[lod] == 0) continue;
            var mesh = _meshes[lod];
            device.SetVertexBuffers(new VertexBufferBinding(mesh.VertexBuffer),
                new VertexBufferBinding(_instanceBuffers[lod], 0, 1));
            device.Indices = mesh.IndexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0,
                    mesh.PrimitiveCount, _visibleCounts[lod]);
            }
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _instanceBuffers) buffer.Dispose();
        if (_ownsMeshes)
            foreach (var mesh in _meshes) mesh.Dispose();
    }
}

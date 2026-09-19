using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Vegetation;

internal sealed class InstancedForest : IDisposable
{
    private const int TreeCount = 10_000;
    private readonly TreeMesh[] _meshes;
    private readonly DynamicVertexBuffer[] _instanceBuffers;
    private readonly Matrix[] _transforms = new Matrix[TreeCount];
    private readonly InstanceVertex[][] _visible = { new InstanceVertex[TreeCount], new InstanceVertex[TreeCount], new InstanceVertex[TreeCount] };
    private readonly float _lod0Distance;
    private readonly float _lod1Distance;
    private readonly float _cullDistance;

    public InstancedForest(
        GraphicsDevice device,
        float areaSize = 340f,
        float lod0Distance = 50f,
        float lod1Distance = 150f,
        float cullDistance = 400f,
        Func<float, float, float>? heightProvider = null)
    {
        _lod0Distance = lod0Distance;
        _lod1Distance = lod1Distance;
        _cullDistance = cullDistance;
        _meshes = new[] { new TreeMesh(device, 10, 3), new TreeMesh(device, 6, 2), new TreeMesh(device, 4, 1) };
        _instanceBuffers = Enumerable.Range(0, 3).Select(_ => new DynamicVertexBuffer(
            device, InstanceVertex.VertexDeclaration, TreeCount, BufferUsage.WriteOnly)).ToArray();
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
        }
    }

    public int[] VisibleCounts { get; } = new int[3];
    public int TotalVisible => VisibleCounts.Sum();
    public int DrawCalls => VisibleCounts.Count(count => count > 0);
    public long VisibleTriangles => Enumerable.Range(0, 3).Sum(lod =>
        (long)VisibleCounts[lod] * _meshes[lod].PrimitiveCount);

    public void Update(Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        Array.Clear(VisibleCounts);
        var frustum = new BoundingFrustum(view * projection);
        foreach (var transform in _transforms)
        {
            var position = transform.Translation;
            var distance = Vector3.Distance(cameraPosition, position);
            if (distance >= _cullDistance || frustum.Contains(new BoundingSphere(position + Vector3.Up * 1.6f, 2.4f)) == ContainmentType.Disjoint)
                continue;
            var lod = distance < _lod0Distance ? 0 : distance < _lod1Distance ? 1 : 2;
            _visible[lod][VisibleCounts[lod]++] = new InstanceVertex(transform);
        }
        for (var lod = 0; lod < 3; lod++)
            if (VisibleCounts[lod] > 0)
                _instanceBuffers[lod].SetData(_visible[lod], 0, VisibleCounts[lod], SetDataOptions.Discard);
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        for (var lod = 0; lod < 3; lod++)
        {
            if (VisibleCounts[lod] == 0) continue;
            var mesh = _meshes[lod];
            device.SetVertexBuffers(
                new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(_instanceBuffers[lod], 0, 1));
            device.Indices = mesh.IndexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawInstancedPrimitives(
                    PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, VisibleCounts[lod]);
            }
        }
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes) mesh.Dispose();
        foreach (var buffer in _instanceBuffers) buffer.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct InstanceVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4));
    public InstanceVertex(Matrix matrix)
    {
        Row0 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        Row1 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        Row2 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34);
        Row3 = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44);
    }
    public readonly Vector4 Row0, Row1, Row2, Row3;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

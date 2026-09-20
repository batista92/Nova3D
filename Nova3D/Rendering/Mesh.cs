using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering;

/// <summary>A GPU mesh with explicit MonoGame vertex and index buffers.</summary>
public sealed class Mesh : IDisposable
{
    public Mesh(VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int primitiveCount, bool ownsBuffers = true)
    {
        VertexBuffer = vertexBuffer ?? throw new ArgumentNullException(nameof(vertexBuffer));
        IndexBuffer = indexBuffer ?? throw new ArgumentNullException(nameof(indexBuffer));
        if (primitiveCount < 0) throw new ArgumentOutOfRangeException(nameof(primitiveCount));
        PrimitiveCount = primitiveCount;
        OwnsBuffers = ownsBuffers;
    }

    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public int PrimitiveCount { get; }
    public bool OwnsBuffers { get; }

    public static Mesh Create<TVertex>(GraphicsDevice device, TVertex[] vertices, ushort[] indices)
        where TVertex : struct, IVertexType
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (vertices.Length == 0) throw new ArgumentException("A mesh needs at least one vertex.", nameof(vertices));
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("Triangle indices must be non-empty and divisible by three.", nameof(indices));

        var vertexBuffer = new VertexBuffer(device, vertices[0].VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);
        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        indexBuffer.SetData(indices);
        return new Mesh(vertexBuffer, indexBuffer, indices.Length / 3);
    }

    public static Mesh Create<TVertex>(GraphicsDevice device, TVertex[] vertices, int[] indices)
        where TVertex : struct, IVertexType
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (vertices.Length == 0) throw new ArgumentException("A mesh needs at least one vertex.", nameof(vertices));
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("Triangle indices must be non-empty and divisible by three.", nameof(indices));
        if (indices.Any(index => index < 0 || index >= vertices.Length))
            throw new ArgumentOutOfRangeException(nameof(indices), "An index is outside the vertex array.");

        var vertexBuffer = new VertexBuffer(device, vertices[0].VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);
        var useSixteenBits = vertices.Length <= ushort.MaxValue && indices.All(index => index <= ushort.MaxValue);
        var indexBuffer = new IndexBuffer(device,
            useSixteenBits ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits,
            indices.Length, BufferUsage.WriteOnly);
        if (useSixteenBits)
            indexBuffer.SetData(indices.Select(index => (ushort)index).ToArray());
        else
            indexBuffer.SetData(indices);
        return new Mesh(vertexBuffer, indexBuffer, indices.Length / 3);
    }

    public void Bind(GraphicsDevice device)
    {
        device.SetVertexBuffer(VertexBuffer);
        device.Indices = IndexBuffer;
    }

    public void Draw(GraphicsDevice device)
    {
        Bind(device);
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
    }

    public void Dispose()
    {
        if (!OwnsBuffers) return;
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}

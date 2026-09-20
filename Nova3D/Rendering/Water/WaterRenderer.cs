using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Water;

/// <summary>Draws a transparent water mesh with explicit forward-render states.</summary>
public sealed class WaterRenderer : IDisposable
{
    private readonly Mesh _mesh;
    private readonly bool _ownsMesh;

    public WaterRenderer(Mesh mesh, WaterMaterial material, bool ownsMesh = true)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        _ownsMesh = ownsMesh;
    }

    public WaterMaterial Material { get; }
    public int PrimitiveCount => _mesh.PrimitiveCount;

    public void Draw(RenderContext context, float time)
    {
        ArgumentNullException.ThrowIfNull(context);
        var device = context.GraphicsDevice;
        var previousBlend = device.BlendState;
        var previousDepth = device.DepthStencilState;
        var previousRasterizer = device.RasterizerState;

        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = RasterizerState.CullClockwise;
        Material.Time = time;
        Material.Apply(context);
        _mesh.Bind(device);
        foreach (var pass in Material.Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _mesh.PrimitiveCount);
            context.Statistics.RecordDraw(_mesh.PrimitiveCount);
        }

        device.BlendState = previousBlend;
        device.DepthStencilState = previousDepth;
        device.RasterizerState = previousRasterizer;
    }

    public void Dispose()
    {
        if (_ownsMesh) _mesh.Dispose();
    }
}

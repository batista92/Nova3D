using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.Production.Debugging;

public sealed class DebugRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly List<VertexPositionColor> _vertices = new();

    public DebugRenderer(GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _effect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false };
    }

    public int LineCount => _vertices.Count / 2;

    public void Line(Vector3 start, Vector3 end, Color color)
    {
        _vertices.Add(new VertexPositionColor(start, color));
        _vertices.Add(new VertexPositionColor(end, color));
    }

    public void Axes(Vector3 origin, float size)
    {
        Line(origin, origin + Vector3.Right * size, Color.Red);
        Line(origin, origin + Vector3.Up * size, Color.Lime);
        Line(origin, origin + Vector3.Backward * size, Color.Blue);
    }

    public void BoundingBox(BoundingBox bounds, Color color)
    {
        var c = bounds.GetCorners();
        AddLoop(c, 0, 1, 2, 3, color);
        AddLoop(c, 4, 5, 6, 7, color);
        Line(c[0], c[4], color); Line(c[1], c[5], color);
        Line(c[2], c[6], color); Line(c[3], c[7], color);
    }

    public void Frustum(BoundingFrustum frustum, Color color)
    {
        var c = frustum.GetCorners();
        AddLoop(c, 0, 1, 2, 3, color);
        AddLoop(c, 4, 5, 6, 7, color);
        Line(c[0], c[4], color); Line(c[1], c[5], color);
        Line(c[2], c[6], color); Line(c[3], c[7], color);
    }

    public void Flush(RenderContext context)
    {
        if (_vertices.Count == 0) return;
        var camera = context.Camera ?? throw new InvalidOperationException("A camera is required for debug rendering.");
        var previousBlend = _device.BlendState;
        var previousDepth = _device.DepthStencilState;
        var previousRasterizer = _device.RasterizerState;
        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.RasterizerState = RasterizerState.CullNone;
        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.LineList, _vertices.ToArray(), 0, LineCount);
            context.Statistics.RecordDraw(LineCount);
        }
        _device.BlendState = previousBlend;
        _device.DepthStencilState = previousDepth;
        _device.RasterizerState = previousRasterizer;
        _vertices.Clear();
    }

    private void AddLoop(Vector3[] corners, int a, int b, int c, int d, Color color)
    {
        Line(corners[a], corners[b], color); Line(corners[b], corners[c], color);
        Line(corners[c], corners[d], color); Line(corners[d], corners[a], color);
    }

    public void Dispose() => _effect.Dispose();
}

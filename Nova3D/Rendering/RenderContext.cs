using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering;

/// <summary>
/// Shared state for one render frame. It deliberately exposes MonoGame's
/// GraphicsDevice instead of wrapping or duplicating its API.
/// </summary>
public sealed class RenderContext
{
    public RenderContext(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public GraphicsDevice GraphicsDevice { get; }
    public Camera3D? Camera { get; private set; }
    public RenderStatistics Statistics { get; } = new();
    public RenderStatistics LastFrameStatistics { get; } = new();
    public ulong FrameIndex { get; private set; }

    public void BeginFrame(Camera3D camera)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        LastFrameStatistics.CopyFrom(Statistics);
        Statistics.Reset();
        FrameIndex++;
    }
}

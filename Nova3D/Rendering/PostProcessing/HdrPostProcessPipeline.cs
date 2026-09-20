using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.PostProcessing;

/// <summary>HDR scene target followed by bloom, FXAA and final composition.</summary>
public sealed class HdrPostProcessPipeline : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Effect _effect;
    private readonly SpriteBatch _spriteBatch;
    private RenderTarget2D? _hdrScene;
    private RenderTarget2D? _bloomA;
    private RenderTarget2D? _bloomB;
    private bool _sceneActive;

    public HdrPostProcessPipeline(GraphicsDevice device, Effect effect)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _spriteBatch = new SpriteBatch(device);
    }

    public float Exposure { get; set; } = 0.95f;
    public float BloomStrength { get; set; } = 0.12f;

    public void BeginScene(Color clearColor)
    {
        if (_sceneActive) throw new InvalidOperationException("The HDR scene has already begun.");
        EnsureTargets();
        _device.SetRenderTarget(_hdrScene);
        _device.Clear(clearColor);
        _sceneActive = true;
    }

    public void EndScene(RenderStatistics? statistics = null)
    {
        if (!_sceneActive) throw new InvalidOperationException("BeginScene must be called before EndScene.");
        var halfRect = new Rectangle(0, 0, _bloomA!.Width, _bloomA.Height);

        _device.SetRenderTarget(_bloomA);
        _device.Clear(Color.Transparent);
        DrawFullscreen(_hdrScene!, halfRect, "ExtractBloom", statistics);

        _device.SetRenderTarget(_bloomB);
        _device.Clear(Color.Transparent);
        _effect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _bloomA.Width, 1f / _bloomA.Height));
        DrawFullscreen(_bloomA, halfRect, "BlurH", statistics);

        _device.SetRenderTarget(_bloomA);
        _device.Clear(Color.Transparent);
        DrawFullscreen(_bloomB!, halfRect, "BlurV", statistics);

        _device.SetRenderTarget(null);
        _effect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _hdrScene!.Width, 1f / _hdrScene.Height));
        _effect.Parameters["BloomTexture"].SetValue(_bloomA);
        _effect.Parameters["Exposure"].SetValue(Exposure);
        _effect.Parameters["BloomStrength"].SetValue(BloomStrength);
        DrawFullscreen(_hdrScene, new Rectangle(0, 0, _hdrScene.Width, _hdrScene.Height),
            "CompositeFinal", statistics);
        _sceneActive = false;
    }

    private void EnsureTargets()
    {
        var width = _device.PresentationParameters.BackBufferWidth;
        var height = _device.PresentationParameters.BackBufferHeight;
        if (_hdrScene?.Width == width && _hdrScene.Height == height) return;
        ReleaseTargets();
        _hdrScene = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.Depth24);
        _bloomA = new RenderTarget2D(_device, Math.Max(1, width / 2), Math.Max(1, height / 2),
            false, SurfaceFormat.Color, DepthFormat.None);
        _bloomB = new RenderTarget2D(_device, _bloomA.Width, _bloomA.Height,
            false, SurfaceFormat.Color, DepthFormat.None);
    }

    private void DrawFullscreen(Texture2D source, Rectangle destination, string technique,
        RenderStatistics? statistics)
    {
        var viewport = _device.Viewport;
        _effect.Parameters["MatrixTransform"].SetValue(
            Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f));
        _effect.CurrentTechnique = _effect.Techniques[technique];
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, _effect);
        _spriteBatch.Draw(source, destination, Color.White);
        _spriteBatch.End();
        statistics?.RecordDraw(2);
    }

    private void ReleaseTargets()
    {
        _hdrScene?.Dispose();
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        _hdrScene = null;
        _bloomA = null;
        _bloomB = null;
    }

    public void Dispose()
    {
        ReleaseTargets();
        _spriteBatch.Dispose();
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Benchmarks.Validation.Pbr;
using Nova3D.Production.Assets.Gltf;
using Nova3D.Rendering;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Models;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.Benchmarks.Validation.VisualCoverage;

internal sealed class GltfVisualScene : IDisposable
{
    private readonly GeneratedGltfFixtures _fixtures = new();
    private readonly GltfModel _staticModel;
    private readonly GltfModel _animatedModel;
    private readonly GltfSkeletonPose _pose;
    private readonly GltfAnimationPlayer _player;
    private readonly GltfModelRenderer _staticRenderer;
    private readonly GltfModelRenderer _animatedRenderer;
    private readonly RenderContext _context;
    private readonly IblEnvironment _environment;
    private readonly Texture2D _whiteShadow;
    private readonly RenderTarget2D _shadowTarget;
    private readonly Effect _pbrEffect;
    private readonly Effect _shadowEffect;
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 0.4f, 7f),
        Direction = Vector3.Normalize(new Vector3(0f, 0f, -1f)),
        NearPlane = 0.1f,
        FarPlane = 50f
    };

    public GltfVisualScene(GraphicsDevice device, Effect pbrEffect, Effect shadowEffect)
    {
        _pbrEffect = pbrEffect;
        _shadowEffect = shadowEffect;
        _context = new RenderContext(device);
        _environment = new IblEnvironment(device);
        _whiteShadow = new Texture2D(device, 1, 1);
        _whiteShadow.SetData(new[] { Color.White });
        _shadowTarget = new RenderTarget2D(device, 512, 512, false,
            SurfaceFormat.Single, DepthFormat.Depth24);
        ConfigurePbr();

        GltfImporter importer = new(device);
        _staticModel = importer.Load(_fixtures.StaticPath);
        _animatedModel = importer.Load(_fixtures.AnimatedPath);
        _pose = new GltfSkeletonPose(_animatedModel);
        _player = new GltfAnimationPlayer(_animatedModel, _pose);
        _player.Play("Swing");
        NovaDirectionalLight light = new(
            Vector3.Normalize(new Vector3(-0.35f, -0.65f, -1f)),
            new Vector3(1f, 0.96f, 0.9f),
            3f);
        ImageBasedLighting ibl = new(
            _environment.EnvironmentMap,
            _environment.IrradianceMap,
            _environment.PrefilteredMap,
            _environment.BrdfLut,
            _environment.PrefilterMipCount);
        _staticRenderer = new GltfModelRenderer(_staticModel, pbrEffect, light, ibl)
        {
            Transform = Matrix.CreateScale(1.25f) * Matrix.CreateTranslation(-2f, 0f, 0f)
        };
        _animatedRenderer = new GltfModelRenderer(_animatedModel, pbrEffect, light, ibl, _pose)
        {
            Transform = Matrix.CreateScale(1.25f) * Matrix.CreateTranslation(2f, 0f, 0f)
        };
    }

    public void Update(GameTime gameTime)
    {
        _camera.SetViewport(_context.GraphicsDevice.Viewport);
        // Capture is keyed to a rendered frame. Fixed-step games may still run
        // more than one update before a draw, so evaluate one absolute pose
        // instead of accumulating scheduler-dependent update counts.
        _player.Play("Swing");
        _player.Update(1f);
        _animatedRenderer.UpdateBounds();
    }

    public void Draw()
    {
        GraphicsDevice device = _context.GraphicsDevice;
        _context.BeginFrame(_camera);
        device.SetRenderTarget(_shadowTarget);
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
        Matrix lightViewProjection = Matrix.CreateLookAt(
                new Vector3(4f, 5f, 6f), Vector3.Zero, Vector3.Up) *
            Matrix.CreateOrthographic(10f, 8f, 0.1f, 20f);
        _staticRenderer.DrawShadows(_context, _shadowEffect, lightViewProjection);
        _animatedRenderer.DrawShadows(_context, _shadowEffect, lightViewProjection);

        device.SetRenderTarget(null);
        device.Clear(new Color(22, 28, 40));
        ConfigurePbr();
        _staticRenderer.Draw(_context);
        _animatedRenderer.Draw(_context);
    }

    private void ConfigurePbr()
    {
        for (int index = 0; index < 4; index++)
        {
            _pbrEffect.Parameters[$"ShadowMap{index}"]?.SetValue(_whiteShadow);
            _pbrEffect.Parameters[$"LightViewProjection{index}"]?.SetValue(Matrix.Identity);
        }
        _pbrEffect.Parameters["ShadowMapTexelSize"]?.SetValue(Vector2.One);
        _pbrEffect.Parameters["CascadeSplits"]?.SetValue(new Vector4(12f, 24f, 36f, 50f));
        _pbrEffect.Parameters["CascadeBlendStarts"]?.SetValue(new Vector4(11f, 23f, 35f, 49f));
    }

    public void Dispose()
    {
        _animatedRenderer.Dispose();
        _staticRenderer.Dispose();
        _animatedModel.Dispose();
        _staticModel.Dispose();
        _shadowTarget.Dispose();
        _whiteShadow.Dispose();
        _environment.Dispose();
        _fixtures.Dispose();
    }
}

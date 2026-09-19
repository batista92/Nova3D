using CityBuilder.Tests.Pbr;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Vegetation;

internal sealed class VegetationScene : IDisposable
{
    private readonly Effect _effect;
    private readonly Effect _skyboxEffect;
    private readonly BasicEffect _groundEffect;
    private readonly PrimitiveMesh _ground;
    private readonly PrimitiveMesh _skybox;
    private readonly IblEnvironment _environment;
    private readonly InstancedForest _forest;
    private readonly OrbitCamera _camera = new(new Vector3(0f, 1.5f, 0f), 115f, 12f, 360f);
    private Matrix _projection;
    private double _smoothedFrameMilliseconds = 16.67;

    public VegetationScene(GraphicsDevice device, Effect effect, Effect skyboxEffect)
    {
        _effect = effect;
        _skyboxEffect = skyboxEffect;
        _groundEffect = new BasicEffect(device)
        {
            LightingEnabled = true,
            TextureEnabled = false,
            DiffuseColor = new Vector3(0.11f, 0.18f, 0.08f),
            AmbientLightColor = new Vector3(0.28f),
            PreferPerPixelLighting = true
        };
        _groundEffect.DirectionalLight0.Enabled = true;
        _groundEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f));
        _groundEffect.DirectionalLight0.DiffuseColor = new Vector3(0.85f, 0.8f, 0.7f);
        _ground = PrimitiveMesh.CreatePlane(device, 220f);
        _skybox = PrimitiveMesh.CreateCube(device);
        _environment = new IblEnvironment(device);
        _forest = new InstancedForest(device);
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        _camera.Update(window);
        _projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, _effect.GraphicsDevice.Viewport.AspectRatio, 0.5f, 600f);
        _forest.Update(_camera.View, _projection, _camera.Position);
        var frameMilliseconds = gameTime.ElapsedGameTime.TotalMilliseconds;
        if (frameMilliseconds > 0)
            _smoothedFrameMilliseconds = _smoothedFrameMilliseconds * 0.95 + frameMilliseconds * 0.05;
        var fps = 1000.0 / _smoothedFrameMilliseconds;
        window.Title = $"CityBuilder - Teste 05: GPU Instancing | " +
                       $"FPS {fps:F0} | frame {_smoothedFrameMilliseconds:F2} ms | " +
                       $"LOD0 {_forest.VisibleCounts[0]} | LOD1 {_forest.VisibleCounts[1]} | " +
                       $"LOD2 {_forest.VisibleCounts[2]} | visiveis {_forest.TotalVisible}/10000 | draws {_forest.DrawCalls}";
    }

    public void Draw(float aspectRatio)
    {
        DrawSkybox();
        var device = _effect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullClockwise;

        _groundEffect.World = Matrix.Identity;
        _groundEffect.View = _camera.View;
        _groundEffect.Projection = _projection;
        foreach (var pass in _groundEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _ground.Draw(device);
        }

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        _effect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        _effect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _effect.Parameters["LightDirection"].SetValue(Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f)));
        _forest.Draw(device, _effect);
    }

    private void DrawSkybox()
    {
        var device = _skyboxEffect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullNone;
        _skyboxEffect.Parameters["World"].SetValue(Matrix.CreateScale(250f));
        _skyboxEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(
            Vector3.Zero, Vector3.Normalize(_camera.Target - _camera.Position), Vector3.Up));
        _skyboxEffect.Parameters["Projection"].SetValue(_projection);
        _skyboxEffect.Parameters["EnvironmentMap"].SetValue(_environment.EnvironmentMap);
        _skyboxEffect.Parameters["Exposure"].SetValue(0.75f);
        foreach (var pass in _skyboxEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _skybox.Draw(device);
        }
    }

    public void Dispose()
    {
        _forest.Dispose();
        _ground.Dispose();
        _skybox.Dispose();
        _environment.Dispose();
        _groundEffect.Dispose();
        _effect.Dispose();
        _skyboxEffect.Dispose();
    }
}

using CityBuilder.Tests.Pbr;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CityBuilder.Tests.Terrain;

internal sealed class TerrainScene : IDisposable
{
    private readonly Effect _effect;
    private readonly Effect _skyboxEffect;
    private readonly Effect _shadowEffect;
    private readonly RuntimeTerrain _terrain;
    private readonly PrimitiveMesh _skybox;
    private readonly TerrainMaterialTextures _textures;
    private readonly IblEnvironment _environment;
    private readonly OrbitCamera _camera = new(new Vector3(0f, 1.2f, 0f), 20f);
    private KeyboardState _previousKeyboard;
    private int _debugView;
    private Vector3 _brushPosition;
    private bool _brushVisible;
    private float _flattenHeight;
    private MouseState _previousMouse;
    private TerrainBrushMode _brushMode = TerrainBrushMode.Raise;
    private readonly RenderTarget2D[] _shadowMaps = new RenderTarget2D[4];
    private readonly RasterizerState _shadowRasterizer;
    private readonly Matrix[] _lightViewProjections = new Matrix[4];
    private readonly float[] _cascadeSplits = new float[4];
    private readonly float[] _cascadeBlendStarts = new float[4];
    private bool _showCascades;
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-0.4f, -1f, -0.6f));

    public TerrainScene(GraphicsDevice device, Effect effect, Effect skyboxEffect, Effect shadowEffect)
    {
        _effect = effect;
        _skyboxEffect = skyboxEffect;
        _shadowEffect = shadowEffect;
        _terrain = new RuntimeTerrain(device);
        _skybox = PrimitiveMesh.CreateCube(device);
        _textures = new TerrainMaterialTextures(device);
        _environment = new IblEnvironment(device);
        for (var i = 0; i < 4; i++)
            _shadowMaps[i] = new RenderTarget2D(device, 1024, 1024, false,
                SurfaceFormat.Single, DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
        _shadowRasterizer = new RasterizerState
        {
            CullMode = CullMode.CullClockwiseFace,
            DepthBias = 0.002f,
            SlopeScaleDepthBias = 3f
        };
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        _camera.Update(window);
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState(window);
        for (var i = 0; i < 4; i++)
        {
            var key = (Keys)((int)Keys.D1 + i);
            if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
                _debugView = i;
        }
        if (keyboard.IsKeyDown(Keys.C) && !_previousKeyboard.IsKeyDown(Keys.C))
            _showCascades = !_showCascades;
        _brushMode = keyboard.IsKeyDown(Keys.F) ? TerrainBrushMode.Flatten :
                     keyboard.IsKeyDown(Keys.S) ? TerrainBrushMode.Smooth :
                     keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                         ? TerrainBrushMode.Lower : TerrainBrushMode.Raise;

        var aspectRatio = _effect.GraphicsDevice.Viewport.AspectRatio;
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspectRatio, 0.1f, 100f);
        var near = _effect.GraphicsDevice.Viewport.Unproject(
            new Vector3(mouse.X, mouse.Y, 0f), projection, _camera.View, Matrix.Identity);
        var far = _effect.GraphicsDevice.Viewport.Unproject(
            new Vector3(mouse.X, mouse.Y, 1f), projection, _camera.View, Matrix.Identity);
        _brushVisible = _terrain.Raycast(new Ray(near, Vector3.Normalize(far - near)), out _brushPosition);
        if (_brushVisible && mouse.RightButton == ButtonState.Pressed)
        {
            if (_previousMouse.RightButton == ButtonState.Released)
                _flattenHeight = _brushPosition.Y;
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var amount = _brushMode is TerrainBrushMode.Raise or TerrainBrushMode.Lower ? 2.2f * dt : 5f * dt;
            _terrain.ApplyBrush(_brushPosition, 1.6f, amount, _brushMode, _flattenHeight);
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        var tool = _brushMode.ToString().ToUpperInvariant();
        window.Title = _debugView switch
        {
            1 => "CityBuilder - TERRAIN DEBUG: layer weights",
            2 => "CityBuilder - TERRAIN DEBUG: mapped normals",
            3 => "CityBuilder - TERRAIN DEBUG: shadow factor",
            _ => $"CityBuilder - Teste 04: terrain runtime | {tool} | chunks atualizados: {_terrain.LastUpdatedChunkCount}"
        };
    }

    public void Draw(float aspectRatio)
    {
        DrawShadowMaps(aspectRatio);
        DrawSkybox(aspectRatio);
        var device = _effect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullClockwise;

        _effect.Parameters["World"].SetValue(Matrix.Identity);
        _effect.Parameters["WorldInverseTranspose"].SetValue(Matrix.Identity);
        _effect.Parameters["View"].SetValue(_camera.View);
        _effect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, aspectRatio, 0.1f, 100f));
        _effect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _effect.Parameters["LightDirection"].SetValue(LightDirection);
        _effect.Parameters["LightColor"].SetValue(new Vector3(3f, 2.85f, 2.65f));
        _effect.Parameters["Exposure"].SetValue(0.9f);
        _effect.Parameters["TextureScale"].SetValue(0.32f);
        _effect.Parameters["TriplanarSharpness"].SetValue(5f);
        _effect.Parameters["DebugView"].SetValue((float)_debugView);
        _effect.Parameters["BrushPosition"].SetValue(_brushPosition);
        _effect.Parameters["BrushRadius"].SetValue(1.6f);
        _effect.Parameters["BrushActive"].SetValue(_brushVisible ? 1f : 0f);
        var names = new[] { "Grass", "Dirt", "Rock", "Sand" };
        for (var i = 0; i < names.Length; i++)
        {
            _effect.Parameters[$"{names[i]}AlbedoHeight"].SetValue(_textures.AlbedoHeightMaps[i]);
            _effect.Parameters[$"{names[i]}NormalAoRoughness"].SetValue(_textures.NormalAoRoughnessMaps[i]);
        }
        _effect.Parameters["IrradianceMap"].SetValue(_environment.IrradianceMap);
        _effect.Parameters["PrefilteredMap"].SetValue(_environment.PrefilteredMap);
        _effect.Parameters["BrdfLut"].SetValue(_environment.BrdfLut);
        _effect.Parameters["MaxReflectionLod"].SetValue(_environment.PrefilterMipCount - 1f);
        _effect.Parameters["LightViewProjection0"].SetValue(_lightViewProjections[0]);
        _effect.Parameters["LightViewProjection1"].SetValue(_lightViewProjections[1]);
        _effect.Parameters["LightViewProjection2"].SetValue(_lightViewProjections[2]);
        _effect.Parameters["LightViewProjection3"].SetValue(_lightViewProjections[3]);
        _effect.Parameters["CascadeSplits"].SetValue(new Vector4(_cascadeSplits[0], _cascadeSplits[1], _cascadeSplits[2], _cascadeSplits[3]));
        _effect.Parameters["CascadeBlendStarts"].SetValue(new Vector4(_cascadeBlendStarts[0], _cascadeBlendStarts[1], _cascadeBlendStarts[2], _cascadeBlendStarts[3]));
        for (var i = 0; i < 4; i++)
            _effect.Parameters[$"ShadowMap{i}"].SetValue(_shadowMaps[i]);
        _effect.Parameters["ShadowMapTexelSize"].SetValue(new Vector2(1f / 1024f));
        _effect.Parameters["ShowCascades"].SetValue(_showCascades ? 1f : 0f);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _terrain.Draw();
        }
    }

    private void DrawShadowMaps(float aspectRatio)
    {
        BuildCascadeMatrices(aspectRatio);
        var device = _shadowEffect.GraphicsDevice;
        for (var cascade = 0; cascade < 4; cascade++)
        {
            device.SetRenderTarget(_shadowMaps[cascade]);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            // Terrain e uma superficie aberta: precisa gravar as faces voltadas
            // para a luz, ao contrario dos volumes fechados usados no teste PBR.
            device.RasterizerState = _shadowRasterizer;
            _shadowEffect.Parameters["World"].SetValue(Matrix.Identity);
            _shadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);
            foreach (var pass in _shadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _terrain.Draw();
            }
        }
        device.SetRenderTarget(null);
    }

    private void BuildCascadeMatrices(float aspectRatio)
    {
        const float near = 0.1f, far = 40f, lambda = 0.65f;
        for (var i = 1; i <= 4; i++)
        {
            var ratio = i / 4f;
            _cascadeSplits[i - 1] = MathHelper.Lerp(near + (far - near) * ratio,
                near * MathF.Pow(far / near, ratio), lambda);
        }
        var forward = Vector3.Normalize(_camera.Target - _camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var previous = near;
        for (var cascade = 0; cascade < 4; cascade++)
        {
            var split = _cascadeSplits[cascade];
            _cascadeBlendStarts[cascade] = MathHelper.Lerp(previous, split, 0.9f);
            var corners = FrustumCorners(_camera.Position, forward, right, up, previous, split, aspectRatio);
            var center = Vector3.Zero;
            foreach (var corner in corners) center += corner;
            center /= 8f;
            var radius = 0f;
            foreach (var corner in corners) radius = MathF.Max(radius, Vector3.Distance(center, corner));
            radius = MathF.Ceiling(radius * 16f) / 16f;
            var lightRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, LightDirection));
            var lightUp = Vector3.Normalize(Vector3.Cross(LightDirection, lightRight));
            var texel = radius * 2f / 1024f;
            var snappedRight = MathF.Round(Vector3.Dot(center, lightRight) / texel) * texel;
            var snappedUp = MathF.Round(Vector3.Dot(center, lightUp) / texel) * texel;
            center += lightRight * (snappedRight - Vector3.Dot(center, lightRight));
            center += lightUp * (snappedUp - Vector3.Dot(center, lightUp));
            var lightView = Matrix.CreateLookAt(center - LightDirection * (radius + 12f), center, lightUp);
            var lightProjection = Matrix.CreateOrthographic(radius * 2f, radius * 2f, 0.1f, radius * 2f + 24f);
            _lightViewProjections[cascade] = lightView * lightProjection;
            previous = split;
        }
    }

    private static Vector3[] FrustumCorners(Vector3 position, Vector3 forward, Vector3 right, Vector3 up,
        float near, float far, float aspectRatio)
    {
        var tangent = MathF.Tan(MathHelper.PiOver4 * 0.5f);
        var nh = tangent * near; var nw = nh * aspectRatio;
        var fh = tangent * far; var fw = fh * aspectRatio;
        var nc = position + forward * near; var fc = position + forward * far;
        return new[]
        {
            nc-right*nw-up*nh, nc+right*nw-up*nh, nc+right*nw+up*nh, nc-right*nw+up*nh,
            fc-right*fw-up*fh, fc+right*fw-up*fh, fc+right*fw+up*fh, fc-right*fw+up*fh
        };
    }

    private void DrawSkybox(float aspectRatio)
    {
        var device = _skyboxEffect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullNone;
        _skyboxEffect.Parameters["World"].SetValue(Matrix.CreateScale(50f));
        _skyboxEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(
            Vector3.Zero, Vector3.Normalize(_camera.Target - _camera.Position), Vector3.Up));
        _skyboxEffect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, aspectRatio, 0.1f, 100f));
        _skyboxEffect.Parameters["EnvironmentMap"].SetValue(_environment.EnvironmentMap);
        _skyboxEffect.Parameters["Exposure"].SetValue(0.85f);
        foreach (var pass in _skyboxEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _skybox.Draw(device);
        }
    }

    public void Dispose()
    {
        _terrain.Dispose();
        _skybox.Dispose();
        _textures.Dispose();
        _environment.Dispose();
        foreach (var map in _shadowMaps) map.Dispose();
        _shadowRasterizer.Dispose();
        _effect.Dispose();
        _skyboxEffect.Dispose();
        _shadowEffect.Dispose();
    }
}

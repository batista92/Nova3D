using CityBuilder.Tests.Pbr;
using CityBuilder.Tests.Vegetation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class LargeWorldScene : IDisposable
{
    private readonly Effect _vegetationEffect;
    private readonly Effect _skyboxEffect;
    private readonly Effect _terrainEffect;
    private readonly Effect _shadowEffect;
    private readonly Effect _instancedShadowEffect;
    private readonly Effect _postProcessEffect;
    private readonly SpriteBatch _spriteBatch;
    private readonly RenderTarget2D[] _shadowMaps = new RenderTarget2D[4];
    private readonly RasterizerState _shadowRasterizer;
    private readonly PrimitiveMesh _skybox;
    private readonly IblEnvironment _environment;
    private readonly LargeWorldTerrain _terrain;
    private readonly InstancedForest _forest;
    private readonly LargeWorldCamera _camera = new();
    private Matrix _projection;
    private readonly Matrix[] _lightViewProjections = new Matrix[4];
    private readonly float[] _cascadeSplits = new float[4];
    private readonly float[] _cascadeBlendStarts = new float[4];
    private RenderTarget2D? _hdrScene;
    private RenderTarget2D? _bloomA;
    private RenderTarget2D? _bloomB;
    private double _smoothedFrameMilliseconds = 16.67;
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-0.45f, -1f, -0.55f));

    public LargeWorldScene(GraphicsDevice device, Effect terrainEffect, Effect vegetationEffect, Effect skyboxEffect,
        Effect shadowEffect, Effect instancedShadowEffect, Effect postProcessEffect)
    {
        _terrainEffect = terrainEffect;
        _vegetationEffect = vegetationEffect;
        _skyboxEffect = skyboxEffect;
        _shadowEffect = shadowEffect;
        _instancedShadowEffect = instancedShadowEffect;
        _postProcessEffect = postProcessEffect;
        _spriteBatch = new SpriteBatch(device);
        for (var i = 0; i < 4; i++)
            _shadowMaps[i] = new RenderTarget2D(device, 2048, 2048, false, SurfaceFormat.Single,
                DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
        _shadowRasterizer = new RasterizerState
        {
            CullMode = CullMode.CullClockwiseFace,
            DepthBias = 0.003f,
            SlopeScaleDepthBias = 4f
        };
        _skybox = PrimitiveMesh.CreateCube(device);
        _environment = new IblEnvironment(device);
        _terrain = new LargeWorldTerrain(device);
        _forest = new InstancedForest(device, 1900f, 140f, 520f, 1800f, LargeWorldTerrain.SampleHeight);
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        _camera.Update(gameTime, window);
        _projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, _vegetationEffect.GraphicsDevice.Viewport.AspectRatio, 1f, 3200f);
        _terrain.Update(_camera.View, _projection, _camera.Position);
        _forest.Update(_camera.View, _projection, _camera.Position);

        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        if (elapsed > 0) _smoothedFrameMilliseconds = _smoothedFrameMilliseconds * 0.95 + elapsed * 0.05;
        var draws = (_terrain.DrawCalls + _forest.DrawCalls) * 5 + 5;
        var triangles = _terrain.VisibleTriangles + _forest.VisibleTriangles;
        window.Title = $"CityBuilder - Teste 07: HDR + Bloom + FXAA | FPS {1000.0 / _smoothedFrameMilliseconds:F0} | {_smoothedFrameMilliseconds:F2} ms | " +
                       $"chunks {_terrain.VisibleCount}/{LargeWorldTerrain.TotalChunks} [{_terrain.VisibleLods[0]}/{_terrain.VisibleLods[1]}/{_terrain.VisibleLods[2]}] | " +
                       $"arvores {_forest.TotalVisible}/10000 [{_forest.VisibleCounts[0]}/{_forest.VisibleCounts[1]}/{_forest.VisibleCounts[2]}] | " +
                       $"draws {draws} | tris {triangles / 1000f:F0}k";
    }

    public void Draw(float aspectRatio)
    {
        DrawShadowMap();
        EnsurePostProcessTargets();
        var device = _vegetationEffect.GraphicsDevice;
        device.SetRenderTarget(_hdrScene);
        device.Clear(new Color(18, 22, 30));
        DrawSkybox();
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullClockwise;

        _terrainEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        SetShadowParameters(_terrainEffect);
        _terrainEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _terrainEffect.Parameters["View"].SetValue(_camera.View);
        _terrain.Draw(device, _terrainEffect);

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        _vegetationEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        _vegetationEffect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _vegetationEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _vegetationEffect.Parameters["View"].SetValue(_camera.View);
        SetShadowParameters(_vegetationEffect);
        _forest.Draw(device, _vegetationEffect);
        DrawPostProcess();
    }

    private void EnsurePostProcessTargets()
    {
        var device = _vegetationEffect.GraphicsDevice;
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        if (_hdrScene?.Width == width && _hdrScene.Height == height) return;
        _hdrScene?.Dispose(); _bloomA?.Dispose(); _bloomB?.Dispose();
        _hdrScene = new RenderTarget2D(device, width, height, false, SurfaceFormat.HdrBlendable, DepthFormat.Depth24);
        _bloomA = new RenderTarget2D(device, Math.Max(1, width / 2), Math.Max(1, height / 2), false, SurfaceFormat.Color, DepthFormat.None);
        _bloomB = new RenderTarget2D(device, _bloomA.Width, _bloomA.Height, false, SurfaceFormat.Color, DepthFormat.None);
    }

    private void DrawPostProcess()
    {
        var device = _vegetationEffect.GraphicsDevice;
        var halfRect = new Rectangle(0, 0, _bloomA!.Width, _bloomA.Height);
        device.SetRenderTarget(_bloomA);
        device.Clear(Color.Transparent);
        DrawFullscreen(_hdrScene!, halfRect, "ExtractBloom");

        device.SetRenderTarget(_bloomB);
        device.Clear(Color.Transparent);
        _postProcessEffect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _bloomA.Width, 1f / _bloomA.Height));
        DrawFullscreen(_bloomA, halfRect, "BlurH");

        device.SetRenderTarget(_bloomA);
        device.Clear(Color.Transparent);
        DrawFullscreen(_bloomB!, halfRect, "BlurV");

        device.SetRenderTarget(null);
        _postProcessEffect.Parameters["SourceTexelSize"].SetValue(new Vector2(1f / _hdrScene!.Width, 1f / _hdrScene.Height));
        _postProcessEffect.Parameters["BloomTexture"].SetValue(_bloomA);
        _postProcessEffect.Parameters["Exposure"].SetValue(0.95f);
        _postProcessEffect.Parameters["BloomStrength"].SetValue(0.12f);
        DrawFullscreen(_hdrScene, new Rectangle(0, 0, _hdrScene.Width, _hdrScene.Height), "CompositeFinal");
    }

    private void DrawFullscreen(Texture2D source, Rectangle destination, string technique)
    {
        var viewport = _vegetationEffect.GraphicsDevice.Viewport;
        _postProcessEffect.Parameters["MatrixTransform"].SetValue(
            Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f));
        _postProcessEffect.CurrentTechnique = _postProcessEffect.Techniques[technique];
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, _postProcessEffect);
        _spriteBatch.Draw(source, destination, Color.White);
        _spriteBatch.End();
    }

    private void DrawShadowMap()
    {
        var device = _terrainEffect.GraphicsDevice;
        BuildCascadeMatrices(device.Viewport.AspectRatio);
        for (var cascade = 0; cascade < 4; cascade++)
        {
            device.SetRenderTarget(_shadowMaps[cascade]);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            device.RasterizerState = _shadowRasterizer;
            _shadowEffect.Parameters["World"].SetValue(Matrix.Identity);
            _shadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);
            _terrain.Draw(device, _shadowEffect);
            device.RasterizerState = RasterizerState.CullNone;
            _instancedShadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);
            _forest.Draw(device, _instancedShadowEffect);
        }
        device.SetRenderTarget(null);
    }

    private void BuildCascadeMatrices(float aspectRatio)
    {
        const float near = 1f, far = 1200f, lambda = 0.72f;
        for (var i = 1; i <= 4; i++)
        {
            var ratio = i / 4f;
            _cascadeSplits[i - 1] = MathHelper.Lerp(near + (far - near) * ratio,
                near * MathF.Pow(far / near, ratio), lambda);
        }
        var forward = _camera.Direction;
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var previous = near;
        for (var cascade = 0; cascade < 4; cascade++)
        {
            var split = _cascadeSplits[cascade];
            _cascadeBlendStarts[cascade] = MathHelper.Lerp(previous, split, 0.88f);
            var corners = FrustumCorners(_camera.Position, forward, right, up, previous, split, aspectRatio);
            var center = Vector3.Zero;
            foreach (var corner in corners) center += corner;
            center /= 8f;
            var radius = 0f;
            foreach (var corner in corners) radius = MathF.Max(radius, Vector3.Distance(center, corner));
            radius = MathF.Ceiling(radius * 8f) / 8f;
            var lightRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, LightDirection));
            var lightUp = Vector3.Normalize(Vector3.Cross(LightDirection, lightRight));
            var texel = radius * 2f / _shadowMaps[cascade].Width;
            center += lightRight * (MathF.Round(Vector3.Dot(center, lightRight) / texel) * texel - Vector3.Dot(center, lightRight));
            center += lightUp * (MathF.Round(Vector3.Dot(center, lightUp) / texel) * texel - Vector3.Dot(center, lightUp));
            var lightView = Matrix.CreateLookAt(center - LightDirection * (radius + 180f), center, lightUp);
            var lightProjection = Matrix.CreateOrthographic(radius * 2f, radius * 2f, 1f, radius * 2f + 360f);
            _lightViewProjections[cascade] = lightView * lightProjection;
            previous = split;
        }
    }

    private static Vector3[] FrustumCorners(Vector3 position, Vector3 forward, Vector3 right, Vector3 up,
        float near, float far, float aspect)
    {
        var tangent = MathF.Tan(MathHelper.PiOver4 * 0.5f);
        var nh = tangent * near; var nw = nh * aspect;
        var fh = tangent * far; var fw = fh * aspect;
        var nc = position + forward * near; var fc = position + forward * far;
        return new[] { nc-right*nw-up*nh, nc+right*nw-up*nh, nc+right*nw+up*nh, nc-right*nw+up*nh,
            fc-right*fw-up*fh, fc+right*fw-up*fh, fc+right*fw+up*fh, fc-right*fw+up*fh };
    }

    private void SetShadowParameters(Effect effect)
    {
        for (var i = 0; i < 4; i++)
        {
            effect.Parameters[$"LightViewProjection{i}"].SetValue(_lightViewProjections[i]);
            effect.Parameters[$"ShadowMap{i}"].SetValue(_shadowMaps[i]);
        }
        effect.Parameters["CascadeSplits"].SetValue(new Vector4(_cascadeSplits[0], _cascadeSplits[1], _cascadeSplits[2], _cascadeSplits[3]));
        effect.Parameters["CascadeBlendStarts"].SetValue(new Vector4(_cascadeBlendStarts[0], _cascadeBlendStarts[1], _cascadeBlendStarts[2], _cascadeBlendStarts[3]));
        effect.Parameters["ShadowMapTexelSize"].SetValue(new Vector2(1f / _shadowMaps[0].Width));
    }

    private void DrawSkybox()
    {
        var device = _skyboxEffect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullNone;
        _skyboxEffect.Parameters["World"].SetValue(Matrix.CreateScale(1500f) * Matrix.CreateTranslation(_camera.Position));
        _skyboxEffect.Parameters["View"].SetValue(_camera.View);
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
        _terrain.Dispose();
        _skybox.Dispose();
        _environment.Dispose();
        _terrainEffect.Dispose();
        _shadowEffect.Dispose();
        _instancedShadowEffect.Dispose();
        _postProcessEffect.Dispose();
        _spriteBatch.Dispose();
        _hdrScene?.Dispose();
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        foreach (var shadowMap in _shadowMaps) shadowMap.Dispose();
        _shadowRasterizer.Dispose();
        _vegetationEffect.Dispose();
        _skyboxEffect.Dispose();
    }
}

using CityBuilder.Tests.Pbr;
using CityBuilder.Tests.Terrain;
using CityBuilder.Tests.Vegetation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class LargeWorldScene : IDisposable
{
    private readonly Effect _vegetationEffect;
    private readonly Effect _skyboxEffect;
    private readonly Effect _terrainEffect;
    private readonly Effect _terrainMaterialEffect;
    private readonly Effect _waterEffect;
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
    private readonly BenchmarkPopulation _population;
    private readonly WaterSurface _water;
    private readonly RoadNetwork _roads;
    private readonly TerrainMaterialTextures _terrainTextures;
    private readonly LargeWorldCamera _camera = new();
    private Matrix _projection;
    private readonly Matrix[] _lightViewProjections = new Matrix[4];
    private readonly float[] _cascadeSplits = new float[4];
    private readonly float[] _cascadeBlendStarts = new float[4];
    private RenderTarget2D? _hdrScene;
    private RenderTarget2D? _bloomA;
    private RenderTarget2D? _bloomB;
    private double _smoothedFrameMilliseconds = 16.67;
    private int _shadowDebugMode;
    private bool _shadowDebugKeyWasDown;
    private float _shadowCpuMaxXY;
    private float _shadowCpuMinZ;
    private float _shadowCpuMaxZ;
    private float _time;
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-0.45f, -1f, -0.55f));

    public LargeWorldScene(GraphicsDevice device, Effect terrainEffect, Effect terrainMaterialEffect, Effect waterEffect, Effect vegetationEffect, Effect skyboxEffect,
        Effect shadowEffect, Effect instancedShadowEffect, Effect postProcessEffect)
    {
        _terrainEffect = terrainEffect;
        _terrainMaterialEffect = terrainMaterialEffect;
        _waterEffect = waterEffect;
        _vegetationEffect = vegetationEffect;
        _skyboxEffect = skyboxEffect;
        _shadowEffect = shadowEffect;
        _instancedShadowEffect = instancedShadowEffect;
        _postProcessEffect = postProcessEffect;
        _spriteBatch = new SpriteBatch(device);
        for (var i = 0; i < 4; i++)
            _shadowMaps[i] = new RenderTarget2D(device, 4096, 4096, false, SurfaceFormat.Single,
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
        _forest = new InstancedForest(device, 1900f, 140f, 520f, 1800f, LargeWorldTerrain.SampleHeight,
            static (x, z) => !BenchmarkPopulation.InsideLake(x, z) &&
                (!(MathF.Abs(x) < 780f && z > -570f && z < 750f) ||
                 (MathF.Abs(x) < 115f && MathF.Abs(z) < 90f)));
        _population = new BenchmarkPopulation(device);
        _water = new WaterSurface(device);
        _roads = new RoadNetwork(device);
        _terrainTextures = new TerrainMaterialTextures(device);
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        var debugKeyDown = Keyboard.GetState().IsKeyDown(Keys.F1);
        if (debugKeyDown && !_shadowDebugKeyWasDown)
            _shadowDebugMode = (_shadowDebugMode + 1) % 5;
        _shadowDebugKeyWasDown = debugKeyDown;
        _camera.Update(gameTime, window);
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, _vegetationEffect.GraphicsDevice.Viewport.AspectRatio, 1f, 3200f);
        _terrain.Update(_camera.View, _projection, _camera.Position);
        _forest.Update(_camera.View, _projection, _camera.Position);
        _population.Update(_camera.View, _projection, _camera.Position);

        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        if (elapsed > 0) _smoothedFrameMilliseconds = _smoothedFrameMilliseconds * 0.95 + elapsed * 0.05;
        var draws = _terrain.DrawCalls + 4 + (_forest.DrawCalls + _population.DrawCalls) * 5 + 6;
        var triangles = _terrain.VisibleTriangles + _forest.VisibleTriangles + _population.VisibleTriangles + _water.PrimitiveCount + _roads.PrimitiveCount;
        window.Title = $"CityBuilder - Teste 08: City Benchmark | FPS {1000.0 / _smoothedFrameMilliseconds:F0} | {_smoothedFrameMilliseconds:F2} ms | " +
                       $"chunks {_terrain.VisibleCount}/{LargeWorldTerrain.TotalChunks} [{_terrain.VisibleLods[0]}/{_terrain.VisibleLods[1]}/{_terrain.VisibleLods[2]}] | " +
                       $"arvores {_forest.TotalVisible}/10000 [{_forest.VisibleCounts[0]}/{_forest.VisibleCounts[1]}/{_forest.VisibleCounts[2]}] | " +
                       $"predios {_population.VisibleBuildings}/1000 | veiculos {_population.VisibleVehicles}/500 | " +
                       $"draws {draws} | tris {triangles / 1000f:F0}k | shadow debug {_shadowDebugMode} | " +
                       $"CPU xy {_shadowCpuMaxXY:F2} z {_shadowCpuMinZ:F2}..{_shadowCpuMaxZ:F2}";
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

        _terrainMaterialEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        _terrainMaterialEffect.Parameters["View"].SetValue(_camera.View);
        _terrainMaterialEffect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _terrainMaterialEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _terrainMaterialEffect.Parameters["TextureScale"].SetValue(0.055f);
        _terrainMaterialEffect.Parameters["TriplanarSharpness"].SetValue(5f);
        SetTerrainTextures();
        SetShadowParameters(_terrainMaterialEffect);
        _terrain.Draw(device, _terrainMaterialEffect);

        _terrainEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        SetShadowParameters(_terrainEffect);
        _terrainEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _terrainEffect.Parameters["View"].SetValue(_camera.View);
        _roads.Draw(device, _terrainEffect);

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        _vegetationEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        _vegetationEffect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _vegetationEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _vegetationEffect.Parameters["View"].SetValue(_camera.View);
        SetShadowParameters(_vegetationEffect);
        _vegetationEffect.Parameters["MaterialMode"].SetValue(0f);
        _forest.Draw(device, _vegetationEffect);
        _vegetationEffect.Parameters["MaterialMode"].SetValue(1f);
        _population.Draw(device, _vegetationEffect);

        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = RasterizerState.CullClockwise;
        _waterEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
        _waterEffect.Parameters["CameraPosition"].SetValue(_camera.Position);
        _waterEffect.Parameters["LightDirection"].SetValue(LightDirection);
        _waterEffect.Parameters["Time"].SetValue(_time);
        _water.Draw(device, _waterEffect);
        device.BlendState = BlendState.Opaque;
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
        BuildCascadeMatrices();
        for (var cascade = 0; cascade < 4; cascade++)
        {
            device.SetRenderTarget(_shadowMaps[cascade]);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            device.RasterizerState = _shadowRasterizer;
            _shadowEffect.Parameters["World"].SetValue(Matrix.Identity);
            _shadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);
            _terrain.DrawShadow(device, _shadowEffect);
            device.RasterizerState = RasterizerState.CullNone;
            _instancedShadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);
            _forest.Draw(device, _instancedShadowEffect);
            _population.Draw(device, _instancedShadowEffect);
        }
        device.SetRenderTarget(null);
    }

    private void BuildCascadeMatrices()
    {
        // Shadow coverage must match the complete camera range. Resolution is
        // provided by 4096 maps rather than silently dropping distant shadows.
        const float near = 1f, far = 3200f, lambda = 0.76f;
        for (var i = 1; i <= 4; i++)
        {
            var ratio = i / 4f;
            _cascadeSplits[i - 1] = MathHelper.Lerp(near + (far - near) * ratio,
                near * MathF.Pow(far / near, ratio), lambda);
        }
        var forward = _camera.Direction;
        var cameraCorners = new BoundingFrustum(_camera.View * _projection).GetCorners();
        _shadowCpuMaxXY = 0f;
        _shadowCpuMinZ = float.MaxValue;
        _shadowCpuMaxZ = float.MinValue;
        var previous = near;
        for (var cascade = 0; cascade < 4; cascade++)
        {
            var split = _cascadeSplits[cascade];
            _cascadeBlendStarts[cascade] = MathHelper.Lerp(previous, split, 0.88f);
            var nearAmount = (previous - near) / (far - near);
            var farAmount = (split - near) / (far - near);
            var corners = new Vector3[8];
            for (var corner = 0; corner < 4; corner++)
            {
                corners[corner] = Vector3.Lerp(cameraCorners[corner], cameraCorners[corner + 4], nearAmount);
                corners[corner + 4] = Vector3.Lerp(cameraCorners[corner], cameraCorners[corner + 4], farAmount);
            }
            var center = Vector3.Zero;
            foreach (var corner in corners) center += corner;
            center /= 8f;
            var lightRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, LightDirection));
            var lightUp = Vector3.Normalize(Vector3.Cross(LightDirection, lightRight));
            var lightView = Matrix.CreateLookAt(center - LightDirection * 5000f, center, lightUp);

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var corner in corners)
            {
                var lightSpace = Vector3.Transform(corner, lightView);
                min = Vector3.Min(min, lightSpace);
                max = Vector3.Max(max, lightSpace);
            }

            // A square projection remains stable as the camera rotates. Snap
            // its center to a shadow texel without moving it out of the actual
            // camera-frustum bounds.
            var extent = MathF.Max(max.X - min.X, max.Y - min.Y) * 0.5f;
            extent = MathF.Ceiling(extent * 16f) / 16f;
            var centerX = (min.X + max.X) * 0.5f;
            var centerY = (min.Y + max.Y) * 0.5f;
            var texel = extent * 2f / _shadowMaps[cascade].Width;
            centerX = MathF.Round(centerX / texel) * texel;
            centerY = MathF.Round(centerY / texel) * texel;
            const float depthPadding = 600f;
            var nearPlane = MathF.Max(1f, -max.Z - depthPadding);
            var farPlane = -min.Z + depthPadding;
            var lightProjection = Matrix.CreateOrthographicOffCenter(
                centerX - extent, centerX + extent, centerY - extent, centerY + extent,
                nearPlane, farPlane);
            _lightViewProjections[cascade] = lightView * lightProjection;
            foreach (var corner in corners)
            {
                var clip = Vector4.Transform(new Vector4(corner, 1f), _lightViewProjections[cascade]);
                var inverseW = 1f / clip.W;
                _shadowCpuMaxXY = MathF.Max(_shadowCpuMaxXY,
                    MathF.Max(MathF.Abs(clip.X * inverseW), MathF.Abs(clip.Y * inverseW)));
                _shadowCpuMinZ = MathF.Min(_shadowCpuMinZ, clip.Z * inverseW);
                _shadowCpuMaxZ = MathF.Max(_shadowCpuMaxZ, clip.Z * inverseW);
            }
            previous = split;
        }
    }

    private void SetShadowParameters(Effect effect)
    {
        for (var i = 0; i < 4; i++)
        {
            effect.Parameters[$"LightViewProjection{i}"].SetValue(_lightViewProjections[i]);
            effect.Parameters[$"ShadowMap{i}"].SetValue(_shadowMaps[i]);
        }
        effect.Parameters["ShadowMapTexelSize"].SetValue(new Vector2(1f / _shadowMaps[0].Width));
        effect.Parameters["ShadowFadeRange"].SetValue(new Vector2(3100f, 3200f));
        effect.Parameters["ShadowDebugMode"].SetValue((float)_shadowDebugMode);
    }

    private void SetTerrainTextures()
    {
        var names = new[] { "Grass", "Dirt", "Rock", "Sand" };
        for (var i = 0; i < names.Length; i++)
        {
            _terrainMaterialEffect.Parameters[$"{names[i]}AlbedoHeight"].SetValue(_terrainTextures.AlbedoHeightMaps[i]);
            _terrainMaterialEffect.Parameters[$"{names[i]}NormalAoRoughness"].SetValue(_terrainTextures.NormalAoRoughnessMaps[i]);
        }
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
        _population.Dispose();
        _water.Dispose();
        _roads.Dispose();
        _terrainTextures.Dispose();
        _terrain.Dispose();
        _skybox.Dispose();
        _environment.Dispose();
        _terrainEffect.Dispose();
        _terrainMaterialEffect.Dispose();
        _waterEffect.Dispose();
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

using CityBuilder.Tests.Pbr;
using CityBuilder.Tests.Terrain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Rendering;
using Nova3D.Rendering.Materials;
using Nova3D.Rendering.PostProcessing;
using Nova3D.Rendering.Shadows;
using Nova3D.Rendering.Water;
using Nova3D.Production.Configuration;
using Nova3D.Production.Debugging;
using Nova3D.Production.Logging;
using Nova3D.Physics.Bepu;
using Nova3D.World.Terrain;
using Nova3D.World.Streaming;
using Nova3D.World.Vegetation;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace CityBuilder.Benchmarks.CityBenchmark;

internal sealed class LargeWorldScene : IDisposable
{
    private readonly Effect _vegetationEffect;
    private readonly Effect _skyboxEffect;
    private readonly Effect _terrainEffect;
    private readonly Effect _shadowEffect;
    private readonly Effect _instancedShadowEffect;
    private readonly Effect _gltfGalleryEffect;
    private readonly HdrPostProcessPipeline _postProcess;
    private readonly CascadedShadowMap _shadows;
    private readonly NovaDirectionalLight _sun;
    private readonly PrimitiveMesh _skybox;
    private readonly IblEnvironment _environment;
    private readonly Nova3D.Rendering.Lighting.ImageBasedLighting _imageBasedLighting;
    private readonly LargeWorldTerrain _terrain;
    private readonly VegetationSystem _forest;
    private readonly BenchmarkPopulation _population;
    private readonly WaterRenderer _water;
    private readonly RoadNetwork _roads;
    private readonly TerrainMaterialTextures _terrainTextures;
    private readonly TerrainMaterial _terrainMaterial;
    private readonly LargeWorldCamera _camera = new();
    private readonly RenderContext _renderContext;
    private readonly DebugRenderer _debugRenderer;
    private readonly GltfValidationGallery _gltfGallery;
    private readonly ForwardLitMaterial _vegetationMaterial;
    private readonly ForwardLitMaterial _cityMaterial;
    private readonly BepuPhysicsWorld _physics;
    private readonly TerrainPhysics _physicsTerrain;
    private readonly List<BepuBody> _physicsBodies = new();
    private Matrix _projection;
    private double _smoothedFrameMilliseconds = 16.67;
    private int _shadowDebugMode;
    private bool _shadowDebugKeyWasDown;
    private bool _deformKeyWasDown;
    private bool _debugKeyWasDown;
    private bool _showDebugGeometry;
    private bool _galleryKeyWasDown;
    private bool _showGltfGallery;
    private bool _physicsDebugKeyWasDown;
    private bool _physicsResetKeyWasDown;
    private bool _showPhysicsDebug;
    private float _time;

    public Matrix View => _camera.View;
    public Matrix Projection => _projection;
    public Vector3 HudMarkerPosition => new(0f, LargeWorldTerrain.SampleHeight(0f, 0f) + 45f, 0f);

    public LargeWorldScene(GraphicsDevice device, Effect terrainEffect, Effect terrainMaterialEffect, Effect waterEffect, Effect vegetationEffect, Effect skyboxEffect,
        Effect shadowEffect, Effect instancedShadowEffect, Effect postProcessEffect, Effect pbrEffect,
        Nova3DConfiguration configuration, ILogger logger)
    {
        _terrainEffect = terrainEffect;
        _vegetationEffect = vegetationEffect;
        _skyboxEffect = skyboxEffect;
        _shadowEffect = shadowEffect;
        _instancedShadowEffect = instancedShadowEffect;
        _gltfGalleryEffect = pbrEffect;
        _postProcess = new HdrPostProcessPipeline(device, postProcessEffect)
        {
            Exposure = configuration.Renderer.Exposure,
            BloomStrength = configuration.Renderer.BloomStrength
        };
        _renderContext = new RenderContext(device);
        _debugRenderer = new DebugRenderer(device);
        _sun = new NovaDirectionalLight(new Vector3(-0.45f, -1f, -0.55f), new Vector3(1f, 0.96f, 0.88f));
        _shadows = new CascadedShadowMap(device, configuration.Renderer.ShadowResolution);
        _vegetationMaterial = new ForwardLitMaterial("vegetation", vegetationEffect, _sun.Direction);
        _cityMaterial = new ForwardLitMaterial("city", vegetationEffect, _sun.Direction, materialMode: 1f);
        _skybox = PrimitiveMesh.CreateCube(device);
        _environment = new IblEnvironment(device);
        _imageBasedLighting = new Nova3D.Rendering.Lighting.ImageBasedLighting(
            _environment.EnvironmentMap, _environment.IrradianceMap,
            _environment.PrefilteredMap, _environment.BrdfLut, _environment.PrefilterMipCount);
        _gltfGallery = new GltfValidationGallery(device, pbrEffect, _sun, _imageBasedLighting,
            Path.Combine(AppContext.BaseDirectory, "LocalAssets", "Models", "validation"), logger);
        _terrain = new LargeWorldTerrain(device, configuration.Streaming);
        _physics = new BepuPhysicsWorld();
        _physicsTerrain = new TerrainPhysics(_physics, _terrain.HeightProvider,
            new TerrainPhysicsSettings
            {
                WorldSize = LargeWorldTerrain.WorldSize,
                ChunksPerAxis = LargeWorldTerrain.ChunkCountPerAxis,
                SegmentsPerChunk = 16,
                Streaming = new WorldStreamingSettings
                {
                    CellSize = LargeWorldTerrain.WorldSize / LargeWorldTerrain.ChunkCountPerAxis,
                    Origin = new Vector2(-LargeWorldTerrain.WorldSize * 0.5f),
                    LoadRadius = configuration.Streaming.LoadRadius,
                    RetainRadius = configuration.Streaming.RetainRadius,
                    MaxLoadsPerUpdate = configuration.Streaming.MaxLoadsPerFrame,
                    MaxUnloadsPerUpdate = configuration.Streaming.MaxUnloadsPerFrame
                }
            });
        ResetPhysicsBodies();
        var vegetation = VegetationScatter.CreateGrid(10_000, 1900f, 9127,
            LargeWorldTerrain.SampleHeight,
            static (x, z) => !BenchmarkPopulation.InsideLake(x, z) &&
                (!(MathF.Abs(x) < 780f && z > -570f && z < 750f) ||
                 (MathF.Abs(x) < 115f && MathF.Abs(z) < 90f)));
        _forest = new VegetationSystem(device,
            new[]
            {
                ConiferMeshFactory.Create(device, 10, 3),
                ConiferMeshFactory.Create(device, 6, 2),
                ConiferMeshFactory.Create(device, 4, 1)
            },
            vegetation, new[] { 140f, 520f }, 1800f, 2.4f, Vector3.Up * 1.6f);
        _population = new BenchmarkPopulation(device);
        _water = new WaterRenderer(WaterSurface.CreateMesh(device),
            new WaterMaterial("benchmark-water", waterEffect, _sun));
        _roads = new RoadNetwork(device);
        _terrainTextures = new TerrainMaterialTextures(device);
        _terrainMaterial = new TerrainMaterial("benchmark-terrain", terrainMaterialEffect, _sun,
            new TerrainLayerSet(new[]
            {
                new TerrainLayer("Grass", _terrainTextures.AlbedoHeightMaps[0], _terrainTextures.NormalAoRoughnessMaps[0]),
                new TerrainLayer("Dirt", _terrainTextures.AlbedoHeightMaps[1], _terrainTextures.NormalAoRoughnessMaps[1]),
                new TerrainLayer("Rock", _terrainTextures.AlbedoHeightMaps[2], _terrainTextures.NormalAoRoughnessMaps[2]),
                new TerrainLayer("Sand", _terrainTextures.AlbedoHeightMaps[3], _terrainTextures.NormalAoRoughnessMaps[3])
            }));
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        var debugKeyDown = Keyboard.GetState().IsKeyDown(Keys.F1);
        if (debugKeyDown && !_shadowDebugKeyWasDown)
            _shadowDebugMode = (_shadowDebugMode + 1) % 5;
        _shadowDebugKeyWasDown = debugKeyDown;
        var deformKeyDown = Keyboard.GetState().IsKeyDown(Keys.F2);
        if (deformKeyDown && !_deformKeyWasDown)
        {
            TerrainRegion changed = _terrain.DeformRadial(0f, 0f, 95f, 12f);
            _physicsTerrain.RebuildRegion(changed);
        }
        _deformKeyWasDown = deformKeyDown;
        var geometryDebugKeyDown = Keyboard.GetState().IsKeyDown(Keys.F3);
        if (geometryDebugKeyDown && !_debugKeyWasDown)
            _showDebugGeometry = !_showDebugGeometry;
        _debugKeyWasDown = geometryDebugKeyDown;
        var galleryKeyDown = Keyboard.GetState().IsKeyDown(Keys.F4);
        if (galleryKeyDown && !_galleryKeyWasDown)
            _showGltfGallery = !_showGltfGallery;
        _galleryKeyWasDown = galleryKeyDown;
        var physicsDebugKeyDown = Keyboard.GetState().IsKeyDown(Keys.F5);
        if (physicsDebugKeyDown && !_physicsDebugKeyWasDown)
            _showPhysicsDebug = !_showPhysicsDebug;
        _physicsDebugKeyWasDown = physicsDebugKeyDown;
        var physicsResetKeyDown = Keyboard.GetState().IsKeyDown(Keys.F6);
        if (physicsResetKeyDown && !_physicsResetKeyWasDown)
            ResetPhysicsBodies();
        _physicsResetKeyWasDown = physicsResetKeyDown;
        _camera.Update(gameTime, window);
        _camera.SetViewport(_vegetationEffect.GraphicsDevice.Viewport);
        _renderContext.BeginFrame(_camera.Camera);
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _projection = _camera.Projection;
        _terrain.Update(_camera.View, _projection, _camera.Position);
        _forest.Update(_camera.Camera);
        _population.Update(_camera.Camera);
        _physicsTerrain.Update(_camera.Position);
        _physics.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        if (elapsed > 0) _smoothedFrameMilliseconds = _smoothedFrameMilliseconds * 0.95 + elapsed * 0.05;
        var statistics = _renderContext.LastFrameStatistics;
        window.Title = $"CityBuilder - CityBenchmark | FPS {1000.0 / _smoothedFrameMilliseconds:F0} | {_smoothedFrameMilliseconds:F2} ms | " +
                       $"chunks {_terrain.VisibleCount}/{_terrain.ResidentChunkCount}/{LargeWorldTerrain.TotalChunks} " +
                       $"[{_terrain.VisibleLods[0]}/{_terrain.VisibleLods[1]}/{_terrain.VisibleLods[2]}] " +
                       $"stream +{_terrain.ChunkLoadsLastUpdate}/-{_terrain.ChunkUnloadsLastUpdate} pend {_terrain.PendingChunkLoads} | " +
                       $"arvores {_forest.TotalVisible}/10000 [{_forest.VisibleCounts[0]}/{_forest.VisibleCounts[1]}/{_forest.VisibleCounts[2]}] | " +
                       $"predios {_population.VisibleBuildings}/1000 | veiculos {_population.VisibleVehicles}/500 | " +
                       $"candidatos {_forest.CandidateCount + _population.CandidateCount} | " +
                       $"rebuild {_terrain.LastRebuiltChunkCount} | " +
                       $"draws {statistics.DrawCalls} | tris {statistics.Triangles / 1000f:F0}k | shadow {statistics.ShadowDrawCalls} | shadow debug {_shadowDebugMode} | " +
                       $"CPU sh {_renderContext.Profiler.GetSmoothedMilliseconds("shadow"):F2} " +
                       $"world {_renderContext.Profiler.GetSmoothedMilliseconds("world"):F2} " +
                       $"post {_renderContext.Profiler.GetSmoothedMilliseconds("post"):F2} ms | dbg {(_showDebugGeometry ? 1 : 0)} | " +
                       $"CPU xy {_shadows.DebugMaxXY:F2} z {_shadows.DebugMinZ:F2}..{_shadows.DebugMaxZ:F2}";
        if (_showPhysicsDebug)
            window.Title += $" | PHYS {_physics.LastStepMilliseconds:F2} ms " +
                            $"bodies {_physics.BodyCount} chunks {_physicsTerrain.ChunkCount} | F6 reset";
        if (_showGltfGallery)
            window.Title += $" | GLB {_gltfGallery.LoadedCount}/{_gltfGallery.FileCount} falhas {_gltfGallery.FailedCount}";
    }

    public void Draw(float aspectRatio)
    {
        using (_renderContext.Profiler.Measure("shadow"))
            DrawShadowMap();
        var device = _vegetationEffect.GraphicsDevice;
        using (_renderContext.Profiler.Measure("world"))
        {
            _postProcess.BeginScene(new Color(18, 22, 30));
            DrawSkybox();
            _renderContext.Statistics.RecordDraw(_skybox.PrimitiveCount);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullClockwise;

            _terrainMaterial.Apply(_renderContext);
            _shadows.Apply(_terrainMaterial.Effect, _shadowDebugMode);
            _terrain.Draw(device, _terrainMaterial.Effect);
            _renderContext.Statistics.RecordDraws(_terrain.DrawCalls, _terrain.VisibleTriangles);
            _renderContext.Statistics.RecordVisibleChunks(_terrain.VisibleCount);

            _terrainEffect.Parameters["ViewProjection"].SetValue(_camera.View * _projection);
            _shadows.Apply(_terrainEffect, _shadowDebugMode);
            _terrainEffect.Parameters["LightDirection"].SetValue(_sun.Direction);
            _terrainEffect.Parameters["View"].SetValue(_camera.View);
            if (!_showGltfGallery)
            {
                _roads.Draw(device, _terrainEffect);
                _renderContext.Statistics.RecordDraw(_roads.PrimitiveCount);
            }

            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            _shadows.Apply(_vegetationEffect, _shadowDebugMode);
            _vegetationMaterial.Apply(_renderContext);
            if (_showGltfGallery)
            {
                _shadows.Apply(_gltfGalleryEffect, _shadowDebugMode);
                _gltfGallery.Draw(_renderContext);
            }
            else
            {
                _forest.Draw(device, _vegetationMaterial.Effect);
                _renderContext.Statistics.RecordDraws(_forest.DrawCalls, _forest.VisibleTriangles, _forest.TotalVisible);
                _cityMaterial.Apply(_renderContext);
                _population.Draw(device, _cityMaterial.Effect);
                _renderContext.Statistics.RecordDraws(_population.DrawCalls, _population.VisibleTriangles,
                    _population.VisibleBuildings + _population.VisibleVehicles);
                _water.Draw(_renderContext, _time);
            }
            if (_showDebugGeometry)
            {
                _debugRenderer.Axes(Vector3.Zero, 120f);
                _debugRenderer.Frustum(_camera.Camera.Frustum, Color.Yellow);
                _debugRenderer.Flush(_renderContext);
            }
            if (_showPhysicsDebug)
            {
                _physics.DebugDraw(_debugRenderer, Color.Cyan);
                _debugRenderer.Flush(_renderContext);
            }
        }
        using (_renderContext.Profiler.Measure("post"))
            _postProcess.EndScene(_renderContext.Statistics);
    }

    private void DrawShadowMap()
    {
        var device = _terrainEffect.GraphicsDevice;
        _shadows.Update(_camera.Camera, _sun);
        for (var cascade = 0; cascade < 4; cascade++)
        {
            _shadows.BeginCascade(cascade);
            _shadowEffect.Parameters["World"].SetValue(Matrix.Identity);
            _shadowEffect.Parameters["LightViewProjection"].SetValue(_shadows.GetLightViewProjection(cascade));
            _terrain.DrawShadow(device, _shadowEffect);
            _renderContext.Statistics.RecordDraw(_terrain.ShadowPrimitiveCount, shadow: true);
            device.RasterizerState = RasterizerState.CullNone;
            _instancedShadowEffect.Parameters["LightViewProjection"].SetValue(_shadows.GetLightViewProjection(cascade));
            if (_showGltfGallery)
                _gltfGallery.DrawShadows(_renderContext, _shadowEffect, _shadows.GetLightViewProjection(cascade));
            else
            {
                _forest.Draw(device, _instancedShadowEffect);
                _renderContext.Statistics.RecordDraws(_forest.DrawCalls, _forest.VisibleTriangles,
                    _forest.TotalVisible, shadow: true);
                _population.Draw(device, _instancedShadowEffect);
                _renderContext.Statistics.RecordDraws(_population.DrawCalls, _population.VisibleTriangles,
                    _population.VisibleBuildings + _population.VisibleVehicles, shadow: true);
            }
        }
        _shadows.End();
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

    private void ResetPhysicsBodies()
    {
        foreach (BepuBody body in _physicsBodies)
            _physics.Remove(body);
        _physicsBodies.Clear();

        const int columns = 10;
        for (int i = 0; i < 100; i++)
        {
            float x = (i % columns - columns * 0.5f) * 10f;
            float z = 390f + (i / columns - columns * 0.5f) * 10f;
            float y = _terrain.HeightProvider.SampleHeight(x, z) + 10f + (i % 5) * 9f;
            _physicsBodies.Add(_physics.CreateDynamicBox(
                new Vector3(x, y, z), new Vector3(7f)));
        }
    }

    public void Dispose()
    {
        _physicsTerrain.Dispose();
        _physics.Dispose();
        _forest.Dispose();
        _population.Dispose();
        _water.Dispose();
        _roads.Dispose();
        _terrainTextures.Dispose();
        _terrain.Dispose();
        _skybox.Dispose();
        _environment.Dispose();
        _postProcess.Dispose();
        _shadows.Dispose();
        _debugRenderer.Dispose();
        _gltfGallery.Dispose();
    }
}

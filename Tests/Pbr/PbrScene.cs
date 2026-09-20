using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Rendering;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Materials;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace CityBuilder.Tests.Pbr;

internal sealed class PbrScene : IDisposable
{
    private readonly Effect _effect;
    private readonly Effect _skyboxEffect;
    private readonly Effect _shadowEffect;
    private readonly RenderTarget2D[] _shadowMaps = new RenderTarget2D[4];
    private readonly PrimitiveMesh _sphere;
    private readonly PrimitiveMesh _plane;
    private readonly PrimitiveMesh _cube;
    private readonly IblEnvironment _environment;
    private readonly OrbitCamera _camera = new();
    private readonly Camera3D _renderCamera = new() { NearPlane = 0.1f, FarPlane = 100f };
    private readonly RenderContext _renderContext;
    private readonly PbrMaterial _pbrMaterial;
    private KeyboardState _previousKeyboard;
    private int _debugView;
    private readonly Matrix[] _lightViewProjections = new Matrix[4];
    private readonly float[] _cascadeSplits = new float[4];
    private readonly float[] _cascadeBlendStarts = new float[4];
    private bool _showCascades;
    private readonly NovaDirectionalLight _light = new(new Vector3(-0.4f, -1f, -0.6f),
        new Vector3(3.0f, 2.85f, 2.65f));
    private readonly (Vector3 Position, Vector3 Albedo, float Metallic, float Roughness)[] _materials =
    {
        (new Vector3(-2.5f, 1f, 0f), new Vector3(1.00f, 0.71f, 0.29f), 1.0f, 0.18f),
        (new Vector3(0f, 1f, 0f), SrgbToLinear(new Vector3(0.08f, 0.30f, 0.78f)), 0.0f, 0.28f),
        (new Vector3(2.5f, 1f, 0f), SrgbToLinear(new Vector3(0.62f, 0.09f, 0.045f)), 0.0f, 0.82f)
    };

    public PbrScene(GraphicsDevice device, Effect effect, Effect skyboxEffect, Effect shadowEffect)
    {
        _effect = effect;
        _skyboxEffect = skyboxEffect;
        _shadowEffect = shadowEffect;
        _sphere = PrimitiveMesh.CreateSphere(device);
        _plane = PrimitiveMesh.CreatePlane(device);
        _cube = PrimitiveMesh.CreateCube(device);
        _environment = new IblEnvironment(device);
        _renderContext = new RenderContext(device);
        _pbrMaterial = new PbrMaterial("pbr-test", effect, _light,
            new ImageBasedLighting(_environment.EnvironmentMap, _environment.IrradianceMap,
                _environment.PrefilteredMap, _environment.BrdfLut, _environment.PrefilterMipCount))
        {
            Exposure = 0.85f
        };
        for (var i = 0; i < _shadowMaps.Length; i++)
            _shadowMaps[i] = new RenderTarget2D(
                device, 1024, 1024, false, SurfaceFormat.Single, DepthFormat.Depth24,
                0, RenderTargetUsage.DiscardContents);
    }

    public void Update(GameTime gameTime, GameWindow window)
    {
        _camera.Update(window);
        var keyboard = Keyboard.GetState();
        for (var i = 0; i <= 8; i++)
        {
            var key = (Keys)((int)Keys.D1 + i);
            if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
                _debugView = i;
        }
        if (keyboard.IsKeyDown(Keys.C) && !_previousKeyboard.IsKeyDown(Keys.C))
            _showCascades = !_showCascades;
        _previousKeyboard = keyboard;
        window.Title = _debugView switch
        {
            1 => "CityBuilder - PBR DEBUG: Albedo/F0",
            2 => "CityBuilder - PBR DEBUG: luz direta",
            3 => "CityBuilder - PBR DEBUG: IBL difuso",
            4 => "CityBuilder - PBR DEBUG: IBL especular",
            5 => "CityBuilder - PBR DEBUG: Fresnel/F0 efetivo",
            6 => "CityBuilder - PBR DEBUG: cubemap prefiltrado",
            7 => "CityBuilder - PBR DEBUG: BRDF LUT (A/B)",
            8 => "CityBuilder - SHADOW DEBUG: fator PCF",
            _ => _showCascades
                ? "CityBuilder - CSM DEBUG: cascatas 0/1/2/3"
                : "CityBuilder - Teste 02: 4 cascaded shadow maps"
        };
    }

    public void Draw(float aspectRatio)
    {
        var device = _effect.GraphicsDevice;
        _renderCamera.Position = _camera.Position;
        _renderCamera.Direction = Vector3.Normalize(_camera.Target - _camera.Position);
        _renderCamera.SetAspectRatio(aspectRatio);
        _renderContext.BeginFrame(_renderCamera);
        DrawShadowMaps(aspectRatio);
        DrawSkybox(aspectRatio);
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        // As primitivas procedurais usam winding anti-horario visto de fora.
        device.RasterizerState = RasterizerState.CullClockwise;

        _pbrMaterial.DebugView = _debugView;
        _pbrMaterial.ShowCascades = _showCascades;
        _pbrMaterial.Apply(_renderContext);
        _effect.Parameters["LightViewProjection0"].SetValue(_lightViewProjections[0]);
        _effect.Parameters["LightViewProjection1"].SetValue(_lightViewProjections[1]);
        _effect.Parameters["LightViewProjection2"].SetValue(_lightViewProjections[2]);
        _effect.Parameters["LightViewProjection3"].SetValue(_lightViewProjections[3]);
        _effect.Parameters["CascadeSplits"].SetValue(new Vector4(
            _cascadeSplits[0], _cascadeSplits[1], _cascadeSplits[2], _cascadeSplits[3]));
        _effect.Parameters["CascadeBlendStarts"].SetValue(new Vector4(
            _cascadeBlendStarts[0], _cascadeBlendStarts[1],
            _cascadeBlendStarts[2], _cascadeBlendStarts[3]));
        _effect.Parameters["ShadowMap0"].SetValue(_shadowMaps[0]);
        _effect.Parameters["ShadowMap1"].SetValue(_shadowMaps[1]);
        _effect.Parameters["ShadowMap2"].SetValue(_shadowMaps[2]);
        _effect.Parameters["ShadowMap3"].SetValue(_shadowMaps[3]);
        _effect.Parameters["ShadowMapTexelSize"].SetValue(new Vector2(
            1f / _shadowMaps[0].Width, 1f / _shadowMaps[0].Height));

        DrawMesh(_plane, Matrix.Identity, SrgbToLinear(new Vector3(0.38f, 0.39f, 0.41f)), 0f, 0.76f, 1f);
        foreach (var material in _materials)
            DrawMesh(_sphere, Matrix.CreateTranslation(material.Position), material.Albedo,
                material.Metallic, material.Roughness, 1f);
    }

    private void DrawShadowMaps(float aspectRatio)
    {
        var device = _shadowEffect.GraphicsDevice;
        BuildCascadeMatrices(aspectRatio);
        for (var cascade = 0; cascade < 4; cascade++)
        {
            device.SetRenderTarget(_shadowMaps[cascade]);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            _shadowEffect.Parameters["LightViewProjection"].SetValue(_lightViewProjections[cascade]);

            foreach (var material in _materials)
                DrawShadowMesh(_sphere, Matrix.CreateTranslation(material.Position));
        }

        device.SetRenderTarget(null);
    }

    private void BuildCascadeMatrices(float aspectRatio)
    {
        const float cameraNear = 0.1f;
        const float shadowDistance = 40f;
        const float splitLambda = 0.65f;
        const float fieldOfView = MathHelper.PiOver4;
        const float shadowDepthPadding = 12f;

        for (var i = 1; i <= 4; i++)
        {
            var ratio = i / 4f;
            var logarithmic = cameraNear * MathF.Pow(shadowDistance / cameraNear, ratio);
            var uniform = cameraNear + (shadowDistance - cameraNear) * ratio;
            _cascadeSplits[i - 1] = MathHelper.Lerp(uniform, logarithmic, splitLambda);
        }

        var forward = Vector3.Normalize(_camera.Target - _camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var previousSplit = cameraNear;

        for (var cascade = 0; cascade < 4; cascade++)
        {
            var split = _cascadeSplits[cascade];
            _cascadeBlendStarts[cascade] = MathHelper.Lerp(previousSplit, split, 0.9f);
            var corners = BuildFrustumSliceCorners(
                _camera.Position, forward, right, up, previousSplit, split, fieldOfView, aspectRatio);
            var center = Vector3.Zero;
            foreach (var corner in corners)
                center += corner;
            center /= corners.Length;

            var radius = 0f;
            foreach (var corner in corners)
                radius = MathF.Max(radius, Vector3.Distance(center, corner));
            radius = MathF.Ceiling(radius * 16f) / 16f;

            var lightRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, _light.Direction));
            var lightUp = Vector3.Normalize(Vector3.Cross(_light.Direction, lightRight));
            var unitsPerTexel = (radius * 2f) / _shadowMaps[cascade].Width;
            var rightCoordinate = MathF.Round(Vector3.Dot(center, lightRight) / unitsPerTexel) * unitsPerTexel;
            var upCoordinate = MathF.Round(Vector3.Dot(center, lightUp) / unitsPerTexel) * unitsPerTexel;
            center += lightRight * (rightCoordinate - Vector3.Dot(center, lightRight));
            center += lightUp * (upCoordinate - Vector3.Dot(center, lightUp));

            var lightPosition = center - _light.Direction * (radius + shadowDepthPadding);
            var lightView = Matrix.CreateLookAt(lightPosition, center, lightUp);
            var lightProjection = Matrix.CreateOrthographic(
                radius * 2f, radius * 2f, 0.1f, radius * 2f + shadowDepthPadding * 2f);
            _lightViewProjections[cascade] = lightView * lightProjection;
            previousSplit = split;
        }
    }

    private static Vector3[] BuildFrustumSliceCorners(
        Vector3 position, Vector3 forward, Vector3 right, Vector3 up,
        float near, float far, float fieldOfView, float aspectRatio)
    {
        var tangent = MathF.Tan(fieldOfView * 0.5f);
        var nearHeight = tangent * near;
        var nearWidth = nearHeight * aspectRatio;
        var farHeight = tangent * far;
        var farWidth = farHeight * aspectRatio;
        var nearCenter = position + forward * near;
        var farCenter = position + forward * far;
        return new[]
        {
            nearCenter - right * nearWidth - up * nearHeight,
            nearCenter + right * nearWidth - up * nearHeight,
            nearCenter + right * nearWidth + up * nearHeight,
            nearCenter - right * nearWidth + up * nearHeight,
            farCenter - right * farWidth - up * farHeight,
            farCenter + right * farWidth - up * farHeight,
            farCenter + right * farWidth + up * farHeight,
            farCenter - right * farWidth + up * farHeight
        };
    }

    private void DrawShadowMesh(PrimitiveMesh mesh, Matrix world)
    {
        _shadowEffect.Parameters["World"].SetValue(world);
        foreach (var pass in _shadowEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            mesh.Draw(_shadowEffect.GraphicsDevice);
        }
    }

    private void DrawSkybox(float aspectRatio)
    {
        var device = _skyboxEffect.GraphicsDevice;
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullNone;
        _skyboxEffect.Parameters["World"].SetValue(Matrix.CreateScale(30f));
        _skyboxEffect.Parameters["View"].SetValue(Matrix.CreateLookAt(
            Vector3.Zero, Vector3.Normalize(_camera.Target - _camera.Position), Vector3.Up));
        _skyboxEffect.Parameters["Projection"].SetValue(Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45f), aspectRatio, 0.1f, 100f));
        _skyboxEffect.Parameters["EnvironmentMap"].SetValue(_environment.EnvironmentMap);
        _skyboxEffect.Parameters["Exposure"].SetValue(0.85f);
        foreach (var pass in _skyboxEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _cube.Draw(device);
        }
    }

    private static Vector3 SrgbToLinear(Vector3 color) => new(
        MathF.Pow(color.X, 2.2f),
        MathF.Pow(color.Y, 2.2f),
        MathF.Pow(color.Z, 2.2f));

    private void DrawMesh(PrimitiveMesh mesh, Matrix world, Vector3 albedo, float metallic, float roughness, float ao)
    {
        _pbrMaterial.Albedo = albedo;
        _pbrMaterial.Metallic = metallic;
        _pbrMaterial.Roughness = roughness;
        _pbrMaterial.AmbientOcclusion = ao;
        _pbrMaterial.ApplySurface(world);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            mesh.Draw(_effect.GraphicsDevice);
        }
    }

    public void Dispose()
    {
        _sphere.Dispose();
        _plane.Dispose();
        _cube.Dispose();
        _environment.Dispose();
        foreach (var shadowMap in _shadowMaps)
            shadowMap.Dispose();
    }
}

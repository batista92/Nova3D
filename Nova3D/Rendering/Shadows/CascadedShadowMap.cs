using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.Rendering.Shadows;

public sealed class CascadedShadowMap : IDisposable
{
    public const int CascadeCount = 4;

    private readonly GraphicsDevice _device;
    private readonly RenderTarget2D[] _maps = new RenderTarget2D[CascadeCount];
    private readonly Matrix[] _lightViewProjections = new Matrix[CascadeCount];
    private readonly float[] _splits = new float[CascadeCount];
    private readonly float[] _blendStarts = new float[CascadeCount];

    public CascadedShadowMap(GraphicsDevice device, int resolution = 4096)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        if (resolution <= 0) throw new ArgumentOutOfRangeException(nameof(resolution));
        Resolution = resolution;
        for (var i = 0; i < CascadeCount; i++)
            _maps[i] = new RenderTarget2D(device, resolution, resolution, false, SurfaceFormat.Single,
                DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
        CasterRasterizer = new RasterizerState
        {
            CullMode = CullMode.CullClockwiseFace,
            DepthBias = 0.003f,
            SlopeScaleDepthBias = 4f
        };
    }

    public int Resolution { get; }
    public float SplitLambda { get; set; } = 0.76f;
    public float BlendStart { get; set; } = 0.88f;
    public float DepthPadding { get; set; } = 600f;
    public float FadeStart { get; set; } = 3100f;
    public float FadeEnd { get; set; } = 3200f;
    public RasterizerState CasterRasterizer { get; }
    public float DebugMaxXY { get; private set; }
    public float DebugMinZ { get; private set; }
    public float DebugMaxZ { get; private set; }

    public Matrix GetLightViewProjection(int cascade) => _lightViewProjections[ValidateCascade(cascade)];

    public void Update(Camera3D camera, NovaDirectionalLight light)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(light);
        var near = camera.NearPlane;
        var far = camera.FarPlane;
        for (var i = 1; i <= CascadeCount; i++)
        {
            var ratio = i / (float)CascadeCount;
            _splits[i - 1] = MathHelper.Lerp(near + (far - near) * ratio,
                near * MathF.Pow(far / near, ratio), SplitLambda);
        }

        var cameraCorners = camera.Frustum.GetCorners();
        DebugMaxXY = 0f;
        DebugMinZ = float.MaxValue;
        DebugMaxZ = float.MinValue;
        var previous = near;
        for (var cascade = 0; cascade < CascadeCount; cascade++)
        {
            var split = _splits[cascade];
            _blendStarts[cascade] = MathHelper.Lerp(previous, split, BlendStart);
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
            var lightRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, light.Direction));
            var lightUp = Vector3.Normalize(Vector3.Cross(light.Direction, lightRight));
            var lightView = Matrix.CreateLookAt(center - light.Direction * 5000f, center, lightUp);
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var corner in corners)
            {
                var lightSpace = Vector3.Transform(corner, lightView);
                min = Vector3.Min(min, lightSpace);
                max = Vector3.Max(max, lightSpace);
            }

            var extent = MathF.Ceiling(MathF.Max(max.X - min.X, max.Y - min.Y) * 8f) / 16f;
            var texel = extent * 2f / Resolution;
            var centerX = MathF.Round(((min.X + max.X) * 0.5f) / texel) * texel;
            var centerY = MathF.Round(((min.Y + max.Y) * 0.5f) / texel) * texel;
            var nearPlane = MathF.Max(1f, -max.Z - DepthPadding);
            var farPlane = -min.Z + DepthPadding;
            var lightProjection = Matrix.CreateOrthographicOffCenter(
                centerX - extent, centerX + extent, centerY - extent, centerY + extent,
                nearPlane, farPlane);
            _lightViewProjections[cascade] = lightView * lightProjection;

            foreach (var corner in corners)
            {
                var clip = Vector4.Transform(new Vector4(corner, 1f), _lightViewProjections[cascade]);
                var inverseW = 1f / clip.W;
                DebugMaxXY = MathF.Max(DebugMaxXY,
                    MathF.Max(MathF.Abs(clip.X * inverseW), MathF.Abs(clip.Y * inverseW)));
                DebugMinZ = MathF.Min(DebugMinZ, clip.Z * inverseW);
                DebugMaxZ = MathF.Max(DebugMaxZ, clip.Z * inverseW);
            }
            previous = split;
        }
    }

    public void BeginCascade(int cascade)
    {
        _device.SetRenderTarget(_maps[ValidateCascade(cascade)]);
        _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        _device.RasterizerState = CasterRasterizer;
    }

    public void End() => _device.SetRenderTarget(null);

    public void Apply(Effect effect, int debugMode = 0)
    {
        ArgumentNullException.ThrowIfNull(effect);
        for (var i = 0; i < CascadeCount; i++)
        {
            effect.Parameters[$"LightViewProjection{i}"]?.SetValue(_lightViewProjections[i]);
            effect.Parameters[$"ShadowMap{i}"]?.SetValue(_maps[i]);
        }
        effect.Parameters["ShadowMapTexelSize"]?.SetValue(new Vector2(1f / Resolution));
        effect.Parameters["ShadowFadeRange"]?.SetValue(new Vector2(FadeStart, FadeEnd));
        effect.Parameters["CascadeSplits"]?.SetValue(new Vector4(_splits[0], _splits[1], _splits[2], _splits[3]));
        effect.Parameters["CascadeBlendStarts"]?.SetValue(new Vector4(_blendStarts[0], _blendStarts[1], _blendStarts[2], _blendStarts[3]));
        effect.Parameters["ShadowDebugMode"]?.SetValue((float)debugMode);
    }

    public void Dispose()
    {
        foreach (var map in _maps) map.Dispose();
        CasterRasterizer.Dispose();
    }

    private static int ValidateCascade(int cascade)
    {
        if ((uint)cascade >= CascadeCount) throw new ArgumentOutOfRangeException(nameof(cascade));
        return cascade;
    }
}

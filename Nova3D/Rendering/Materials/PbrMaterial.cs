using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering.Lighting;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.Rendering.Materials;

public sealed class PbrMaterial : Material
{
    public PbrMaterial(string name, Effect effect, NovaDirectionalLight light,
        ImageBasedLighting environment, string? technique = null) : base(name, effect, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public PbrMaterial(string name, Func<Effect> effectProvider, NovaDirectionalLight light,
        ImageBasedLighting environment, string? technique = null) : base(name, effectProvider, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public NovaDirectionalLight Light { get; }
    public ImageBasedLighting Environment { get; }
    public Vector3 Albedo { get; set; } = Vector3.One;
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public float AmbientOcclusion { get; set; } = 1f;
    public Texture2D? BaseColorTexture { get; set; }
    public Texture2D? NormalTexture { get; set; }
    public Texture2D? MetallicRoughnessTexture { get; set; }
    public Texture2D? OcclusionTexture { get; set; }
    public float NormalScale { get; set; } = 1f;
    public float OcclusionStrength { get; set; } = 1f;
    public float Exposure { get; set; } = 1f;
    public int DebugView { get; set; }
    public bool ShowCascades { get; set; }

    protected override void ApplyParameters(RenderContext context)
    {
        var camera = context.Camera ?? throw new InvalidOperationException(
            "RenderContext.BeginFrame must be called before applying materials.");
        Effect.Parameters["View"]?.SetValue(camera.View);
        Effect.Parameters["Projection"]?.SetValue(camera.Projection);
        Effect.Parameters["CameraPosition"]?.SetValue(camera.Position);
        Effect.Parameters["LightDirection"]?.SetValue(Light.Direction);
        Effect.Parameters["LightColor"]?.SetValue(Light.Color * Light.Intensity);
        Effect.Parameters["Exposure"]?.SetValue(Exposure);
        Effect.Parameters["IrradianceMap"]?.SetValue(Environment.IrradianceMap);
        Effect.Parameters["PrefilteredMap"]?.SetValue(Environment.PrefilteredMap);
        Effect.Parameters["BrdfLut"]?.SetValue(Environment.BrdfLut);
        Effect.Parameters["MaxReflectionLod"]?.SetValue(Environment.PrefilteredMipCount - 1f);
        Effect.Parameters["DebugView"]?.SetValue((float)DebugView);
        Effect.Parameters["ShowCascades"]?.SetValue(ShowCascades ? 1f : 0f);
    }

    public void ApplySurface(Matrix world)
    {
        Effect.Parameters["World"]?.SetValue(world);
        Effect.Parameters["WorldInverseTranspose"]?.SetValue(Matrix.Transpose(Matrix.Invert(world)));
        Effect.Parameters["Albedo"]?.SetValue(Albedo);
        Effect.Parameters["Metallic"]?.SetValue(MathHelper.Clamp(Metallic, 0f, 1f));
        Effect.Parameters["Roughness"]?.SetValue(MathHelper.Clamp(Roughness, 0.045f, 1f));
        Effect.Parameters["AmbientOcclusion"]?.SetValue(MathHelper.Clamp(AmbientOcclusion, 0f, 1f));
        Effect.Parameters["BaseColorTexture"]?.SetValue(BaseColorTexture);
        Effect.Parameters["NormalTexture"]?.SetValue(NormalTexture);
        Effect.Parameters["MetallicRoughnessTexture"]?.SetValue(MetallicRoughnessTexture);
        Effect.Parameters["OcclusionTexture"]?.SetValue(OcclusionTexture);
        Effect.Parameters["HasBaseColorTexture"]?.SetValue(BaseColorTexture is null ? 0f : 1f);
        Effect.Parameters["HasNormalTexture"]?.SetValue(NormalTexture is null ? 0f : 1f);
        Effect.Parameters["HasMetallicRoughnessTexture"]?.SetValue(MetallicRoughnessTexture is null ? 0f : 1f);
        Effect.Parameters["HasOcclusionTexture"]?.SetValue(OcclusionTexture is null ? 0f : 1f);
        Effect.Parameters["NormalScale"]?.SetValue(NormalScale);
        Effect.Parameters["OcclusionStrength"]?.SetValue(MathHelper.Clamp(OcclusionStrength, 0f, 1f));
    }
}

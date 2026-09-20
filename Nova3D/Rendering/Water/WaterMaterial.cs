using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Materials;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.Rendering.Water;

public sealed class WaterMaterial : Material
{
    public WaterMaterial(string name, Effect effect, NovaDirectionalLight light,
        string? technique = null) : base(name, effect, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
    }

    public WaterMaterial(string name, Func<Effect> effectProvider, NovaDirectionalLight light,
        string? technique = null) : base(name, effectProvider, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
    }

    public NovaDirectionalLight Light { get; }
    public float Time { get; set; }

    protected override void ApplyParameters(RenderContext context)
    {
        var camera = context.Camera ?? throw new InvalidOperationException(
            "RenderContext.BeginFrame must be called before applying materials.");
        Effect.Parameters["ViewProjection"]?.SetValue(camera.ViewProjection);
        Effect.Parameters["CameraPosition"]?.SetValue(camera.Position);
        Effect.Parameters["LightDirection"]?.SetValue(Light.Direction);
        Effect.Parameters["Time"]?.SetValue(Time);
    }
}

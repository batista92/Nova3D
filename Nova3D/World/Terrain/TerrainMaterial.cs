using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;
using Nova3D.Rendering.Materials;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace Nova3D.World.Terrain;

public sealed class TerrainMaterial : Material
{
    public TerrainMaterial(string name, Effect effect, NovaDirectionalLight light,
        TerrainLayerSet layers, string? technique = null) : base(name, effect, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    public TerrainMaterial(string name, Func<Effect> effectProvider, NovaDirectionalLight light,
        TerrainLayerSet layers, string? technique = null) : base(name, effectProvider, technique)
    {
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    public NovaDirectionalLight Light { get; }
    public TerrainLayerSet Layers { get; }
    public float TextureScale { get; set; } = 0.055f;
    public float TriplanarSharpness { get; set; } = 5f;

    protected override void ApplyParameters(RenderContext context)
    {
        var camera = context.Camera ?? throw new InvalidOperationException(
            "RenderContext.BeginFrame must be called before applying materials.");
        Effect.Parameters["ViewProjection"]?.SetValue(camera.ViewProjection);
        Effect.Parameters["View"]?.SetValue(camera.View);
        Effect.Parameters["CameraPosition"]?.SetValue(camera.Position);
        Effect.Parameters["LightDirection"]?.SetValue(Light.Direction);
        Effect.Parameters["TextureScale"]?.SetValue(TextureScale);
        Effect.Parameters["TriplanarSharpness"]?.SetValue(TriplanarSharpness);
        Layers.Apply(Effect);
    }
}

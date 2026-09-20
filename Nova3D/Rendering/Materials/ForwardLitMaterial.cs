using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Materials;

/// <summary>
/// Common forward-lit parameters used by opaque meshes and vegetation.
/// Shader-specific systems, such as CSM, can append their own parameters.
/// </summary>
public sealed class ForwardLitMaterial : Material
{
    public ForwardLitMaterial(string name, Effect effect, Vector3 lightDirection, float materialMode = 0f,
        string? technique = null) : base(name, effect, technique)
    {
        LightDirection = Vector3.Normalize(lightDirection);
        MaterialMode = materialMode;
    }

    public Vector3 LightDirection { get; set; }
    public float MaterialMode { get; set; }

    protected override void ApplyParameters(RenderContext context)
    {
        var camera = context.Camera ?? throw new InvalidOperationException("RenderContext.BeginFrame must be called before applying materials.");
        Effect.Parameters["ViewProjection"]?.SetValue(camera.ViewProjection);
        Effect.Parameters["View"]?.SetValue(camera.View);
        Effect.Parameters["CameraPosition"]?.SetValue(camera.Position);
        Effect.Parameters["LightDirection"]?.SetValue(Vector3.Normalize(LightDirection));
        Effect.Parameters["MaterialMode"]?.SetValue(MaterialMode);
    }
}

using Nova3D.Rendering.Lighting;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>A directional light plus its scene-authored shadow intent.</summary>
public sealed class SceneDirectionalLightComponent
{
    internal SceneDirectionalLightComponent(DirectionalLight light, bool castsShadows)
    {
        Light = light;
        CastsShadows = castsShadows;
    }

    public DirectionalLight Light { get; }
    public bool CastsShadows { get; }
}

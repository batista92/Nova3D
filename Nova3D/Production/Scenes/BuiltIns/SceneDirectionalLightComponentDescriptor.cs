using Microsoft.Xna.Framework;
using Nova3D.Rendering.Lighting;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Creates a directional light whose direction follows the node forward axis.</summary>
public sealed class SceneDirectionalLightComponentDescriptor : ISceneRuntimeComponentDescriptor
{
    public const string ComponentType = "nova3d.directional-light";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        SceneBuiltInProperties.ValidateKnown(context, "color", "intensity", "castShadows");
        SceneBuiltInProperties.OptionalColor(context, "color", Vector3.One);
        SceneBuiltInProperties.OptionalNumber(context, "intensity", 1f,
            value => value >= 0f, "must be greater than or equal to zero");
        SceneBuiltInProperties.OptionalBoolean(context, "castShadows", true);
    }

    public object Create(SceneComponentInstantiationContext context)
    {
        var light = new DirectionalLight(
            SceneBuiltInProperties.Forward(context.WorldTransform),
            SceneBuiltInProperties.ReadColor(context.Component, "color", Vector3.One),
            SceneBuiltInProperties.ReadNumber(context.Component, "intensity", 1f));
        return new SceneDirectionalLightComponent(light,
            SceneBuiltInProperties.ReadBoolean(context.Component, "castShadows", true));
    }

    public void Destroy(object instance) => ArgumentNullException.ThrowIfNull(instance);
}

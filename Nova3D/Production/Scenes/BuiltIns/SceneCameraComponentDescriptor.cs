using Microsoft.Xna.Framework;
using Nova3D.Rendering;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Creates a Camera3D from the owning node and projection properties.</summary>
public sealed class SceneCameraComponentDescriptor : ISceneRuntimeComponentDescriptor
{
    public const string ComponentType = "nova3d.camera";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        SceneBuiltInProperties.ValidateKnown(context,
            "primary", "fieldOfViewDegrees", "nearPlane", "farPlane");
        SceneBuiltInProperties.OptionalBoolean(context, "primary", false);
        SceneBuiltInProperties.OptionalNumber(context, "fieldOfViewDegrees", 45f,
            value => value > 0f && value < 180f, "must be greater than 0 and less than 180");
        var nearPlane = SceneBuiltInProperties.OptionalNumber(context, "nearPlane", 0.1f,
            value => value > 0f, "must be greater than zero");
        SceneBuiltInProperties.OptionalNumber(context, "farPlane", 1000f,
            value => value > nearPlane, "must be greater than nearPlane");
    }

    public object Create(SceneComponentInstantiationContext context)
    {
        var direction = SceneBuiltInProperties.Forward(context.WorldTransform);
        var camera = new Camera3D
        {
            Position = context.WorldTransform.Translation,
            Direction = direction,
            Up = SceneBuiltInProperties.CameraUp(context.WorldTransform, direction),
            FieldOfView = MathHelper.ToRadians(SceneBuiltInProperties.ReadNumber(
                context.Component, "fieldOfViewDegrees", 45f)),
            NearPlane = SceneBuiltInProperties.ReadNumber(context.Component, "nearPlane", 0.1f),
            FarPlane = SceneBuiltInProperties.ReadNumber(context.Component, "farPlane", 1000f)
        };
        return new SceneCameraComponent(camera,
            SceneBuiltInProperties.ReadBoolean(context.Component, "primary", false));
    }

    public void Destroy(object instance) => ArgumentNullException.ThrowIfNull(instance);
}

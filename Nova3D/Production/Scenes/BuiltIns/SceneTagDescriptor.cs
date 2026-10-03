namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Creates a string tag without assigning gameplay meaning to it.</summary>
public sealed class SceneTagDescriptor : ISceneRuntimeComponentDescriptor
{
    public const string ComponentType = "nova3d.tag";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        SceneBuiltInProperties.ValidateKnown(context, "value");
        SceneBuiltInProperties.RequireString(context, "value", out _);
    }

    public object Create(SceneComponentInstantiationContext context) =>
        new SceneTag(SceneBuiltInProperties.ReadString(context.Component, "value"));

    public void Destroy(object instance) => ArgumentNullException.ThrowIfNull(instance);
}

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Creates a gameplay-owned spawn marker without creating game entities.</summary>
public sealed class SceneSpawnPointDescriptor : ISceneRuntimeComponentDescriptor
{
    public const string ComponentType = "nova3d.spawn";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        SceneBuiltInProperties.ValidateKnown(context, "kind");
        SceneBuiltInProperties.RequireString(context, "kind", out _);
    }

    public object Create(SceneComponentInstantiationContext context) =>
        new SceneSpawnPoint(
            SceneBuiltInProperties.ReadString(context.Component, "kind"),
            context.WorldTransform);

    public void Destroy(object instance) => ArgumentNullException.ThrowIfNull(instance);
}

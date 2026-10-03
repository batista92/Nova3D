namespace Nova3D.Production.Scenes;

/// <summary>Small descriptor backed by an explicit validation delegate.</summary>
public sealed class DelegateSceneComponentDescriptor : ISceneComponentDescriptor
{
    private readonly Action<SceneComponentValidationContext>? _validate;

    public DelegateSceneComponentDescriptor(
        string type,
        Action<SceneComponentValidationContext>? validate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Type = type;
        _validate = validate;
    }

    public string Type { get; }

    public void Validate(SceneComponentValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _validate?.Invoke(context);
    }
}

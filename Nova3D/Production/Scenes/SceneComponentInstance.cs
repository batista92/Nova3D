namespace Nova3D.Production.Scenes;

/// <summary>A runtime value created for one scene component.</summary>
public sealed class SceneComponentInstance
{
    internal SceneComponentInstance(
        SceneComponentDocument document,
        object value,
        ISceneRuntimeComponentDescriptor descriptor)
    {
        Document = document;
        Value = value;
        Descriptor = descriptor;
    }

    public SceneComponentDocument Document { get; }
    public string Id => Document.Id;
    public string Type => Document.Type;
    public object Value { get; }

    internal ISceneRuntimeComponentDescriptor Descriptor { get; }
}

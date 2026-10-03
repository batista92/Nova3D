namespace Nova3D.Production.Scenes;

/// <summary>Identifies the component whose runtime factory failed.</summary>
public sealed class SceneInstantiationException : Exception
{
    public SceneInstantiationException(
        string documentPath,
        string nodeId,
        string componentId,
        string componentType,
        Exception innerException)
        : base(
            $"Failed to instantiate component '{componentId}' ({componentType}) on node '{nodeId}' " +
            $"from scene '{documentPath}'.",
            innerException)
    {
        DocumentPath = documentPath;
        NodeId = nodeId;
        ComponentId = componentId;
        ComponentType = componentType;
    }

    public string DocumentPath { get; }
    public string NodeId { get; }
    public string ComponentId { get; }
    public string ComponentType { get; }
}

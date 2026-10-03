namespace Nova3D.Production.Scenes;

/// <summary>Validated CPU-only scene plan ready for runtime instantiation.</summary>
public sealed class SceneLoadPlan
{
    private readonly IReadOnlyList<ScenePlannedNode> _nodes;

    internal SceneLoadPlan(
        string documentPath,
        SceneDocument document,
        IEnumerable<ScenePlannedNode> nodes)
    {
        DocumentPath = documentPath;
        Document = document;
        _nodes = Array.AsReadOnly(nodes.ToArray());
    }

    public string DocumentPath { get; }
    public SceneDocument Document { get; }
    public IReadOnlyList<ScenePlannedNode> Nodes => _nodes;
}

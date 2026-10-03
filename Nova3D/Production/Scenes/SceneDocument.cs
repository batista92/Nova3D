namespace Nova3D.Production.Scenes;

/// <summary>CPU-only representation of a versioned Nova3D scene document.</summary>
public sealed class SceneDocument
{
    public const string FormatName = "nova3d.scene";
    public const int CurrentVersion = 1;

    private readonly IReadOnlyList<SceneNodeDocument> _nodes;

    public SceneDocument(
        string format,
        int version,
        string name,
        IEnumerable<SceneNodeDocument> nodes)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(nodes);

        Format = format;
        Version = version;
        Name = name;
        _nodes = Array.AsReadOnly(nodes.ToArray());
    }

    public string Format { get; }
    public int Version { get; }
    public string Name { get; }
    public IReadOnlyList<SceneNodeDocument> Nodes => _nodes;
}

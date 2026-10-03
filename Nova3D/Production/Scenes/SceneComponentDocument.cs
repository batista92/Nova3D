using System.Text.Json;

namespace Nova3D.Production.Scenes;

/// <summary>Typed component payload retained as immutable JSON data.</summary>
public sealed class SceneComponentDocument
{
    public SceneComponentDocument(string id, string type, JsonElement properties)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(type);
        if (properties.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Scene component properties must be a JSON object.", nameof(properties));

        Id = id;
        Type = type;
        Properties = properties.Clone();
    }

    public string Id { get; }
    public string Type { get; }
    public JsonElement Properties { get; }
}

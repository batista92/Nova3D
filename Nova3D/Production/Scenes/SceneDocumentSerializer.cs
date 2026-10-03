using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>Strict UTF-8 reader and deterministic writer for scene documents.</summary>
public static class SceneDocumentSerializer
{
    private const string MemorySource = "<memory>";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private static readonly string[] RootProperties = ["format", "version", "name", "nodes"];
    private static readonly string[] NodeProperties = ["id", "name", "parent", "transform", "components"];
    private static readonly string[] TransformProperties = ["position", "rotationDegrees", "scale"];
    private static readonly string[] ComponentProperties = ["id", "type", "properties"];

    public static SceneDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return Parse(File.ReadAllBytes(fullPath), fullPath);
    }

    public static SceneDocument Parse(string json, string documentPath = MemorySource)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Parse(Encoding.UTF8.GetBytes(json), documentPath);
    }

    public static SceneDocument Parse(ReadOnlyMemory<byte> utf8Json, string documentPath = MemorySource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        try
        {
            using var json = JsonDocument.Parse(utf8Json, DocumentOptions);
            RejectDuplicateProperties(json.RootElement, documentPath, "$");
            var root = RequireObject(json.RootElement, documentPath, "$", "Scene root must be an object.");
            RequireShape(root, RootProperties, RootProperties, documentPath, "$");

            var format = ReadString(root, "format", documentPath, "$", "$.format");
            var version = ReadInt32(root, "version", documentPath, "$", "$.version");
            var name = ReadString(root, "name", documentPath, "$", "$.name");
            var nodesElement = GetRequired(root, "nodes", documentPath, "$", "$.nodes");
            if (nodesElement.ValueKind != JsonValueKind.Array)
                throw Error("Expected an array.", documentPath, "$.nodes");

            var nodes = new List<SceneNodeDocument>(nodesElement.GetArrayLength());
            var nodeIndex = 0;
            foreach (var nodeElement in nodesElement.EnumerateArray())
            {
                nodes.Add(ReadNode(nodeElement, documentPath, $"$.nodes[{nodeIndex}]"));
                nodeIndex++;
            }

            return new SceneDocument(format, version, name, nodes);
        }
        catch (SceneDocumentParseException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SceneDocumentParseException(
                exception.Message,
                documentPath,
                string.IsNullOrWhiteSpace(exception.Path) ? "$" : exception.Path,
                exception.LineNumber,
                exception.BytePositionInLine,
                exception);
        }
    }

    public static void Save(string path, SceneDocument document, bool indented = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(Path.GetFullPath(path), SerializeToUtf8(document, indented));
    }

    public static string Serialize(SceneDocument document, bool indented = true)
    {
        return Encoding.UTF8.GetString(SerializeToUtf8(document, indented));
    }

    public static byte[] SerializeToUtf8(SceneDocument document, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = indented }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", document.Format);
            writer.WriteNumber("version", document.Version);
            writer.WriteString("name", document.Name);
            writer.WriteStartArray("nodes");

            foreach (var node in document.Nodes)
                WriteNode(writer, node);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    private static SceneNodeDocument ReadNode(JsonElement element, string documentPath, string path)
    {
        var node = RequireObject(element, documentPath, path, "Scene node must be an object.");
        RequireShape(node, NodeProperties, ["id", "name", "transform", "components"], documentPath, path);

        var id = ReadString(node, "id", documentPath, path, $"{path}.id");
        var name = ReadString(node, "name", documentPath, path, $"{path}.name");
        var parentId = node.TryGetProperty("parent", out var parent)
            ? ReadString(parent, documentPath, $"{path}.parent")
            : null;
        var transform = ReadTransform(
            GetRequired(node, "transform", documentPath, path, $"{path}.transform"),
            documentPath,
            $"{path}.transform");

        var componentsElement = GetRequired(node, "components", documentPath, path, $"{path}.components");
        if (componentsElement.ValueKind != JsonValueKind.Array)
            throw Error("Expected an array.", documentPath, $"{path}.components");

        var components = new List<SceneComponentDocument>(componentsElement.GetArrayLength());
        var componentIndex = 0;
        foreach (var componentElement in componentsElement.EnumerateArray())
        {
            components.Add(ReadComponent(componentElement, documentPath, $"{path}.components[{componentIndex}]"));
            componentIndex++;
        }

        return new SceneNodeDocument(id, name, parentId, transform, components);
    }

    private static SceneTransformDocument ReadTransform(JsonElement element, string documentPath, string path)
    {
        var transform = RequireObject(element, documentPath, path, "Transform must be an object.");
        RequireShape(transform, TransformProperties, TransformProperties, documentPath, path);
        return new SceneTransformDocument(
            ReadVector3(transform, "position", documentPath, path),
            ReadVector3(transform, "rotationDegrees", documentPath, path),
            ReadVector3(transform, "scale", documentPath, path));
    }

    private static SceneComponentDocument ReadComponent(JsonElement element, string documentPath, string path)
    {
        var component = RequireObject(element, documentPath, path, "Scene component must be an object.");
        RequireShape(component, ComponentProperties, ComponentProperties, documentPath, path);
        var properties = GetRequired(component, "properties", documentPath, path, $"{path}.properties");
        if (properties.ValueKind != JsonValueKind.Object)
            throw Error("Expected an object.", documentPath, $"{path}.properties");

        return new SceneComponentDocument(
            ReadString(component, "id", documentPath, path, $"{path}.id"),
            ReadString(component, "type", documentPath, path, $"{path}.type"),
            properties);
    }

    private static Vector3 ReadVector3(JsonElement owner, string propertyName, string documentPath, string ownerPath)
    {
        var path = $"{ownerPath}.{propertyName}";
        var value = GetRequired(owner, propertyName, documentPath, ownerPath, path);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            throw Error("Expected an array containing exactly three numbers.", documentPath, path);

        Span<float> values = stackalloc float[3];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out values[index]) || !float.IsFinite(values[index]))
                throw Error("Expected a finite single-precision number.", documentPath, $"{path}[{index}]");
            index++;
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    private static string ReadString(
        JsonElement owner,
        string propertyName,
        string documentPath,
        string ownerPath,
        string propertyPath)
    {
        return ReadString(GetRequired(owner, propertyName, documentPath, ownerPath, propertyPath), documentPath, propertyPath);
    }

    private static string ReadString(JsonElement value, string documentPath, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw Error("Expected a string.", documentPath, path);
        return value.GetString()!;
    }

    private static int ReadInt32(
        JsonElement owner,
        string propertyName,
        string documentPath,
        string ownerPath,
        string propertyPath)
    {
        var value = GetRequired(owner, propertyName, documentPath, ownerPath, propertyPath);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw Error("Expected a 32-bit integer.", documentPath, propertyPath);
        return result;
    }

    private static JsonElement RequireObject(JsonElement value, string documentPath, string path, string message)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(message, documentPath, path);
        return value;
    }

    private static JsonElement GetRequired(
        JsonElement owner,
        string propertyName,
        string documentPath,
        string ownerPath,
        string propertyPath)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
            throw Error($"Required property '{propertyName}' is missing.", documentPath, ownerPath);
        return value;
    }

    private static void RequireShape(
        JsonElement owner,
        IReadOnlyCollection<string> allowed,
        IReadOnlyCollection<string> required,
        string documentPath,
        string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in owner.EnumerateObject())
        {
            var propertyPath = $"{path}.{property.Name}";
            if (!seen.Add(property.Name))
                throw Error($"Property '{property.Name}' occurs more than once.", documentPath, propertyPath);
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw Error($"Unknown property '{property.Name}'.", documentPath, propertyPath);
        }

        foreach (var propertyName in required)
        {
            if (!seen.Contains(propertyName))
                throw Error($"Required property '{propertyName}' is missing.", documentPath, path);
        }
    }

    private static void RejectDuplicateProperties(JsonElement value, string documentPath, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!seen.Add(property.Name))
                    throw Error($"Property '{property.Name}' occurs more than once.", documentPath, propertyPath);
                RejectDuplicateProperties(property.Value, documentPath, propertyPath);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                RejectDuplicateProperties(item, documentPath, $"{path}[{index}]");
                index++;
            }
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, SceneNodeDocument node)
    {
        writer.WriteStartObject();
        writer.WriteString("id", node.Id);
        writer.WriteString("name", node.Name);
        if (node.ParentId is not null)
            writer.WriteString("parent", node.ParentId);

        writer.WriteStartObject("transform");
        WriteVector3(writer, "position", node.Transform.Position);
        WriteVector3(writer, "rotationDegrees", NormalizeDegrees(node.Transform.RotationDegrees));
        WriteVector3(writer, "scale", node.Transform.Scale);
        writer.WriteEndObject();

        writer.WriteStartArray("components");
        foreach (var component in node.Components)
        {
            writer.WriteStartObject();
            writer.WriteString("id", component.Id);
            writer.WriteString("type", component.Type);
            writer.WritePropertyName("properties");
            component.Properties.WriteTo(writer);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteVector3(Utf8JsonWriter writer, string propertyName, Vector3 value)
    {
        writer.WriteStartArray(propertyName);
        writer.WriteNumberValue(CanonicalZero(value.X));
        writer.WriteNumberValue(CanonicalZero(value.Y));
        writer.WriteNumberValue(CanonicalZero(value.Z));
        writer.WriteEndArray();
    }

    private static Vector3 NormalizeDegrees(Vector3 value)
    {
        return new Vector3(NormalizeDegrees(value.X), NormalizeDegrees(value.Y), NormalizeDegrees(value.Z));
    }

    private static float NormalizeDegrees(float value)
    {
        var normalized = (value + 180f) % 360f;
        if (normalized < 0f)
            normalized += 360f;
        return CanonicalZero(normalized - 180f);
    }

    private static float CanonicalZero(float value) => value == 0f ? 0f : value;

    private static SceneDocumentParseException Error(string message, string documentPath, string jsonPath)
    {
        return new SceneDocumentParseException(message, documentPath, jsonPath);
    }
}

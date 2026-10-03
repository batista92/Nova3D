using System.Text.Json;
using Microsoft.Xna.Framework;
using Nova3D.Production.Scenes.Assets;

namespace Nova3D.Production.Scenes.Prefabs;

/// <summary>Expands scene/1 prefab references into a validated CPU-only load plan.</summary>
public static class ScenePrefabLoader
{
    public static SceneLoadPlan Prepare(
        string scenePath,
        SceneComponentRegistry components,
        SceneAssetResolver assets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(assets);
        var fullPath = Path.GetFullPath(scenePath);
        return Prepare(SceneDocumentSerializer.Load(fullPath), fullPath, components, assets);
    }

    public static SceneLoadPlan Prepare(
        SceneDocument document,
        string documentPath,
        SceneComponentRegistry components,
        SceneAssetResolver assets)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(assets);
        if (components.TryGet(ScenePrefabComponentDescriptor.ComponentType, out _))
            throw new InvalidOperationException("nova3d.prefab is reserved for ScenePrefabLoader.");

        var validationRegistry = new SceneComponentRegistry();
        foreach (var type in components.Types)
            validationRegistry.Register(components.GetRequired(type));
        validationRegistry.Register(new ScenePrefabComponentDescriptor());

        var output = new List<SceneNodeDocument>();
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var active = new HashSet<string>(pathComparer);
        var rootPath = Path.GetFullPath(documentPath);
        active.Add(rootPath);
        Expand(document, documentPath, string.Empty, null,
            validationRegistry, assets, active, output);

        var expanded = new SceneDocument(document.Format, document.Version, document.Name, output);
        return SceneLoader.Prepare(expanded, components, documentPath);
    }

    private static void Expand(
        SceneDocument document,
        string documentPath,
        string prefix,
        string? rootParent,
        SceneComponentRegistry validationRegistry,
        SceneAssetResolver assets,
        HashSet<string> active,
        List<SceneNodeDocument> output)
    {
        var validation = SceneDocumentValidator.Validate(document, validationRegistry);
        if (!validation.IsValid)
            throw new SceneDocumentValidationException(documentPath, validation.Issues);

        foreach (var node in document.Nodes)
        {
            var id = prefix + node.Id;
            var parentId = node.ParentId is null ? rootParent : prefix + node.ParentId;
            var prefabComponents = node.Components
                .Where(component => component.Type == ScenePrefabComponentDescriptor.ComponentType)
                .ToArray();
            if (prefabComponents.Length > 1)
                throw new ScenePrefabException(documentPath, $"node:{node.Id}",
                    "A prefab anchor may contain only one nova3d.prefab component.");

            output.Add(new SceneNodeDocument(id, node.Name, parentId, node.Transform,
                node.Components.Where(component => component.Type != ScenePrefabComponentDescriptor.ComponentType)));

            if (prefabComponents.Length == 0)
                continue;

            var properties = prefabComponents[0].Properties;
            var reference = properties.GetProperty("asset").GetString()!;
            var path = assets.ResolveExistingFile(documentPath, reference);
            if (!active.Add(path))
                throw new ScenePrefabException(documentPath, $"node:{node.Id}.components:{prefabComponents[0].Id}",
                    $"Recursive prefab reference to '{reference}'.");

            try
            {
                var prefab = SceneDocumentSerializer.Load(path);
                if (properties.TryGetProperty("overrides", out var overrides))
                    prefab = ApplyOverrides(prefab, overrides, documentPath, node.Id);
                Expand(prefab, path, id + ".", id, validationRegistry, assets, active, output);
            }
            finally
            {
                active.Remove(path);
            }
        }
    }

    private static SceneDocument ApplyOverrides(
        SceneDocument prefab,
        JsonElement overrides,
        string sourcePath,
        string anchorId)
    {
        var nodes = prefab.Nodes.ToArray();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in overrides.EnumerateArray())
        {
            var location = $"node:{anchorId}.overrides[{index}]";
            var nodeId = item.GetProperty("node").GetString()!;
            var propertyName = item.GetProperty("property").GetString()!;
            var componentId = item.TryGetProperty("component", out var componentValue)
                ? componentValue.GetString() : null;
            var key = $"{nodeId}\0{componentId}\0{propertyName}";
            if (!used.Add(key))
                throw new ScenePrefabException(sourcePath, location,
                    "The same prefab property is overridden more than once.");

            var nodeIndex = Array.FindIndex(nodes, node => node.Id == nodeId);
            if (nodeIndex < 0)
                throw new ScenePrefabException(sourcePath, location,
                    $"Prefab node '{nodeId}' does not exist.");
            var node = nodes[nodeIndex];
            var replacement = item.GetProperty("value");
            if (componentId is null)
            {
                var transform = OverrideTransform(node.Transform, propertyName, replacement, sourcePath, location);
                nodes[nodeIndex] = new SceneNodeDocument(node.Id, node.Name, node.ParentId,
                    transform, node.Components);
            }
            else
            {
                var components = node.Components.ToArray();
                var componentIndex = Array.FindIndex(components, component => component.Id == componentId);
                if (componentIndex < 0)
                    throw new ScenePrefabException(sourcePath, location,
                        $"Component '{componentId}' does not exist on prefab node '{nodeId}'.");
                var component = components[componentIndex];
                if (!component.Properties.TryGetProperty(propertyName, out var original))
                    throw new ScenePrefabException(sourcePath, location,
                        $"Component property '{propertyName}' does not exist.");
                if (!SameTypeShape(original, replacement))
                    throw new ScenePrefabException(sourcePath, location,
                        $"Override for '{propertyName}' must retain the property's JSON type and shape.");
                components[componentIndex] = new SceneComponentDocument(component.Id, component.Type,
                    ReplaceProperty(component.Properties, propertyName, replacement));
                nodes[nodeIndex] = new SceneNodeDocument(node.Id, node.Name, node.ParentId,
                    node.Transform, components);
            }
            index++;
        }
        return new SceneDocument(prefab.Format, prefab.Version, prefab.Name, nodes);
    }

    private static SceneTransformDocument OverrideTransform(
        SceneTransformDocument original,
        string property,
        JsonElement value,
        string sourcePath,
        string location)
    {
        var vector = ReadVector3(value, sourcePath, location);
        return property switch
        {
            "position" => new SceneTransformDocument(vector, original.RotationDegrees, original.Scale),
            "rotationDegrees" => new SceneTransformDocument(original.Position, vector, original.Scale),
            "scale" => new SceneTransformDocument(original.Position, original.RotationDegrees, vector),
            _ => throw new ScenePrefabException(sourcePath, location,
                $"Transform property '{property}' is not overridable.")
        };
    }

    private static Vector3 ReadVector3(JsonElement value, string path, string location)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            throw new ScenePrefabException(path, location, "Transform override must be a vector of three numbers.");
        var values = new float[3];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out values[index]) ||
                !float.IsFinite(values[index]))
                throw new ScenePrefabException(path, location, "Transform override must contain finite numbers.");
            index++;
        }
        return new Vector3(values[0], values[1], values[2]);
    }

    private static bool SameTypeShape(JsonElement original, JsonElement replacement)
    {
        if (original.ValueKind == JsonValueKind.Number)
            return replacement.ValueKind == JsonValueKind.Number &&
                   replacement.TryGetSingle(out var number) && float.IsFinite(number);
        if (original.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return replacement.ValueKind is JsonValueKind.True or JsonValueKind.False;
        if (original.ValueKind != replacement.ValueKind)
            return false;
        if (original.ValueKind == JsonValueKind.Array)
        {
            var left = original.EnumerateArray().ToArray();
            var right = replacement.EnumerateArray().ToArray();
            return left.Length == right.Length &&
                   left.Zip(right).All(pair => SameTypeShape(pair.First, pair.Second));
        }
        if (original.ValueKind == JsonValueKind.Object)
        {
            var left = original.EnumerateObject().ToArray();
            var right = replacement.EnumerateObject().ToArray();
            return left.Length == right.Length && left.All(property =>
                replacement.TryGetProperty(property.Name, out var value) &&
                SameTypeShape(property.Value, value));
        }
        return true;
    }

    private static JsonElement ReplaceProperty(JsonElement properties, string property, JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var item in properties.EnumerateObject())
            {
                writer.WritePropertyName(item.Name);
                (item.Name == property ? value : item.Value).WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}

using System.Text.Json;

namespace Nova3D.Production.Scenes.Prefabs;

/// <summary>Validates the prefab reference envelope; expansion handles target checks.</summary>
public sealed class ScenePrefabComponentDescriptor : ISceneComponentDescriptor
{
    public const string ComponentType = "nova3d.prefab";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        var properties = context.Component.Properties;
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Name is not ("asset" or "overrides"))
                context.Report("SCN404", $"Unknown prefab property '{property.Name}'.", property.Name);
        }

        if (!properties.TryGetProperty("asset", out var asset))
            context.Report("SCN401", "Prefab asset is required.", "asset");
        else if (asset.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(asset.GetString()) ||
                 !asset.GetString()!.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
            context.Report("SCN402", "Prefab asset must be a non-empty .scene.json path.", "asset");

        if (properties.TryGetProperty("overrides", out var overrides))
        {
            if (overrides.ValueKind != JsonValueKind.Array)
            {
                context.Report("SCN402", "Overrides must be an array.", "overrides");
                return;
            }
            var index = 0;
            foreach (var item in overrides.EnumerateArray())
            {
                var path = $"overrides[{index}]";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    context.Report("SCN402", "Override must be an object.", path);
                    index++;
                    continue;
                }
                foreach (var field in item.EnumerateObject())
                {
                    if (field.Name is not ("node" or "component" or "property" or "value"))
                        context.Report("SCN404", $"Unknown override field '{field.Name}'.", $"{path}.{field.Name}");
                }
                RequireString(item, "node", context, path);
                RequireString(item, "property", context, path);
                if (item.TryGetProperty("component", out var component) &&
                    (component.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(component.GetString())))
                    context.Report("SCN402", "Component must be a non-empty string.", $"{path}.component");
                if (!item.TryGetProperty("value", out _))
                    context.Report("SCN401", "Override value is required.", $"{path}.value");
                index++;
            }
        }
    }

    private static void RequireString(JsonElement item, string name,
        SceneComponentValidationContext context, string path)
    {
        if (!item.TryGetProperty(name, out var value))
            context.Report("SCN401", $"Override {name} is required.", $"{path}.{name}");
        else if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            context.Report("SCN402", $"Override {name} must be a non-empty string.", $"{path}.{name}");
    }
}
